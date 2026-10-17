using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Common;
using CRMS.Domain.Constants;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Namp.Authorization;

/// <summary>A NAMP stage transition, for authorisation purposes.</summary>
public enum NampStageAction
{
    /// <summary>Loan Officer submits a draft for appraisal.</summary>
    Submit,
    /// <summary>Credit Officer approves or declines the financial appraisal, or returns it.</summary>
    FinancialAppraisal,
    /// <summary>Risk Officer approves to committee, returns to appraisal, or declines.</summary>
    RiskReview,
    /// <summary>Creating the committee review and circulating it.</summary>
    CirculateToCommittee,
    /// <summary>Sending a circulating case back for rework, which supersedes votes already cast.</summary>
    ReturnFromCommittee,
    /// <summary>Ratifying or declining to ratify the committee's decision.</summary>
    Ratification,
    /// <summary>Recording the applicant's countersignature, or lapsing an unaccepted offer.</summary>
    Offer,
    /// <summary>Legal clearance: granting, returning, resubmitting or declining.</summary>
    LegalClearance,
    /// <summary>Pre-deployment verification and confirming deployment.</summary>
    Deployment,
    /// <summary>Booking the loan in core banking, including a retry.</summary>
    Booking,
    /// <summary>Closing a finished application.</summary>
    Close,
}

/// <summary>
/// Server-side authorisation for NAMP stage transitions.
/// <para>
/// Until this existed the UI was the only gate: the handlers called the domain method, which guards
/// status and nothing else, so anyone who could reach an endpoint could drive a case through any
/// stage regardless of role or branch. Hiding a button is not a permission.
/// </para>
/// <para>
/// Roles and location are read from the database by user id, not taken from the caller. A command
/// carries only the id, and trusting a role claim passed alongside it would reintroduce the same
/// hole one layer down.
/// </para>
/// </summary>
public interface INampStageAuthorizer
{
    /// <summary>
    /// Whether this user may take this action on this application. Failure carries a message naming
    /// what was required, since the usual reader is an officer who expected the action to work.
    /// </summary>
    Task<Result> AuthoriseAsync(
        Guid userId, NampStageAction action, NampApplication application, CancellationToken ct = default);
}

public class NampStageAuthorizer : INampStageAuthorizer
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly ILocationRepository _locations;

    public NampStageAuthorizer(IUserRepository users, IRoleRepository roles, ILocationRepository locations)
    {
        _users = users;
        _roles = roles;
        _locations = locations;
    }

    public async Task<Result> AuthoriseAsync(
        Guid userId, NampStageAction action, NampApplication application, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return Result.Failure("This action requires a signed-in user.");

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
            return Result.Failure("The acting user could not be found.");

        var roleNames = (await _roles.GetUserRolesAsync(userId, ct))
            .Select(r => r.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // A system administrator is the break-glass for every stage. Deliberate: someone has to be
        // able to unstick a case, and the action is recorded against their name either way.
        if (roleNames.Contains(Roles.SystemAdmin))
            return Result.Success();

        // The bank's rule: a user is scoped to a branch or a zone only once one is assigned, and
        // until then they are head office with no tier or branch restriction.
        var isHeadOffice = user.LocationId is null
            || string.Equals(user.Location?.Type.ToString(), "HeadOffice", StringComparison.OrdinalIgnoreCase);

        var scopeCheck = await CheckBranchScopeAsync(user, application, isHeadOffice, ct);
        if (scopeCheck.IsFailure)
            return scopeCheck;

        var allowed = AllowedRoles(action, application.CommitteeTier, isHeadOffice);
        if (allowed.Any(roleNames.Contains))
            return Result.Success();

        return Result.Failure(
            $"You do not have permission to take this action on a {application.CommitteeTier}-tier application. "
            + $"It requires one of: {string.Join(", ", allowed)}.");
    }

    /// <summary>
    /// Whether the user's location covers the application's branch. Head office covers everything; a
    /// branch covers its own applications; a zone or region covers the branches beneath it.
    /// </summary>
    private async Task<Result> CheckBranchScopeAsync(
        Domain.Entities.Identity.ApplicationUser user,
        NampApplication application,
        bool isHeadOffice,
        CancellationToken ct)
    {
        if (isHeadOffice || user.LocationId is null)
            return Result.Success();

        if (user.LocationId.Value == application.BranchId)
            return Result.Success();

        var descendants = await _locations.GetDescendantBranchIdsAsync(user.LocationId.Value, ct);
        if (descendants.Contains(application.BranchId))
            return Result.Success();

        return Result.Failure("This application belongs to a branch outside your assigned location.");
    }

    /// <summary>
    /// The roles that may take an action. Tier-specific stages name the tier's own role, plus every
    /// higher-tier equivalent for a head-office user — head office is not tier-restricted, so a Head
    /// Office Risk Officer acts on a Branch-tier case.
    /// </summary>
    private static string[] AllowedRoles(NampStageAction action, NampCommitteeTier tier, bool isHeadOffice) =>
        action switch
        {
            NampStageAction.Submit => [Roles.LoanOfficer],
            NampStageAction.FinancialAppraisal => [Roles.CreditOfficer],
            NampStageAction.Offer => [Roles.LoanOfficer],
            NampStageAction.LegalClearance => [Roles.LegalOfficer, Roles.HeadOfLegal],
            NampStageAction.Deployment => [Roles.DeploymentOfficer],
            NampStageAction.Booking => [Roles.DisbursementOfficer, Roles.GMFinance],
            NampStageAction.Close => [Roles.LoanOfficer, Roles.DisbursementOfficer],

            NampStageAction.RiskReview or NampStageAction.CirculateToCommittee =>
                isHeadOffice ? AllRiskOfficerRoles : [TierRiskRole(tier)],

            NampStageAction.ReturnFromCommittee or NampStageAction.Ratification =>
                isHeadOffice ? AllRatifierRoles : [TierRatifierRole(tier)],

            _ => [],
        };

    private static readonly string[] AllRiskOfficerRoles =
        [Roles.HORiskOfficer, Roles.RegionalRiskOfficer, Roles.ZonalRiskOfficer, Roles.BranchRiskOfficer];

    private static readonly string[] AllRatifierRoles =
        [Roles.MdCeo, Roles.RegionalManager, Roles.ZonalManager, Roles.BranchManager];

    private static string TierRiskRole(NampCommitteeTier tier) => tier switch
    {
        NampCommitteeTier.Branch => Roles.BranchRiskOfficer,
        NampCommitteeTier.Zonal => Roles.ZonalRiskOfficer,
        NampCommitteeTier.Regional => Roles.RegionalRiskOfficer,
        _ => Roles.HORiskOfficer,
    };

    private static string TierRatifierRole(NampCommitteeTier tier) => tier switch
    {
        NampCommitteeTier.Branch => Roles.BranchManager,
        NampCommitteeTier.Zonal => Roles.ZonalManager,
        NampCommitteeTier.Regional => Roles.RegionalManager,
        _ => Roles.MdCeo,
    };
}
