using System.Security.Cryptography;
using CRMS.Application.Common;
using CRMS.Application.Identity.Interfaces;
using CRMS.Domain.Aggregates.Billing;
using CRMS.Domain.Constants;
using CRMS.Domain.Entities.Identity;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CRMS.Application.Billing.Commands;

/// <summary>Who may change billing. Every writing handler asks this first; the page's own check is only a convenience.</summary>
public static class BillingAccess
{
    public static async Task<ApplicationResult> RequireManagerAsync(IUserRepository users, Guid actorId, CancellationToken ct)
    {
        var actor = await users.GetByIdAsync(actorId, ct);
        if (actor is null || actor.Status != UserStatus.Active)
            return ApplicationResult.Failure("Your account could not be verified.");
        return IsManager(actor)
            ? ApplicationResult.Success()
            : ApplicationResult.Failure("Only a Billing Manager can do this.");
    }

    public static bool IsManager(ApplicationUser user) =>
        user.UserRoles.Any(r => r.Role is not null && r.Role.Name == Roles.BillingManager);
}

// ───────────────────────────── Rates ─────────────────────────────

/// <param name="Flow">Null for a rate that covers every flow; a loan flow for a platform fee.</param>
/// <param name="EffectiveFromMonth">Any date in the month the rate starts; earlier months keep their own rate.</param>
public sealed record SetBillingRateCommand(string Item, BillingFlow? Flow, DateTime EffectiveFromMonth, decimal UnitPrice, decimal UnitCost, string? Note, Guid ActorUserId)
    : IRequest<ApplicationResult>;

public sealed class SetBillingRateHandler : IRequestHandler<SetBillingRateCommand, ApplicationResult>
{
    private readonly IBillingRateRepository _rates;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;

