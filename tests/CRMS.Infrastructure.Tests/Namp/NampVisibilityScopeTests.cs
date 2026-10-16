using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories.Namp;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRMS.Infrastructure.Tests.Namp;

/// <summary>
/// The bank's NAMP visibility rule, at the query that enforces it.
///
/// Head office sees every tier and every branch. A branch sees its own applications. A zone or
/// region sees the branches beneath it, which the caller resolves to a branch-id list before getting
/// here. The rule that broke was tier-filtering head office: a Head Office Risk Officer asked only
/// for HeadOffice-tier cases and so saw nothing at all, leaving a Branch-tier application invisible
/// to everyone senior to the branch that owned it.
/// </summary>
public class NampVisibilityScopeTests
{
    private static readonly Guid Lagos = Guid.NewGuid();
    private static readonly Guid Kano = Guid.NewGuid();

    [Fact]
    public async Task HeadOffice_SeesEveryTierAndEveryBranch()
    {
        // No tier and no branch list — what a head-office user resolves to.
        var repo = await SeededRepo();

        var found = await repo.GetByStatusScopedAsync(NampApplicationStatus.RiskReview, tier: null, branchIds: null);

        Assert.Equal(3, found.Count);
    }

    [Fact]
    public async Task ABranchSeesOnlyItsOwn()
    {
        var repo = await SeededRepo();

        var found = await repo.GetByStatusScopedAsync(NampApplicationStatus.RiskReview, tier: null, branchIds: [Lagos]);

        Assert.Equal(2, found.Count);
        Assert.All(found, a => Assert.Equal(Lagos, a.BranchId));
    }

    [Fact]
    public async Task AZoneSeesEveryBranchBeneathIt()
    {
        // A zone resolves to its branch ids, so one list covers branch, zone and region scoping.
        var repo = await SeededRepo();

        var found = await repo.GetByStatusScopedAsync(
            NampApplicationStatus.RiskReview, tier: null, branchIds: [Lagos, Kano]);

        Assert.Equal(3, found.Count);
    }

    [Fact]
    public async Task AnEmptyBranchListMeansNoRestriction_NotNothing()
    {
        // The trap this guards: treating "no branches resolved" as "match nothing" would blank the
        // queue for every head-office user.
        var repo = await SeededRepo();

        var found = await repo.GetByStatusScopedAsync(
            NampApplicationStatus.RiskReview, tier: null, branchIds: []);

        Assert.Equal(3, found.Count);
    }

    [Fact]
    public async Task ATierFilterStillAppliesWhenOneIsGiven()
    {
        // Non-head-office actors stay scoped to their own tier.
        var repo = await SeededRepo();

        var branchTier = await repo.GetByStatusScopedAsync(
            NampApplicationStatus.RiskReview, NampCommitteeTier.Branch, branchIds: null);
        var hoTier = await repo.GetByStatusScopedAsync(
            NampApplicationStatus.RiskReview, NampCommitteeTier.HeadOffice, branchIds: null);

        Assert.Equal(2, branchTier.Count);
        Assert.Single(hoTier);
    }

    [Fact]
    public async Task TierAndBranchCombine()
    {
        var repo = await SeededRepo();

        var found = await repo.GetByStatusScopedAsync(
            NampApplicationStatus.RiskReview, NampCommitteeTier.Branch, branchIds: [Kano]);

        Assert.Empty(found); // the Kano case is HeadOffice tier
    }

    [Fact]
    public async Task StatusIsAlwaysHonoured()
    {
        var repo = await SeededRepo();

        var found = await repo.GetByStatusScopedAsync(NampApplicationStatus.Draft, tier: null, branchIds: null);

        Assert.Empty(found);
    }

    private static async Task<NampApplicationRepository> SeededRepo()
    {
        var options = new DbContextOptionsBuilder<CRMSDbContext>()
            .UseInMemoryDatabase("NampScope_" + Guid.NewGuid())
            .Options;
        var ctx = new CRMSDbContext(options);

        ctx.NampApplications.AddRange(
            AtRiskReview("NAMP-A", Lagos, NampCommitteeTier.Branch),
            AtRiskReview("NAMP-B", Lagos, NampCommitteeTier.Branch),
            AtRiskReview("NAMP-C", Kano, NampCommitteeTier.HeadOffice));
        await ctx.SaveChangesAsync();

        return new NampApplicationRepository(ctx);
    }

    private static NampApplication AtRiskReview(string number, Guid branchId, NampCommitteeTier tier)
    {
        var app = NampApplication.Create(
            applicationNumber: number,
            loanProductId: Guid.NewGuid(),
            applicationReference: number + "-REF",
            applicantName: "Scope Probe",
            boaAccountNumber: "0123456789",
            applicantCategory: NampApplicantCategory.YouthAgripreneur,
            equipmentDescription: "Tractor",
            equipmentValue: 1_000_000m,
            branchId: branchId,
            committeeTier: tier,
            recalledByUserId: Guid.NewGuid()).Value;

        app.Submit(Guid.NewGuid());
        app.SubmitFinancialAppraisal(Guid.NewGuid(), isApproved: true, "Appraised.");
        return app;
    }
}
