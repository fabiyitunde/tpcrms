using System.Text.Json;
using CRMS.Application.Access;
using CRMS.Application.Common;
using CRMS.Application.CustomerCheck;
using CRMS.Domain.Aggregates.Access;
using CRMS.Domain.Aggregates.CreditCheck;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Services;


namespace CRMS.Application.CreditCheck;

/// <summary>Shared by the staff-check handlers: who may see what, and how a row becomes a view.</summary>
public static class StaffCreditChecks
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The keyed hash a company is stored under. Prefixed so it can never collide with a
    /// BVN's hash.</summary>
    public static string CompanyHashInput(string rcNumber) => "RC:" + rcNumber;

    public static StaffCreditCheckSummary Summarise(StaffCreditCheck c) => new(
        c.Id, c.SubjectType, c.SubjectLabel, c.SubjectName, c.Status, c.RequestedByName, c.RequestedByUserId,
        c.Purpose, c.CaseReference, c.CreatedAt, c.CompletedAt, c.AdvisoryScore, c.AdvisoryRating,
        c.AdvisoryRecommendation, c.WarningCount);

    public static StaffCreditCheckView View(StaffCreditCheck c, ICustomerCheckCipher cipher)
    {
        CustomerCheckResponse? individual = null;
        CompanyCheckResponse? company = null;
        if (c.Status == StaffCreditCheck.Completed && c.ResultCiphertext is not null)
        {
            var json = cipher.Unprotect(c.ResultCiphertext);
            if (c.SubjectType == StaffCreditCheck.Company)
                company = JsonSerializer.Deserialize<CompanyCheckResponse>(json, Json);
            else
                individual = JsonSerializer.Deserialize<CustomerCheckResponse>(json, Json);
        }
        return new StaffCreditCheckView(
            Summarise(c), c.SectionList, c.OutcomeMap, c.StartedAt, c.DurationMs, c.BilledCalls, c.BureauUsed,
            c.ErrorMessage, individual, company);
    }
}

// ── Queue a check ────────────────────────────────────────────────────────────────────────────────

public record QueueStaffCreditCheckCommand(Guid UserId, StaffCreditCheckRequest Request) : IRequest<ApplicationResult<Guid>>;

public class QueueStaffCreditCheckHandler : IRequestHandler<QueueStaffCreditCheckCommand, ApplicationResult<Guid>>
{
    private readonly IFeatureAccessService _access;
    private readonly IStaffCreditCheckRepository _checks;
    private readonly IUserRepository _users;
    private readonly ICustomerCheckService _customerCheck;
    private readonly ICustomerCheckCipher _cipher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditService _audit;
    private readonly IStaffCreditCheckSignal? _signal;
    private readonly CustomerCheckSettings _settings;

    public QueueStaffCreditCheckHandler(
        IFeatureAccessService access, IStaffCreditCheckRepository checks, IUserRepository users,
        ICustomerCheckService customerCheck, ICustomerCheckCipher cipher, IUnitOfWork unitOfWork, AuditService audit,
        CustomerCheckSettings settings, IStaffCreditCheckSignal? signal = null)
    {
        _access = access;
        _checks = checks;
        _users = users;
        _customerCheck = customerCheck;
        _cipher = cipher;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _settings = settings;
        _signal = signal;
    }