    public SetBillingRateHandler(IBillingRateRepository rates, IUserRepository users, IUnitOfWork uow)
    {
        _rates = rates;
        _users = users;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(SetBillingRateCommand request, CancellationToken ct = default)
    {
        var access = await BillingAccess.RequireManagerAsync(_users, request.ActorUserId, ct);
        if (!access.IsSuccess) return access;

        var item = BillingItems.Find(request.Item);
        if (item is null)
            return ApplicationResult.Failure("That is not a billable item.");
        if (item.PerFlow && (request.Flow is null || !BillingItems.LoanFlows.Contains(request.Flow.Value)))
            return ApplicationResult.Failure("A platform fee is set for one loan flow at a time.");
        if (!item.PerFlow && request.Flow is not null)
            return ApplicationResult.Failure("This item has one price for every flow.");

        var month = BillingUsage.MonthOf(DateTime.SpecifyKind(request.EffectiveFromMonth, DateTimeKind.Utc));
        var existing = await _rates.FindAsync(item.Item, request.Flow, month, ct);
        if (existing is not null)
        {
            var revised = existing.Revise(request.UnitPrice, request.UnitCost, request.Note, request.ActorUserId);
            if (revised.IsFailure) return ApplicationResult.Failure(revised.Error);
            await _rates.UpdateAsync(existing, ct);
        }
        else
        {
            var created = BillingRate.Create(item.Item, request.Flow, month, request.UnitPrice, request.UnitCost, request.Note, request.ActorUserId);
            if (created.IsFailure) return ApplicationResult.Failure(created.Error);
            await _rates.AddAsync(created.Value, ct);
        }
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

// ───────────────────────────── Access ─────────────────────────────

/// <summary>
/// Grants the Billing Manager role. An existing account keeps its other roles and gains this one; a
/// new account is created with an unguessable password and sent the usual password-reset email so
/// the person sets their own. Only a current Billing Manager may do this: the bank's administrators
/// cannot grant themselves a view of the vendor's costs.
/// </summary>
public sealed record AddBillingManagerCommand(string Email, string FirstName, string LastName, string ResetUrlBase, Guid ActorUserId) : IRequest<ApplicationResult<string>>;

public sealed class AddBillingManagerHandler : IRequestHandler<AddBillingManagerCommand, ApplicationResult<string>>
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IPasswordHasher _hasher;
    private readonly IAuthService _auth;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<AddBillingManagerHandler> _logger;

    public AddBillingManagerHandler(IUserRepository users, IRoleRepository roles, IPasswordHasher hasher, IAuthService auth, IUnitOfWork uow, ILogger<AddBillingManagerHandler> logger)
    {
        _users = users;
        _roles = roles;
        _hasher = hasher;
        _auth = auth;
        _uow = uow;
        _logger = logger;
    }

    public async Task<ApplicationResult<string>> Handle(AddBillingManagerCommand request, CancellationToken ct = default)
    {
        var access = await BillingAccess.RequireManagerAsync(_users, request.ActorUserId, ct);
        if (!access.IsSuccess) return ApplicationResult<string>.Failure(access.Error!);

        var email = request.Email.Trim().ToLowerInvariant();
        if (!System.Net.Mail.MailAddress.TryCreate(email, out var parsed) || parsed.Address != email || !email.Contains('.', StringComparison.Ordinal))
            return ApplicationResult<string>.Failure("Enter one valid email address.");

        var role = await _roles.GetByNameAsync(Roles.BillingManager, ct);
        if (role is null)
            return ApplicationResult<string>.Failure("The Billing Manager role has not been seeded on this server.");

        var user = await _users.GetByEmailAsync(email, ct);
        if (user is not null)
        {
            if (BillingAccess.IsManager(user))
                return ApplicationResult<string>.Failure($"{user.FullName} is already a Billing Manager.");
            user.AddRole(role);
            if (user.Status != UserStatus.Active)
                user.Activate();
            await _users.UpdateAsync(user, ct);
            await _uow.SaveChangesAsync(ct);
            return ApplicationResult<string>.Success($"{user.FullName} is now a Billing Manager and can sign in with their existing password.");
        }

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return ApplicationResult<string>.Failure("First and last name are needed for a new account.");

        var userName = await UniqueUserNameAsync(email, ct);
        var created = ApplicationUser.Create(email, userName, request.FirstName.Trim(), request.LastName.Trim(), UserType.Staff, null, null);
        if (created.IsFailure)
            return ApplicationResult<string>.Failure(created.Error);
        var account = created.Value;
        account.SetPasswordHash(_hasher.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));
        account.AddRole(role);
        await _users.AddAsync(account, ct);
        await _uow.SaveChangesAsync(ct);

        var reset = await _auth.RequestPasswordResetAsync(email, request.ResetUrlBase, ct);
        if (!reset.IsSuccess)
            _logger.LogWarning("Billing Manager {Email} created but the password email could not be queued: {Error}", email, reset.Error);
        return ApplicationResult<string>.Success(reset.IsSuccess
            ? $"{account.FullName} has been added. An email to set their password is on its way to {email}."
            : $"{account.FullName} has been added, but the password email could not be sent. They can use Forgot password on the login page.");
    }

    private async Task<string> UniqueUserNameAsync(string email, CancellationToken ct)
    {
        var stem = email.Split('@')[0];
        var candidate = stem;
        for (var n = 2; await _users.GetByUserNameAsync(candidate, ct) is not null; n++)
            candidate = $"{stem}{n}";
        return candidate;
    }
}

public sealed record RemoveBillingManagerCommand(Guid UserId, Guid ActorUserId) : IRequest<ApplicationResult<string>>;

public sealed class RemoveBillingManagerHandler : IRequestHandler<RemoveBillingManagerCommand, ApplicationResult<string>>
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IUnitOfWork _uow;

    public RemoveBillingManagerHandler(IUserRepository users, IRoleRepository roles, IUnitOfWork uow)
    {
        _users = users;
        _roles = roles;
        _uow = uow;
    }

