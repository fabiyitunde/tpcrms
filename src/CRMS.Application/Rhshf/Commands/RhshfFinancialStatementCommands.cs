using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

// ── Save (create or update a year) ────────────────────────────────────────────
public record SaveRhshfFinancialStatementCommand(
    string Reference, Guid? StatementId, Guid ActorUserId,
    int FinancialYear, string? YearEndDate, RhshfFinancialYearType YearType, string Currency,
    RhshfFinancialInputMethod InputMethod, RhshfFinancialStatementFigures Figures,
    string? OriginalFileName = null, string? FilePath = null) : IRequest<ApplicationResult<Guid>>;

public class SaveRhshfFinancialStatementHandler : IRequestHandler<SaveRhshfFinancialStatementCommand, ApplicationResult<Guid>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfFinancialStatementRepository _statements;
    private readonly IUnitOfWork _uow;

    public SaveRhshfFinancialStatementHandler(
        IRhshfCreditProfileRepository repo, IRhshfFinancialStatementRepository statements, IUnitOfWork uow)
    {
        _repo = repo;
        _statements = statements;
        _uow = uow;
    }

    public async Task<ApplicationResult<Guid>> Handle(SaveRhshfFinancialStatementCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<Guid>.Failure("Case not found.");

        // A given year can only be captured once per case.
        var sameYear = await _statements.GetByProfileAndYearAsync(profile.Id, request.FinancialYear, ct);
        if (sameYear is not null && sameYear.Id != request.StatementId)
            return ApplicationResult<Guid>.Failure($"A {request.FinancialYear} statement already exists for this case.");

        RhshfFinancialStatement statement;
        if (request.StatementId is Guid id)
        {
            var existing = await _statements.GetByIdAsync(id, ct);
            if (existing is null || existing.RhshfCreditProfileId != profile.Id)
                return ApplicationResult<Guid>.Failure("Statement not found.");

            var meta = existing.SetYearMeta(request.FinancialYear, request.YearEndDate, request.YearType, request.Currency);
            if (meta.IsFailure) return ApplicationResult<Guid>.Failure(meta.Error);

            var set = existing.SetFigures(request.Figures);
            if (set.IsFailure) return ApplicationResult<Guid>.Failure(set.Error);

            existing.SetAuditInfo(request.ActorUserId.ToString());
            statement = existing;
        }
        else
        {
            var created = RhshfFinancialStatement.Create(
                profile.Id, request.FinancialYear, request.YearEndDate, request.YearType, request.InputMethod,
                request.ActorUserId, request.Currency, request.OriginalFileName, request.FilePath);
            if (created.IsFailure) return ApplicationResult<Guid>.Failure(created.Error);

            statement = created.Value;
            var set = statement.SetFigures(request.Figures);
            if (set.IsFailure) return ApplicationResult<Guid>.Failure(set.Error);

            statement.SetAuditInfo(request.ActorUserId.ToString(), isNew: true);
            await _statements.AddAsync(statement, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult<Guid>.Success(statement.Id);
    }
}

// ── Lifecycle transitions ─────────────────────────────────────────────────────
/// <summary>
/// Imports a financial statements file in place of everything already on the case.
/// <para>
/// A new file is the FAC's latest statement of its financials, so it supersedes every earlier
/// statement — imported, typed in, verified or not. Importing year by year instead left the old
/// years behind: a corrected file skipped any year already present, so the zeros from a blank
/// template outlived the figures that should have replaced them.
/// </para>
/// </summary>
public record ReplaceRhshfFinancialStatementsCommand(
    string Reference, Guid ActorUserId, string Currency,
    IReadOnlyList<RhshfFinancialStatementImportYear> Years, string? OriginalFileName = null)
    : IRequest<ApplicationResult<RhshfFinancialStatementsReplaced>>;

public record RhshfFinancialStatementImportYear(
    int FinancialYear, RhshfFinancialYearType YearType, RhshfFinancialStatementFigures Figures);

/// <param name="VerifiedYearsReplaced">Years an officer had verified that the new file replaced, so
/// the officer knows those figures need verifying again.</param>
public record RhshfFinancialStatementsReplaced(int Imported, int Replaced, IReadOnlyList<int> VerifiedYearsReplaced);

public class ReplaceRhshfFinancialStatementsHandler
    : IRequestHandler<ReplaceRhshfFinancialStatementsCommand, ApplicationResult<RhshfFinancialStatementsReplaced>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfFinancialStatementRepository _statements;
    private readonly IUnitOfWork _uow;

    public ReplaceRhshfFinancialStatementsHandler(
        IRhshfCreditProfileRepository repo, IRhshfFinancialStatementRepository statements, IUnitOfWork uow)
    {
        _repo = repo;
        _statements = statements;
        _uow = uow;
    }

    public async Task<ApplicationResult<RhshfFinancialStatementsReplaced>> Handle(
        ReplaceRhshfFinancialStatementsCommand request, CancellationToken ct = default)
    {
        if (request.Years.Count == 0)
            return ApplicationResult<RhshfFinancialStatementsReplaced>.Failure("The file has no financial years to import.");

        var duplicate = request.Years.GroupBy(y => y.FinancialYear).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            return ApplicationResult<RhshfFinancialStatementsReplaced>.Failure($"The file has {duplicate.Key} more than once.");

        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfFinancialStatementsReplaced>.Failure("Case not found.");

        // Build every new statement before touching the old ones, so a year that fails validation
        // leaves the case exactly as it was.
        var incoming = new List<RhshfFinancialStatement>();
        foreach (var year in request.Years.OrderBy(y => y.FinancialYear))
        {
            var created = RhshfFinancialStatement.Create(
                profile.Id, year.FinancialYear, null, year.YearType, RhshfFinancialInputMethod.ExcelUpload,
                request.ActorUserId, request.Currency, request.OriginalFileName);
            if (created.IsFailure)
                return ApplicationResult<RhshfFinancialStatementsReplaced>.Failure($"{year.FinancialYear}: {created.Error}");

            var set = created.Value.SetFigures(year.Figures);
            if (set.IsFailure)
                return ApplicationResult<RhshfFinancialStatementsReplaced>.Failure($"{year.FinancialYear}: {set.Error}");

            created.Value.SetAuditInfo(request.ActorUserId.ToString(), isNew: true);
            incoming.Add(created.Value);
        }

        var existing = await _statements.GetByProfileIdAsync(profile.Id, ct);
        var verifiedYears = existing
            .Where(s => s.Status == RhshfFinancialStatementStatus.Verified)
            .Select(s => s.FinancialYear)
            .OrderBy(y => y)
            .ToList();

        foreach (var old in existing)
            _statements.Remove(old);
        foreach (var statement in incoming)
            await _statements.AddAsync(statement, ct);

        // One save: the old years go and the new ones arrive together, or not at all. EF orders the
        // deletes ahead of inserts that reuse a year, which the per-case unique index requires.
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<RhshfFinancialStatementsReplaced>.Success(
            new RhshfFinancialStatementsReplaced(incoming.Count, existing.Count, verifiedYears));
    }
}