    public async Task<ApplicationResult<Guid>> Handle(QueueStaffCreditCheckCommand command, CancellationToken ct = default)
    {
        if (!await _access.IsAllowedAsync(command.UserId, FeatureKeys.CreditCheckRun, ct))
            return ApplicationResult<Guid>.Failure("You do not have access to run credit checks. Ask an administrator to add your role in Admin > Feature Access.");
        var user = await _users.GetByIdAsync(command.UserId, ct);
        if (user is null)
            return ApplicationResult<Guid>.Failure("Your account could not be found.");
        if (await _checks.CountOpenAsync(command.UserId, ct) > 0)
            return ApplicationResult<Guid>.Failure("You already have a check running. It must finish before you start another.");

        var r = command.Request;
        var purpose = r.Purpose?.Trim() ?? string.Empty;
        if (purpose.Length < 5)
            return ApplicationResult<Guid>.Failure("Say briefly why you are running this check (at least 5 characters).");

        var requestId = $"staff-{Guid.NewGuid():N}";
        string subjectType, hash, label, ciphertext;
        IReadOnlyList<string> sections;
        SensitiveDataType dataType;

        if (r.SubjectType == StaffCreditCheck.Company)
        {
            var (query, errors) = CompanyCheckRules.Validate(new CompanyCheckRequest(
                requestId, r.RcNumber, r.CompanyName, r.Nubans, r.Sections,
                new CompanyCheckOptions(null, r.RunAmlCheck, r.LoanAmount, r.TenorMonths, r.AnnualRevenue, r.LoanPurpose)), _settings);
            if (query is null)
                return ApplicationResult<Guid>.Failure(errors.Select(Friendly).ToList());
            subjectType = StaffCreditCheck.Company;
            hash = _customerCheck.HashIdentifier(StaffCreditChecks.CompanyHashInput(query.RcNumber));
            label = $"{query.RcNumber} · {query.CompanyName}";
            sections = query.Sections;
            ciphertext = _cipher.Protect(JsonSerializer.Serialize(query, StaffCreditChecks.Json));
            dataType = SensitiveDataType.CreditReport;
        }
        else if (r.SubjectType == StaffCreditCheck.Individual)
        {
            var (query, errors) = CustomerCheckRules.Validate(new CustomerCheckRequest(
                requestId, r.Bvn, r.Nubans, r.Sections,
                new CustomerCheckOptions(null, true, r.RunAmlCheck, r.LoanAmount, r.TenorMonths)), _settings);
            if (query is null)
                return ApplicationResult<Guid>.Failure(errors.Select(Friendly).ToList());
            subjectType = StaffCreditCheck.Individual;
            hash = _customerCheck.HashIdentifier(query.Bvn);
            label = $"BVN {CustomerCheckRules.MaskLast4(query.Bvn)}";
            sections = query.Sections;
            ciphertext = _cipher.Protect(JsonSerializer.Serialize(query, StaffCreditChecks.Json));
            dataType = SensitiveDataType.BVN;
        }
        else
            return ApplicationResult<Guid>.Failure("Choose an individual or a company check.");

        var created = StaffCreditCheck.Create(subjectType, hash, label, user.Id, user.FullName, purpose, r.CaseReference, sections, ciphertext);
        if (created.IsFailure)
            return ApplicationResult<Guid>.Failure(created.Error);

        await _checks.AddAsync(created.Value, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _audit.LogDataAccessAsync(
            user.Id, user.FullName, string.Empty, dataType, nameof(StaffCreditCheck), created.Value.Id, DataAccessType.View,
            entityReference: label, accessReason: purpose, ct: ct);
        _signal?.Nudge();
        return ApplicationResult<Guid>.Success(created.Value.Id);
    }

    /// <summary>The rules name request fields the way the partner API does; staff see form labels.</summary>
    private static string Friendly(string error) => error
        .Replace("bvn must be 11 digits.", "The BVN must be 11 digits.")
        .Replace("bvn is required.", "Enter the BVN.")
        .Replace("rcNumber is required.", "Enter the RC number.")
        .Replace("rcNumber must be", "The RC number must be")
        .Replace("companyName is required.", "Enter the company's registered name.")
        .Replace("options.loanAmount", "The loan amount")
        .Replace("options.tenorMonths", "The tenor")
        .Replace("options.annualRevenue", "Annual revenue")
        .Replace("options.loanPurpose", "The loan purpose")
        .Replace("nubans holds at most", "Enter at most")
        .Replace(" entries.", " account numbers.")
        .Replace("nubans[", "Account number ")
        .Replace("] must be 10 digits.", " must be 10 digits.");
}

// ── Open one check ───────────────────────────────────────────────────────────────────────────────

public record GetStaffCreditCheckQuery(Guid Id, Guid UserId) : IRequest<ApplicationResult<StaffCreditCheckView>>;

public class GetStaffCreditCheckHandler : IRequestHandler<GetStaffCreditCheckQuery, ApplicationResult<StaffCreditCheckView>>
{
    private readonly IFeatureAccessService _access;
    private readonly IStaffCreditCheckRepository _checks;
    private readonly IUserRepository _users;
    private readonly ICustomerCheckCipher _cipher;
    private readonly AuditService _audit;