    public async Task<ApplicationResult<string>> Handle(RemoveBillingManagerCommand request, CancellationToken ct = default)
    {
        var access = await BillingAccess.RequireManagerAsync(_users, request.ActorUserId, ct);
        if (!access.IsSuccess) return ApplicationResult<string>.Failure(access.Error!);

        var holders = await _users.GetByRoleAsync(Roles.BillingManager, ct);
        var target = holders.FirstOrDefault(u => u.Id == request.UserId);
        if (target is null)
            return ApplicationResult<string>.Failure("That person is not a Billing Manager.");
        if (holders.Count(u => u.Status == UserStatus.Active) <= 1 && target.Status == UserStatus.Active)
            return ApplicationResult<string>.Failure("There must always be at least one active Billing Manager. Add another before removing this one.");

        var role = await _roles.GetByNameAsync(Roles.BillingManager, ct);
        target.RemoveRole(role!.Id);
        var message = $"{target.FullName} is no longer a Billing Manager.";
        if (target.UserRoles.Count == 0)
        {
            target.Deactivate();
            message += " The account had no other role and has been deactivated.";
        }
        await _users.UpdateAsync(target, ct);
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult<string>.Success(message);
    }
}

// ───────────────────────────── Loan events ─────────────────────────────

public sealed record BillingSyncResultDto(int LoansSeen, int RowsAdded, DateTime RanAt);

/// <summary>
/// Reads every loan's current state and writes the platform-fee rows it has earned and not yet been
/// given: one "received" row per application, and one outcome row once it is booked, declined or
/// cancelled. Derived from state rather than hooked into each transition, so a missed hook can never
/// lose a fee, and re-running is harmless. The hourly background job and the Access page both call this.
/// </summary>
public sealed record SyncBillingLoanEventsCommand() : IRequest<ApplicationResult<BillingSyncResultDto>>;

public sealed class SyncBillingLoanEventsHandler : IRequestHandler<SyncBillingLoanEventsCommand, ApplicationResult<BillingSyncResultDto>>
{
    private readonly IBillingLoanSource _loans;
    private readonly IBillingUsageRepository _usage;
    private readonly IUnitOfWork _uow;

    public SyncBillingLoanEventsHandler(IBillingLoanSource loans, IBillingUsageRepository usage, IUnitOfWork uow)
    {
        _loans = loans;
        _usage = usage;
        _uow = uow;
    }

    public static string SourceKey(BillingFlow flow, Guid caseId, string item) => $"loan:{flow}:{caseId:N}:{item}";

    public async Task<ApplicationResult<BillingSyncResultDto>> Handle(SyncBillingLoanEventsCommand request, CancellationToken ct = default)
    {
        var loans = await _loans.AllLoansAsync(ct);
        var wanted = new List<(string Key, BillingLoanSnapshot Loan, string Item, DateTime At)>();
        foreach (var loan in loans)
        {
            wanted.Add((SourceKey(loan.Flow, loan.CaseId, BillingItems.LoanReceived), loan, BillingItems.LoanReceived, loan.ReceivedAt));
            if (BillingLoanClassifier.OutcomeItem(loan.Outcome) is { } item)
                wanted.Add((SourceKey(loan.Flow, loan.CaseId, item), loan, item, loan.OutcomeAt ?? loan.ReceivedAt));
        }

        var existing = await _usage.ExistingSourceKeysAsync(wanted.Select(w => w.Key), ct);
        var host = Environment.MachineName;
        var rows = new List<BillingUsage>();
        foreach (var w in wanted.Where(w => !existing.Contains(w.Key)))
        {
            var created = BillingUsage.Create(w.Item, w.Loan.Flow, w.Loan.CaseId, w.Loan.Reference, "Application", null, true, null, host, null,
                w.At, BillingUsageSource.Derived, w.Key, null);
            if (created.IsSuccess) rows.Add(created.Value);
        }
        if (rows.Count > 0)
        {
            await _usage.AddRangeAsync(rows, ct);
            await _uow.SaveChangesAsync(ct);
        }
        return ApplicationResult<BillingSyncResultDto>.Success(new BillingSyncResultDto(loans.Count, rows.Count, DateTime.UtcNow));
    }
}

