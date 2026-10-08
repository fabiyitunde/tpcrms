using System.Text.Json;
using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

// ── CAC fetch ──────────────────────────────────────────────────────────────

/// <summary>Pulls the FAC's company profile and director list from SmartComply's CAC lookup.
/// Mirrors FetchNampCacDetailsCommand — same cost guard (don't re-bill unless explicitly
/// refreshing) and same prune-CAC-but-keep-manual semantics.</summary>
public record FetchRhshfCacDetailsCommand(string Reference, Guid UserId, bool ForceRefresh = false)
    : IRequest<ApplicationResult>;

public class FetchRhshfCacDetailsHandler : IRequestHandler<FetchRhshfCacDetailsCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly ISmartComplyProvider _smartComply;
    private readonly IUnitOfWork _uow;

    public FetchRhshfCacDetailsHandler(
        IRhshfCreditProfileRepository repo, ISmartComplyProvider smartComply, IUnitOfWork uow)
    {
        _repo = repo;
        _smartComply = smartComply;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(FetchRhshfCacDetailsCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        if (string.IsNullOrWhiteSpace(profile.RcNumber))
            return ApplicationResult.Failure("An RC number is required to fetch CAC details.");

        // Cost guard: SmartComply bills per lookup. Already-fetched cases are a no-op unless the
        // caller explicitly asked to refresh.
        if (profile.CacFetchedAt.HasValue && !request.ForceRefresh)
            return ApplicationResult.Success();

        var result = await _smartComply.VerifyCacAdvancedAsync(
            profile.RcNumber.Trim(), profile.CompanyName, DeriveCompanyType(profile.RcNumber), ct);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error ?? "CAC lookup failed.");

        var cac = result.Value;

        profile.SetCacCompanyProfile(
            cac.Status, cac.CompanyType, cac.RegistrationDate, cac.NatureOfBusiness,
            cac.ShareCapital, cac.CompanyId, cac.Address, cac.City, cac.State,
            JsonSerializer.Serialize(cac));

        // Track what this response contained so stale CAC-sourced rows can be pruned afterwards.
        var keptIds = new List<Guid>();

        foreach (var d in cac.Directors)
        {
            var fullName = !string.IsNullOrWhiteSpace(d.FullName)
                ? d.FullName!
                : $"{d.FirstName} {d.OtherName} {d.Surname}".Replace("  ", " ").Trim();

            var existing = profile.FindCacDirector(d.Id, fullName);
            if (existing is not null)
            {
                existing.RefreshCacFields(
                    fullName, d.Surname, d.FirstName, d.OtherName, d.Gender, d.DateOfBirth, d.Nationality,
                    d.Occupation, d.Email, d.PhoneNumber, d.Address, d.City, d.State,
                    d.IsChairman ?? false, d.DateOfAppointment, d.AffiliateType, d.Status,
                    d.TypeOfShares, d.NumSharesAlloted, d.IdentityNumber);
                keptIds.Add(existing.Id);
            }
            else
            {
                var director = RhshfDirector.FromCac(
                    profile.Id, d.Id, fullName, d.Surname, d.FirstName, d.OtherName, d.Gender, d.DateOfBirth,
                    d.Nationality, d.Occupation, d.Email, d.PhoneNumber, d.Address, d.City, d.State,
                    d.IsChairman ?? false, d.DateOfAppointment, d.AffiliateType, d.Status,
                    d.TypeOfShares, d.NumSharesAlloted, d.IdentityNumber);
                profile.AddDirector(director);
                keptIds.Add(director.Id);
            }
        }

        // Drops CAC-sourced directors absent from this response (left the company, or stale test
        // data). Manually-added directors always survive.
        profile.PruneCacDirectorsNotIn(keptIds);

        profile.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }

    private static string DeriveCompanyType(string rcNumber)
    {
        var rc = rcNumber.Trim().ToUpperInvariant();
        if (rc.StartsWith("BN")) return "BN";
        if (rc.StartsWith("IT")) return "IT";
        return "RC";
    }
}

// ── Manual CRUD ────────────────────────────────────────────────────────────

public record AddRhshfDirectorCommand(
    string Reference, string FullName, string? Bvn, decimal? ShareholdingPercent,
    bool IsChairman, string? Email, string? PhoneNumber, Guid UserId) : IRequest<ApplicationResult>;

public class AddRhshfDirectorHandler : IRequestHandler<AddRhshfDirectorCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUnitOfWork _uow;

    public AddRhshfDirectorHandler(IRhshfCreditProfileRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(AddRhshfDirectorCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        // Profiling adds carry no individual user (token-auth) and pass Guid.Empty — those are the
        // FAC declaring, and are protected from staff deletion. A real UserId means a staff addition.
        var declaredByFac = request.UserId == Guid.Empty;
        var result = RhshfDirector.CreateManual(
            profile.Id, request.FullName, request.Bvn, request.ShareholdingPercent,
            request.IsChairman, request.Email, request.PhoneNumber, declaredByFac);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        profile.AddDirector(result.Value);
        profile.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

/// <summary>Completes the two fields CAC routinely omits. Deliberately narrow — the rest of a
/// CAC-sourced director's data belongs to CAC and is overwritten on refresh.</summary>
public record UpdateRhshfDirectorCommand(
    string Reference, Guid DirectorId, string? Bvn, decimal? ShareholdingPercent, Guid UserId)
    : IRequest<ApplicationResult>;

public class UpdateRhshfDirectorHandler : IRequestHandler<UpdateRhshfDirectorCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUnitOfWork _uow;

    public UpdateRhshfDirectorHandler(IRhshfCreditProfileRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(UpdateRhshfDirectorCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var director = profile.Directors.FirstOrDefault(d => d.Id == request.DirectorId);
        if (director is null)
            return ApplicationResult.Failure("Director not found on this case.");

        var bvnResult = director.UpdateBvn(request.Bvn);
        if (bvnResult.IsFailure)
            return ApplicationResult.Failure(bvnResult.Error);

        var shareResult = director.UpdateShareholding(request.ShareholdingPercent);
        if (shareResult.IsFailure)
            return ApplicationResult.Failure(shareResult.Error);

        profile.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

public record RemoveRhshfDirectorCommand(string Reference, Guid DirectorId, Guid UserId) : IRequest<ApplicationResult>;

public class RemoveRhshfDirectorHandler : IRequestHandler<RemoveRhshfDirectorCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUnitOfWork _uow;

    public RemoveRhshfDirectorHandler(IRhshfCreditProfileRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(RemoveRhshfDirectorCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var result = profile.RemoveDirector(request.DirectorId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        profile.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