    public GetStaffCreditCheckHandler(IFeatureAccessService access, IStaffCreditCheckRepository checks, IUserRepository users, ICustomerCheckCipher cipher, AuditService audit)
    {
        _access = access;
        _checks = checks;
        _users = users;
        _cipher = cipher;
        _audit = audit;
    }

    public async Task<ApplicationResult<StaffCreditCheckView>> Handle(GetStaffCreditCheckQuery query, CancellationToken ct = default)
    {
        var check = await _checks.GetByIdAsync(query.Id, ct);
        var opened = await StaffCreditCheckAccess.OpenAsync(check, query.UserId, _access, ct);
        if (opened is not null)
            return ApplicationResult<StaffCreditCheckView>.Failure(opened);

        if (check!.RequestedByUserId != query.UserId && check.Status == StaffCreditCheck.Completed)
            await StaffCreditCheckAccess.AuditOtherAsync(check, query.UserId, _users, _audit, DataAccessType.View, ct);
        return ApplicationResult<StaffCreditCheckView>.Success(StaffCreditChecks.View(check, _cipher));
    }
}

/// <summary>Polled by the page while a check runs: the status only, no decryption, no audit row.</summary>
public record GetStaffCreditCheckStatusQuery(Guid Id, Guid UserId) : IRequest<ApplicationResult<StaffCreditCheckSummary>>;

public class GetStaffCreditCheckStatusHandler : IRequestHandler<GetStaffCreditCheckStatusQuery, ApplicationResult<StaffCreditCheckSummary>>
{
    private readonly IFeatureAccessService _access;
    private readonly IStaffCreditCheckRepository _checks;

    public GetStaffCreditCheckStatusHandler(IFeatureAccessService access, IStaffCreditCheckRepository checks)
    {
        _access = access;
        _checks = checks;
    }

    public async Task<ApplicationResult<StaffCreditCheckSummary>> Handle(GetStaffCreditCheckStatusQuery query, CancellationToken ct = default)
    {
        var check = await _checks.GetByIdAsync(query.Id, ct);
        var opened = await StaffCreditCheckAccess.OpenAsync(check, query.UserId, _access, ct);
        return opened is not null
            ? ApplicationResult<StaffCreditCheckSummary>.Failure(opened)
            : ApplicationResult<StaffCreditCheckSummary>.Success(StaffCreditChecks.Summarise(check!));
    }
}

// ── History ──────────────────────────────────────────────────────────────────────────────────────

public record ListStaffCreditChecksQuery(
    Guid UserId, bool Everyone, string? SubjectType, string? Status, DateTime? From, DateTime? To, int Page = 1, int PageSize = 25)
    : IRequest<ApplicationResult<StaffCreditCheckPage>>;

public class ListStaffCreditChecksHandler : IRequestHandler<ListStaffCreditChecksQuery, ApplicationResult<StaffCreditCheckPage>>
{
    private readonly IFeatureAccessService _access;
    private readonly IStaffCreditCheckRepository _checks;

    public ListStaffCreditChecksHandler(IFeatureAccessService access, IStaffCreditCheckRepository checks)
    {
        _access = access;
        _checks = checks;
    }

    public async Task<ApplicationResult<StaffCreditCheckPage>> Handle(ListStaffCreditChecksQuery query, CancellationToken ct = default)
    {
        if (query.Everyone)
        {
            if (!await _access.IsAllowedAsync(query.UserId, FeatureKeys.CreditCheckViewAll, ct))
                return ApplicationResult<StaffCreditCheckPage>.Failure("You can only see the checks you ran.");
        }
        else if (!await _access.IsAllowedAsync(query.UserId, FeatureKeys.CreditCheckRun, ct))
            return ApplicationResult<StaffCreditCheckPage>.Failure("You do not have access to credit checks.");

        var (items, total) = await _checks.SearchAsync(new StaffCreditCheckFilter(
            query.Everyone ? null : query.UserId, query.SubjectType, query.Status, null,
            query.From, query.To?.Date.AddDays(1), query.Page, query.PageSize), ct);
        return ApplicationResult<StaffCreditCheckPage>.Success(new StaffCreditCheckPage(
            Math.Max(1, query.Page), Math.Clamp(query.PageSize, 1, 100), total, items.Select(StaffCreditChecks.Summarise).ToList()));
    }
}

// ── PDF ──────────────────────────────────────────────────────────────────────────────────────────

public record GetStaffCreditCheckPdfQuery(Guid Id, Guid UserId) : IRequest<ApplicationResult<(byte[] Content, string FileName)>>;

public class GetStaffCreditCheckPdfHandler : IRequestHandler<GetStaffCreditCheckPdfQuery, ApplicationResult<(byte[] Content, string FileName)>>
{
    private readonly IFeatureAccessService _access;
    private readonly IStaffCreditCheckRepository _checks;
    private readonly IUserRepository _users;
    private readonly ICustomerCheckCipher _cipher;
    private readonly ICreditCheckPdfGenerator _pdf;
    private readonly AuditService _audit;