// ───────────────────────────── Backfill ─────────────────────────────

/// <summary>Rebuilds third-party usage from records kept before billing existed. Implemented in Infrastructure.</summary>
public interface IBillingBackfillSource
{
    /// <param name="before">Only events earlier than this are reconstructed, so nothing overlaps with live rows.</param>
    Task<IReadOnlyList<BillingUsageDraft>> ReconstructAsync(DateTime before, CancellationToken ct = default);
}

public sealed record BillingBackfillItemDto(string Item, string DisplayName, int Count);

public sealed record BillingBackfillResultDto(bool DryRun, DateTime Before, int Candidates, int AlreadyPresent, int Added, IReadOnlyList<BillingBackfillItemDto> ByItem, IReadOnlyList<string> Notes);

public sealed record RunBillingBackfillCommand(bool DryRun, Guid ActorUserId) : IRequest<ApplicationResult<BillingBackfillResultDto>>;

public sealed class RunBillingBackfillHandler : IRequestHandler<RunBillingBackfillCommand, ApplicationResult<BillingBackfillResultDto>>
{
    private readonly IBillingBackfillSource _source;
    private readonly IBillingUsageRepository _usage;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;

    public RunBillingBackfillHandler(IBillingBackfillSource source, IBillingUsageRepository usage, IUserRepository users, IUnitOfWork uow)
    {
        _source = source;
        _usage = usage;
        _users = users;
        _uow = uow;
    }

    public async Task<ApplicationResult<BillingBackfillResultDto>> Handle(RunBillingBackfillCommand request, CancellationToken ct = default)
    {
        var access = await BillingAccess.RequireManagerAsync(_users, request.ActorUserId, ct);
        if (!access.IsSuccess) return ApplicationResult<BillingBackfillResultDto>.Failure(access.Error!);

        var before = await _usage.FirstLiveAtAsync(ct) ?? DateTime.UtcNow;
        var drafts = await _source.ReconstructAsync(before, ct);
        var keyed = drafts.Where(d => !string.IsNullOrEmpty(d.SourceKey)).ToList();
        var existing = await _usage.ExistingSourceKeysAsync(keyed.Select(d => d.SourceKey!), ct);
        var missing = keyed.Where(d => !existing.Contains(d.SourceKey!)).ToList();

        var host = Environment.MachineName;
        var rows = new List<BillingUsage>();
        foreach (var d in missing)
        {
            var scope = d.Scope ?? new BillingScope(BillingFlow.Unattributed, null, null);
            var created = BillingUsage.Create(d.Item, scope.Flow, scope.CaseId, scope.CaseReference, scope.SubjectType, d.SubjectHint,
                d.Succeeded, d.Detail, host, scope.TriggeredBy, d.OccurredAtUtc ?? before, BillingUsageSource.Reconstructed, d.SourceKey, d.DurationMs);
            if (created.IsSuccess) rows.Add(created.Value);
        }

        if (!request.DryRun && rows.Count > 0)
        {
            await _usage.AddRangeAsync(rows, ct);
            await _uow.SaveChangesAsync(ct);
        }

        var byItem = rows.GroupBy(r => r.Item)
            .Select(g => new BillingBackfillItemDto(g.Key, BillingItems.DisplayNameOf(g.Key), g.Count()))
            .OrderByDescending(x => x.Count).ToList();
        var notes = new List<string>
        {
            "Bureau reports, CAC lookups and partner checks were rebuilt from the records each of them left behind.",
            "BVN, NIN, TIN and fraud checks made before billing went live left no record and are not included.",
            "A bureau report that was retried counts once, since only the saved result is known.",
        };
        return ApplicationResult<BillingBackfillResultDto>.Success(new BillingBackfillResultDto(
            request.DryRun, before, keyed.Count, keyed.Count - missing.Count, request.DryRun ? 0 : rows.Count, byItem, notes));
    }
}
