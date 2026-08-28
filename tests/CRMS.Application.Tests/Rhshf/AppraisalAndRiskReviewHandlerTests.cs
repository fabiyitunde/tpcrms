using CRMS.Application.Rhshf.Commands;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Application.Rhshf.Queries;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

public class AppraisalAndRiskReviewHandlerTests
{
    private static RhshfCreditProfile MakeProfileUnderReview(Guid? resolvedBranchId = null)
    {
        var result = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: "01234567-0001",
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Nassarawa", totalEopValue: 51_500_000.00m, currency: "NGN", farmerCount: 1200,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
            eopLines: null, resolvedBranchId: resolvedBranchId, resolvedOfficeId: null);
        var profile = result.Value;

        foreach (var stage in new[]
        {
            RhshfProfilingStage.CompanyVerification, RhshfProfilingStage.CreditBureauCheck,
            RhshfProfilingStage.EopReview, RhshfProfilingStage.SupportingDocuments, RhshfProfilingStage.ReviewAndSubmit,
        })
        {
            profile.AdvanceStage(stage);
        }

        return profile;
    }

    [Fact]
    public async Task Appraise_UnknownReference_Fails()
    {
        var handler = new AppraiseRhshfCaseHandler(new FakeRepository(null), new FakeUnitOfWork());

        var result = await handler.Handle(new AppraiseRhshfCaseCommand("RHSHF-2026-000000", Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null, null));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Appraise_ValidCase_Succeeds_AndPersists()
    {
        var profile = MakeProfileUnderReview();
        var repo = new FakeRepository(profile);
        var handler = new AppraiseRhshfCaseHandler(repo, new FakeUnitOfWork());

        var result = await handler.Handle(new AppraiseRhshfCaseCommand(profile.Reference, Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, "ok", null));

        Assert.True(result.IsSuccess);
        Assert.Single(profile.Appraisals);
    }

    [Fact]
    public async Task ReviewRisk_SameUserAsAppraiser_PropagatesDomainFailure()
    {
        var profile = MakeProfileUnderReview();
        var creditOfficerId = Guid.NewGuid();
        profile.Appraise(creditOfficerId, RhshfAppraisalOutcome.Proceed, null);
        var repo = new FakeRepository(profile);
        var handler = new ReviewRhshfRiskHandler(repo, new FakeCommitteeRepository(), new FakeCommitteeConfig(), new FakeUnitOfWork());

        var result = await handler.Handle(new ReviewRhshfRiskCommand(profile.Reference, creditOfficerId, RhshfRiskReviewOutcome.Cleared, null, null));

        Assert.False(result.IsSuccess);
        Assert.Empty(profile.RiskReviews);
    }

    [Fact]
    public async Task GetStaffQueue_FiltersByStageAndBranch()
    {
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var caseInBranchA = MakeProfileUnderReview(branchA);
        var caseInBranchB = MakeProfileUnderReview(branchB);
        var repo = new FakeRepository(null) { All = [caseInBranchA, caseInBranchB] };
        var handler = new GetRhshfStaffQueueHandler(repo);

        var result = await handler.Handle(new GetRhshfStaffQueueQuery(RhshfInternalStage.Appraisal, branchA));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal(caseInBranchA.Reference, result.Data!.Single().Reference);
    }

    [Fact]
    public async Task GetCaseWorkspace_ReturnsCompanyAndPipelineData()
    {
        var profile = MakeProfileUnderReview();
        var repo = new FakeRepository(profile);
        var handler = new GetRhshfCaseWorkspaceHandler(repo);

        var result = await handler.Handle(new GetRhshfCaseWorkspaceQuery(profile.Reference));

        Assert.True(result.IsSuccess);
        Assert.Equal(profile.CompanyName, result.Data!.CompanyName);
        Assert.Equal(RhshfInternalStage.Appraisal, result.Data.InternalStage);
        Assert.Equal(1, result.Data.CurrentCycleNumber);
    }

    private class FakeRepository : IRhshfCreditProfileRepository
    {
        private readonly RhshfCreditProfile? _profile;
        public FakeRepository(RhshfCreditProfile? profile) => _profile = profile;
        public List<RhshfCreditProfile> All { get; set; } = [];

        public Task<RhshfCreditProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_profile?.Id == id ? _profile : null);

        public Task<RhshfCreditProfile?> GetByReferenceAsync(string reference, CancellationToken ct = default)
            => Task.FromResult(_profile?.Reference == reference ? _profile : null);

        public Task<RhshfCreditProfile?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default)
            => Task.FromResult(_profile?.SubmissionId == submissionId ? _profile : null);

        public Task AddAsync(RhshfCreditProfile profile, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>(
                All.Where(x => x.InternalStage == stage && (branchId == null || x.ResolvedBranchId == branchId)).ToList());
    }

    private class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }

    private class FakeCommitteeRepository : IRhshfCommitteeReviewRepository
    {
        public Task<RhshfCommitteeReview?> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default)
            => Task.FromResult<RhshfCommitteeReview?>(null);
        public Task AddAsync(RhshfCommitteeReview review, CancellationToken ct = default) => Task.CompletedTask;
    }

    private class FakeCommitteeConfig : IRhshfCommitteeConfig
    {
        public int RequiredVotes => 3;
        public int MinimumApprovalVotes => 2;
    }
}