    public GetStaffCreditCheckPdfHandler(
        IFeatureAccessService access, IStaffCreditCheckRepository checks, IUserRepository users,
        ICustomerCheckCipher cipher, ICreditCheckPdfGenerator pdf, AuditService audit)
    {
        _access = access;
        _checks = checks;
        _users = users;
        _cipher = cipher;
        _pdf = pdf;
        _audit = audit;
    }

    public async Task<ApplicationResult<(byte[] Content, string FileName)>> Handle(GetStaffCreditCheckPdfQuery query, CancellationToken ct = default)
    {
        var check = await _checks.GetByIdAsync(query.Id, ct);
        var opened = await StaffCreditCheckAccess.OpenAsync(check, query.UserId, _access, ct);
        if (opened is not null)
            return ApplicationResult<(byte[], string)>.Failure(opened);
        if (check!.Status != StaffCreditCheck.Completed)
            return ApplicationResult<(byte[], string)>.Failure("The check has not finished, so there is no report yet.");

        var user = await _users.GetByIdAsync(query.UserId, ct);
        var printedBy = user?.FullName ?? "CRMS user";
        var bytes = _pdf.Generate(StaffCreditChecks.View(check, _cipher), DateTime.UtcNow, printedBy);
        await _audit.LogDataAccessAsync(
            query.UserId, printedBy, string.Empty, SensitiveDataType.CreditReport, nameof(StaffCreditCheck), check.Id,
            DataAccessType.Download, entityReference: check.SubjectLabel,
            accessReason: check.RequestedByUserId == query.UserId ? check.Purpose : $"Opened a check run by {check.RequestedByName}", ct: ct);

        var kind = check.SubjectType == StaffCreditCheck.Company ? "Company" : "Individual";
        return ApplicationResult<(byte[], string)>.Success((bytes, $"Credit-Check-{kind}-{check.CreatedAt.AddHours(1):yyyyMMdd-HHmm}.pdf"));
    }
}

internal static class StaffCreditCheckAccess
{
    /// <summary>Null when the user may open the check; otherwise why not. A check that does not
    /// exist and one the user may not see read the same.</summary>
    public static async Task<string?> OpenAsync(StaffCreditCheck? check, Guid userId, IFeatureAccessService access, CancellationToken ct)
    {
        if (check is null)
            return "Check not found.";
        if (check.RequestedByUserId == userId)
            return await access.IsAllowedAsync(userId, FeatureKeys.CreditCheckRun, ct) ? null : "You do not have access to credit checks.";
        return await access.IsAllowedAsync(userId, FeatureKeys.CreditCheckViewAll, ct) ? null : "Check not found.";
    }

    public static async Task AuditOtherAsync(StaffCreditCheck check, Guid userId, IUserRepository users, AuditService audit, DataAccessType kind, CancellationToken ct)
    {
        var viewer = await users.GetByIdAsync(userId, ct);
        await audit.LogDataAccessAsync(
            userId, viewer?.FullName ?? "CRMS user", string.Empty,
            check.SubjectType == StaffCreditCheck.Company ? SensitiveDataType.CreditReport : SensitiveDataType.BVN,
            nameof(StaffCreditCheck), check.Id, kind, entityReference: check.SubjectLabel,
            accessReason: $"Opened a check run by {check.RequestedByName}", ct: ct);
    }
}
