using CRMS.Application.Rhshf.Commands;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Application.Rhshf.Queries;
using CRMS.Domain.Aggregates.Committee;
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
            profile.AdvanceStageForTest(stage);
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
        var creditOfficerId = Guid.NewGuid();
        profile.SeedViableAppraisal(creditOfficerId); // Proceed now requires a saved financial appraisal
        var repo = new FakeRepository(profile);
        var handler = new AppraiseRhshfCaseHandler(repo, new FakeUnitOfWork());

        var result = await handler.Handle(new AppraiseRhshfCaseCommand(profile.Reference, creditOfficerId, RhshfAppraisalOutcome.Proceed, "ok", null));

        Assert.True(result.IsSuccess);
        Assert.Single(profile.Appraisals);
    }

    [Fact]
    public async Task Appraise_Proceed_WithoutFinancialAppraisal_IsRejectedByTheHandler()
    {
        var profile = MakeProfileUnderReview();
        var handler = new AppraiseRhshfCaseHandler(new FakeRepository(profile), new FakeUnitOfWork());

        var result = await handler.Handle(new AppraiseRhshfCaseCommand(profile.Reference, Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, "ok", null));

        Assert.False(result.IsSuccess);
        Assert.Empty(profile.Appraisals);
    }

    [Fact]
    public async Task ReviewRisk_SameUserAsAppraiser_PropagatesDomainFailure()
    {
        var profile = MakeProfileUnderReview();
        var creditOfficerId = Guid.NewGuid();
        profile.AppraiseWithFinancials(creditOfficerId, RhshfAppraisalOutcome.Proceed, null);
        var repo = new FakeRepository(profile);
        var handler = new ReviewRhshfRiskHandler(repo, new FakeCommitteeRepository(), new FakeRoutingConfigRepository(), new FakeStandingCommitteeRepository(), new FakeUnitOfWork());

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
        var handler = new GetRhshfCaseWorkspaceHandler(repo, new FakeUserNameResolver(), new FakeRoutingConfigRepository());

        var result = await handler.Handle(new GetRhshfCaseWorkspaceQuery(profile.Reference));

        Assert.True(result.IsSuccess);
        Assert.Equal(profile.CompanyName, result.Data!.CompanyName);
        Assert.Equal(RhshfInternalStage.Appraisal, result.Data.InternalStage);
        Assert.Equal(1, result.Data.CurrentCycleNumber);
    }

    [Fact]
    public async Task GetCaseWorkspace_ResolvesActorNames_NotRawGuids()
    {
        var profile = MakeProfileUnderReview();
        var creditOfficerId = Guid.NewGuid();
        var riskOfficerId = Guid.NewGuid();
        profile.AppraiseWithFinancials(creditOfficerId, RhshfAppraisalOutcome.Proceed, null);
        profile.ReviewRisk(riskOfficerId, RhshfRiskReviewOutcome.Cleared, null);

        var resolver = new FakeUserNameResolver(new Dictionary<Guid, string>
        {
            [creditOfficerId] = "Chukwuemeka Obi",
            [riskOfficerId] = "Adaeze Nwosu",
        });
        var handler = new GetRhshfCaseWorkspaceHandler(new FakeRepository(profile), resolver, new FakeRoutingConfigRepository());

        var result = await handler.Handle(new GetRhshfCaseWorkspaceQuery(profile.Reference));

        Assert.True(result.IsSuccess);
        Assert.Equal("Chukwuemeka Obi", result.Data!.Appraisals.Single().CreditOfficerName);
        Assert.Equal("Adaeze Nwosu", result.Data.RiskReviews.Single().RiskOfficerName);
        // The raw id stays available for "is this me?" comparisons — it just isn't what renders.
        Assert.Equal(creditOfficerId, result.Data.Appraisals.Single().CreditOfficerId);
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
        public Task<RhshfSupportingDocument?> GetSupportingDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
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

    private class FakeRoutingConfigRepository : IRhshfRoutingConfigRepository
    {
        public Task<IReadOnlyList<RhshfRoutingConfig>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RhshfRoutingConfig>>([]);
        public Task<IReadOnlyList<RhshfRoutingConfig>> GetActiveConfigsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RhshfRoutingConfig>>([]);
        public Task<RhshfRoutingConfig?> ResolveAsync(decimal totalEopValue, CancellationToken ct = default)
            => Task.FromResult<RhshfRoutingConfig?>(RhshfRoutingConfig.Create(CommitteeType.BranchCredit, 0, 999_999_999_999m, 0).Value);
        public Task<RhshfRoutingConfig?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(RhshfRoutingConfig config, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(RhshfRoutingConfig config) { }
    }

    private class FakeStandingCommitteeRepository : IStandingCommitteeRepository
    {
        private readonly StandingCommittee _committee;
        public FakeStandingCommitteeRepository(int requiredVotes = 3, int minimumApprovalVotes = 2, params Guid[] memberIds)
        {
            _committee = StandingCommittee.Create("Branch Credit Committee", CommitteeType.BranchCredit, requiredVotes, minimumApprovalVotes, 48, 0, null).Value;
            foreach (var id in memberIds) _committee.AddMember(id, "Test Member", "Member", false);
        }
        public Task<StandingCommittee?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StandingCommittee?> GetByCommitteeTypeAsync(CommitteeType type, CancellationToken ct = default) => Task.FromResult<StandingCommittee?>(_committee);
        public Task<StandingCommittee?> GetByCommitteeTypeAndLocationAsync(CommitteeType type, Guid? locationId, CancellationToken ct = default) => Task.FromResult<StandingCommittee?>(_committee);
        public Task<StandingCommittee?> GetForAmountAsync(decimal amount, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<StandingCommittee>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(StandingCommittee committee, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(StandingCommittee committee) { }
    }
}