public record SubmitRhshfFinancialStatementCommand(Guid StatementId) : IRequest<ApplicationResult>;
public record VerifyRhshfFinancialStatementCommand(Guid StatementId, Guid ActorUserId, string? Notes) : IRequest<ApplicationResult>;
public record RejectRhshfFinancialStatementCommand(Guid StatementId, string Reason) : IRequest<ApplicationResult>;
public record RevertRhshfFinancialStatementCommand(Guid StatementId) : IRequest<ApplicationResult>;
public record DeleteRhshfFinancialStatementCommand(Guid StatementId) : IRequest<ApplicationResult>;

public class RhshfFinancialStatementLifecycleHandler :
    IRequestHandler<SubmitRhshfFinancialStatementCommand, ApplicationResult>,
    IRequestHandler<VerifyRhshfFinancialStatementCommand, ApplicationResult>,
    IRequestHandler<RejectRhshfFinancialStatementCommand, ApplicationResult>,
    IRequestHandler<RevertRhshfFinancialStatementCommand, ApplicationResult>,
    IRequestHandler<DeleteRhshfFinancialStatementCommand, ApplicationResult>
{
    private readonly IRhshfFinancialStatementRepository _statements;
    private readonly IUnitOfWork _uow;

    public RhshfFinancialStatementLifecycleHandler(IRhshfFinancialStatementRepository statements, IUnitOfWork uow)
    {
        _statements = statements;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(SubmitRhshfFinancialStatementCommand request, CancellationToken ct = default)
        => await Apply(request.StatementId, s => s.Submit(), ct);

    public async Task<ApplicationResult> Handle(VerifyRhshfFinancialStatementCommand request, CancellationToken ct = default)
        => await Apply(request.StatementId, s => s.Verify(request.ActorUserId, request.Notes), ct);

    public async Task<ApplicationResult> Handle(RejectRhshfFinancialStatementCommand request, CancellationToken ct = default)
        => await Apply(request.StatementId, s => s.Reject(request.Reason), ct);

    public async Task<ApplicationResult> Handle(RevertRhshfFinancialStatementCommand request, CancellationToken ct = default)
        => await Apply(request.StatementId, s => s.RevertToDraft(), ct);

    public async Task<ApplicationResult> Handle(DeleteRhshfFinancialStatementCommand request, CancellationToken ct = default)
    {
        var statement = await _statements.GetByIdAsync(request.StatementId, ct);
        if (statement is null) return ApplicationResult.Failure("Statement not found.");
        _statements.Remove(statement);
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }

    private async Task<ApplicationResult> Apply(Guid id, Func<RhshfFinancialStatement, Result> action, CancellationToken ct)
    {
        var statement = await _statements.GetByIdAsync(id, ct);
        if (statement is null) return ApplicationResult.Failure("Statement not found.");
        var result = action(statement);
        if (result.IsFailure) return ApplicationResult.Failure(result.Error);
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
