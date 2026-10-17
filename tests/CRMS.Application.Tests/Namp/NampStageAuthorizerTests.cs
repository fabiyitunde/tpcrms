using CRMS.Application.Namp.Authorization;
using CRMS.Domain.Aggregates.Location;
using LocationEntity = CRMS.Domain.Aggregates.Location.Location;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Constants;
using CRMS.Domain.Entities.Identity;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Namp;

/// <summary>
/// Server-side authorisation for NAMP stage transitions.
///
/// Before this, the handlers called the domain method — which guards status and nothing else — so
/// anyone able to reach an endpoint could drive a case through any stage regardless of role or
/// branch. Hiding a button is not a permission, which is why these assertions matter more than the
/// UI gating they mirror.
///
/// Roles and location are read from the database by user id rather than taken from the caller: a
/// command carries only an id, and trusting a role passed alongside it would put the hole back.
/// </summary>
public class NampStageAuthorizerTests
{
    private static readonly Guid LagosBranch = Guid.NewGuid();
    private static readonly Guid KanoBranch = Guid.NewGuid();

    [Fact]
    public async Task TheTiersOwnRiskOfficerMayActOnIt()
    {
        var app = BranchTierApp();
        var auth = Authorizer(app, roles: [Roles.BranchRiskOfficer], locationId: LagosBranch);

        var result = await auth.AuthoriseAsync(UserId, NampStageAction.RiskReview, app);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AHeadOfficeRiskOfficerMayActOnAnyTier()
    {
        // The rule: head office is not tier-restricted. This is the behaviour the UI already shows;
        // without it here, the server would refuse what the button offers.
        var app = BranchTierApp();
        var auth = Authorizer(app, roles: [Roles.HORiskOfficer], locationId: null);

        var result = await auth.AuthoriseAsync(UserId, NampStageAction.RiskReview, app);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ANoLocationUserIsTreatedAsHeadOffice()
    {
        // "No location means head office" — so a Branch Risk Officer with no branch assigned is head
        // office and may act anywhere, which is what the queue already assumes.
        var app = BranchTierApp();
        var auth = Authorizer(app, roles: [Roles.BranchRiskOfficer], locationId: null);

        Assert.True((await auth.AuthoriseAsync(UserId, NampStageAction.RiskReview, app)).IsSuccess);
    }

    [Fact]
    public async Task AWrongTierRiskOfficerScopedToABranchIsRefused()
    {
        // A Zonal Risk Officer assigned to a branch is not head office and does not own a Branch-tier
        // case.
        var app = BranchTierApp();
        var auth = Authorizer(app, roles: [Roles.ZonalRiskOfficer], locationId: LagosBranch);

        var result = await auth.AuthoriseAsync(UserId, NampStageAction.RiskReview, app);

        Assert.True(result.IsFailure);
        Assert.Contains("BranchRiskOfficer", result.Error);
    }

    [Fact]
    public async Task ARiskOfficerAtAnotherBranchIsRefused()
    {
        // The hole the role check alone left open: right role, wrong branch.
        var app = BranchTierApp();
        var auth = Authorizer(app, roles: [Roles.BranchRiskOfficer], locationId: KanoBranch);

        var result = await auth.AuthoriseAsync(UserId, NampStageAction.RiskReview, app);

        Assert.True(result.IsFailure);
        Assert.Contains("outside your assigned location", result.Error);
    }

    [Fact]
    public async Task AZoneCoversTheBranchesBeneathIt()
    {
        var zoneId = Guid.NewGuid();
        var app = BranchTierApp();
        var auth = Authorizer(
            app, roles: [Roles.BranchRiskOfficer], locationId: zoneId,
            descendants: [LagosBranch, KanoBranch]);

        Assert.True((await auth.AuthoriseAsync(UserId, NampStageAction.RiskReview, app)).IsSuccess);
    }

    [Fact]
    public async Task AGenericRiskManagerMayActWithinTheirLocation()
    {
        // RiskManager carries no tier, so its holder is scoped by location alone. Assigned to the
        // application's own branch, they act on it whatever its tier.
        var app = BranchTierApp();
        var auth = Authorizer(app, roles: [Roles.RiskManager], locationId: LagosBranch);

        Assert.True((await auth.AuthoriseAsync(UserId, NampStageAction.RiskReview, app)).IsSuccess);
        Assert.True((await auth.AuthoriseAsync(UserId, NampStageAction.CirculateToCommittee, app)).IsSuccess);
    }

    [Fact]
    public async Task AGenericRiskManagerIsStillBoundByLocation()
    {
        // No tier restriction is not no restriction: a RiskManager at another branch is still out.
        var app = BranchTierApp();
        var auth = Authorizer(app, roles: [Roles.RiskManager], locationId: KanoBranch);

        var result = await auth.AuthoriseAsync(UserId, NampStageAction.RiskReview, app);

        Assert.True(result.IsFailure);
        Assert.Contains("outside your assigned location", result.Error);
    }

    [Fact]
    public async Task AGenericRiskManagerStillCannotRatify()
    {
        var app = BranchTierApp();
        var auth = Authorizer(app, roles: [Roles.RiskManager], locationId: null);

        Assert.True((await auth.AuthoriseAsync(UserId, NampStageAction.Ratification, app)).IsFailure);
    }

    [Fact]
    public async Task ACreditOfficerCannotTakeARiskDecision()
    {
        // The separation the stages exist for. Previously nothing stopped it server-side.
        var app = BranchTierApp();
        var auth = Authorizer(app, roles: [Roles.CreditOfficer], locationId: null);

        Assert.True((await auth.AuthoriseAsync(UserId, NampStageAction.RiskReview, app)).IsFailure);
    }

    [Fact]
    public async Task ARiskOfficerCannotTakeTheCreditDecision()
    {
        var app = BranchTierApp();
        var auth = Authorizer(app, roles: [Roles.BranchRiskOfficer], locationId: null);

        Assert.True((await auth.AuthoriseAsync(UserId, NampStageAction.FinancialAppraisal, app)).IsFailure);
    }

    [Fact]
    public async Task ARiskOfficerCannotRatifyOrReturnFromCommittee()
    {
        // Returning a circulating case unwinds votes, so it sits with the ratifier, not the officer
        // who sent it up.
        var app = BranchTierApp();
        var auth = Authorizer(app, roles: [Roles.HORiskOfficer], locationId: null);

        Assert.True((await auth.AuthoriseAsync(UserId, NampStageAction.ReturnFromCommittee, app)).IsFailure);
        Assert.True((await auth.AuthoriseAsync(UserId, NampStageAction.Ratification, app)).IsFailure);
    }

    [Fact]
    public async Task AHeadOfficeManagerMayReturnOrRatifyAnyTier()
    {
        var app = BranchTierApp();
        var auth = Authorizer(app, roles: [Roles.MdCeo], locationId: null);

        Assert.True((await auth.AuthoriseAsync(UserId, NampStageAction.ReturnFromCommittee, app)).IsSuccess);
        Assert.True((await auth.AuthoriseAsync(UserId, NampStageAction.Ratification, app)).IsSuccess);
    }

    [Fact]
    public async Task SystemAdminIsTheBreakGlassForEveryStage()
    {
        var app = BranchTierApp();
        var auth = Authorizer(app, roles: [Roles.SystemAdmin], locationId: KanoBranch);

        foreach (var action in Enum.GetValues<NampStageAction>())
            Assert.True((await auth.AuthoriseAsync(UserId, action, app)).IsSuccess, action.ToString());
    }

    [Fact]
    public async Task AUserWithNoRolesIsRefusedEverything()
    {
        var app = BranchTierApp();
        var auth = Authorizer(app, roles: [], locationId: null);

        foreach (var action in Enum.GetValues<NampStageAction>())
            Assert.True((await auth.AuthoriseAsync(UserId, action, app)).IsFailure, action.ToString());
    }

    [Fact]
    public async Task AnEmptyUserIdIsRefused()
    {
        // A command arriving without a signed-in user must not fall through to "no roles matched" —
        // it is a different failure and worth saying so.
        var app = BranchTierApp();
        var auth = Authorizer(app, roles: [Roles.SystemAdmin], locationId: null);

        var result = await auth.AuthoriseAsync(Guid.Empty, NampStageAction.RiskReview, app);

        Assert.True(result.IsFailure);
        Assert.Contains("signed-in user", result.Error);
    }

    [Fact]
    public async Task AnUnknownUserIsRefused()
    {
        var app = BranchTierApp();
        var auth = new NampStageAuthorizer(new FakeUsers(null), new FakeRoles([]), new FakeLocations([]));

        var result = await auth.AuthoriseAsync(UserId, NampStageAction.RiskReview, app);

        Assert.True(result.IsFailure);
        Assert.Contains("could not be found", result.Error);
    }

    // ── Fixtures ──────────────────────────────────────────────────────────────────────────────────

    private static readonly Guid UserId = Guid.NewGuid();

    private static NampApplication BranchTierApp() => NampApplication.Create(
        applicationNumber: "NAMP-2026-AUTH0001",
        loanProductId: Guid.NewGuid(),
        applicationReference: "PORTAL-AUTH-1",
        applicantName: "Musa Danjuma",
        boaAccountNumber: "0123456789",
        applicantCategory: NampApplicantCategory.YouthAgripreneur,
        equipmentDescription: "Tractor",
        equipmentValue: 12_000_000m,
        branchId: LagosBranch,
        committeeTier: NampCommitteeTier.Branch,
        recalledByUserId: Guid.NewGuid()).Value;

    private static NampStageAuthorizer Authorizer(
        NampApplication app, string[] roles, Guid? locationId, Guid[]? descendants = null)
    {
        var user = ApplicationUser.Create(
            email: "probe@crms.test", userName: "probe", firstName: "Probe", lastName: "User",
            type: UserType.Staff, phoneNumber: null, locationId: locationId).Value;

        return new NampStageAuthorizer(
            new FakeUsers(user), new FakeRoles(roles), new FakeLocations(descendants ?? []));
    }

    private class FakeUsers : IUserRepository
    {
        private readonly ApplicationUser? _user;
        public FakeUsers(ApplicationUser? user) => _user = user;

        public Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_user);
        public Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApplicationUser?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApplicationUser?> GetByUserNameAsync(string userName, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ApplicationUser>> GetAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ApplicationUser>> GetByRoleAsync(string roleName, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(string email, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(ApplicationUser user, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(ApplicationUser user, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeRoles : IRoleRepository
    {
        private readonly string[] _roles;
        public FakeRoles(string[] roles) => _roles = roles;

        public Task<IReadOnlyList<ApplicationRole>> GetUserRolesAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ApplicationRole>>(
                _roles.Select(r => ApplicationRole.Create(r, r, RoleType.System)).ToList());

        public Task<ApplicationRole?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApplicationRole?> GetByNameAsync(string name, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ApplicationRole>> GetAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(ApplicationRole role, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(ApplicationRole role, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeLocations : ILocationRepository
    {
        private readonly Guid[] _descendants;
        public FakeLocations(Guid[] descendants) => _descendants = descendants;

        public Task<IReadOnlyList<Guid>> GetDescendantBranchIdsAsync(Guid locationId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Guid>>(_descendants);

        public Task<LocationEntity?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<LocationEntity?> GetByIdWithChildrenAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<LocationEntity?> GetByCodeAsync(string code, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LocationEntity>> GetByTypeAsync(LocationType type, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LocationEntity>> GetChildrenAsync(Guid parentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LocationEntity>> GetAllActiveAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LocationEntity>> GetAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetAncestorIdsAsync(Guid locationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<LocationEntity?> GetHierarchyTreeAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<LocationEntity?> GetBranchByNameAsync(string name, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(LocationEntity location, CancellationToken ct = default) => throw new NotSupportedException();
        public void Update(LocationEntity location) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
