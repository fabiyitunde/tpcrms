using CRMS.Application.Rhshf.Commands;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Application.Rhshf.Queries;
using CRMS.Domain.Aggregates.Committee;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

public class CommitteeVoteHandlerTests
{
    private static RhshfCreditProfile MakeProfileAtCommitteeVoting(out Guid creditOfficerId, out Guid riskOfficerId)
    {
        var result = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: "01234567-0001",
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Nassarawa", totalEopValue: 51_500_000.00m, currency: "NGN", farmerCount: 1200,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
            eopLines: null, resolvedBranchId: null, resolvedOfficeId: null);
        var profile = result.Value;

        foreach (var stage in new[]
        {
            RhshfProfilingStage.CompanyVerification, RhshfProfilingStage.CreditBureauCheck,
            RhshfProfilingStage.EopReview, RhshfProfilingStage.SupportingDocuments, RhshfProfilingStage.ReviewAndSubmit,
        })
        {
            profile.AdvanceStageForTest(stage);
        }

        creditOfficerId = Guid.NewGuid();
        riskOfficerId = Guid.NewGuid();
        profile.AppraiseWithFinancials(creditOfficerId, RhshfAppraisalOutcome.Proceed, null);
        profile.ReviewRisk(riskOfficerId, RhshfRiskReviewOutcome.Cleared, null);

        return profile;
    }

    [Fact]
    public async Task ReviewRhshfRisk_Cleared_AutoCirculatesToCommittee()
    {
        var result = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: "01234567-0001",
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Nassarawa", totalEopValue: 51_500_000.00m, currency: "NGN", farmerCount: 1200,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
            eopLines: null, resolvedBranchId: null, resolvedOfficeId: null);
        var profile = result.Value;
        foreach (var stage in new[]
        {
            RhshfProfilingStage.CompanyVerification, RhshfProfilingStage.CreditBureauCheck,
            RhshfProfilingStage.EopReview, RhshfProfilingStage.SupportingDocuments, RhshfProfilingStage.ReviewAndSubmit,
        })
        {
            profile.AdvanceStageForTest(stage);
        }
        profile.AppraiseWithFinancials(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);

        var profileRepo = new FakeProfileRepository(profile);
        var committeeRepo = new FakeCommitteeRepository();
        var handler = new ReviewRhshfRiskHandler(profileRepo, committeeRepo, new FakeRoutingConfigRepository(), new FakeStandingCommitteeRepository(), new FakeUnitOfWork());

        var result2 = await handler.Handle(new ReviewRhshfRiskCommand(profile.Reference, Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null, null));

        Assert.True(result2.IsSuccess);
        Assert.NotNull(committeeRepo.Added);
        Assert.Equal(1, committeeRepo.Added!.CycleNumber);
    }

    [Fact]
    public async Task CastVote_BelowQuorum_DoesNotAdvanceProfile()
    {
        var profile = MakeProfileAtCommitteeVoting(out _, out _);
        var review = RhshfCommitteeReview.Create(profile.Id, profile.CurrentCycleNumber, requiredVotes: 3, minimumApprovalVotes: 2, CommitteeType.BranchCredit, null).Value;
        var voterId = Guid.NewGuid();
        var handler = new CastRhshfCommitteeVoteHandler(new FakeProfileRepository(profile), new FakeCommitteeRepository(review), new FakeStandingCommitteeRepository(memberIds: voterId), new FakeUnitOfWork());

        var result = await handler.Handle(new CastRhshfCommitteeVoteCommand(profile.Reference, voterId, RhshfCommitteeVoteChoice.Approve, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.CommitteeVoting, profile.InternalStage);
    }

    [Fact]
    public async Task CastVote_QuorumApproved_AdvancesProfileToRatification()
    {
        var profile = MakeProfileAtCommitteeVoting(out _, out _);
        var review = RhshfCommitteeReview.Create(profile.Id, profile.CurrentCycleNumber, requiredVotes: 1, minimumApprovalVotes: 1, CommitteeType.BranchCredit, null).Value;
        var voterId = Guid.NewGuid();
        var handler = new CastRhshfCommitteeVoteHandler(new FakeProfileRepository(profile), new FakeCommitteeRepository(review), new FakeStandingCommitteeRepository(memberIds: voterId), new FakeUnitOfWork());

        var result = await handler.Handle(new CastRhshfCommitteeVoteCommand(profile.Reference, voterId, RhshfCommitteeVoteChoice.Approve, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.Ratification, profile.InternalStage);
    }

    [Fact]
    public async Task CastVote_QuorumRejected_DeclinesProfile()
    {
        var profile = MakeProfileAtCommitteeVoting(out _, out _);
        var review = RhshfCommitteeReview.Create(profile.Id, profile.CurrentCycleNumber, requiredVotes: 1, minimumApprovalVotes: 1, CommitteeType.BranchCredit, null).Value;
        var voterId = Guid.NewGuid();
        var handler = new CastRhshfCommitteeVoteHandler(new FakeProfileRepository(profile), new FakeCommitteeRepository(review), new FakeStandingCommitteeRepository(memberIds: voterId), new FakeUnitOfWork());

        var result = await handler.Handle(new CastRhshfCommitteeVoteCommand(profile.Reference, voterId, RhshfCommitteeVoteChoice.Reject, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCaseStatus.Declined, profile.Status);
        Assert.Equal("CRMS Committee", profile.DecidedBy);
    }

    [Fact]
    public async Task CastVote_ByCreditOfficerFromThisCycle_Fails()
    {
        var profile = MakeProfileAtCommitteeVoting(out var creditOfficerId, out _);
        var review = RhshfCommitteeReview.Create(profile.Id, profile.CurrentCycleNumber, requiredVotes: 3, minimumApprovalVotes: 2, CommitteeType.BranchCredit, null).Value;
        var handler = new CastRhshfCommitteeVoteHandler(new FakeProfileRepository(profile), new FakeCommitteeRepository(review), new FakeStandingCommitteeRepository(memberIds: creditOfficerId), new FakeUnitOfWork());

        var result = await handler.Handle(new CastRhshfCommitteeVoteCommand(profile.Reference, creditOfficerId, RhshfCommitteeVoteChoice.Approve, null));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ReturnToFac_ResetsBothAggregates()
    {
        var profile = MakeProfileAtCommitteeVoting(out _, out _);
        var review = RhshfCommitteeReview.Create(profile.Id, profile.CurrentCycleNumber, requiredVotes: 3, minimumApprovalVotes: 2, CommitteeType.BranchCredit, null).Value;
        var handler = new ReturnRhshfCommitteeToFacHandler(new FakeProfileRepository(profile), new FakeCommitteeRepository(review), new FakeUnitOfWork());

        var result = await handler.Handle(new ReturnRhshfCommitteeToFacCommand(profile.Reference, Guid.NewGuid(), "need more info", RhshfProfilingStage.SupportingDocuments));

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCaseStatus.ProfilingInProgress, profile.Status);
        Assert.Equal(RhshfCommitteeDecision.ReturnToFac, review.FinalDecision);
    }

    [Fact]
    public async Task GetCommitteeReview_ReturnsTallyAndVotes()
    {
        var profile = MakeProfileAtCommitteeVoting(out _, out _);
        var review = RhshfCommitteeReview.Create(profile.Id, profile.CurrentCycleNumber, requiredVotes: 3, minimumApprovalVotes: 2, CommitteeType.BranchCredit, null).Value;
        review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Approve, "looks good", []);
        var handler = new GetRhshfCommitteeReviewHandler(new FakeProfileRepository(profile), new FakeCommitteeRepository(review), new FakeUserNameResolver());

        var result = await handler.Handle(new GetRhshfCommitteeReviewQuery(profile.Reference));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Votes);
        Assert.Equal(3, result.Data.RequiredVotes);
    }

    private class FakeProfileRepository : IRhshfCreditProfileRepository
    {
        private readonly RhshfCreditProfile? _profile;
        public FakeProfileRepository(RhshfCreditProfile? profile) => _profile = profile;

        public Task<RhshfCreditProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_profile?.Id == id ? _profile : null);
        public Task<RhshfCreditProfile?> GetByReferenceAsync(string reference, CancellationToken ct = default)
            => Task.FromResult(_profile?.Reference == reference ? _profile : null);
        public Task<RhshfCreditProfile?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default)
            => Task.FromResult(_profile?.SubmissionId == submissionId ? _profile : null);
        public Task AddAsync(RhshfCreditProfile profile, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<RhshfCreditProfile>> GetAllForListAsync(Guid? branchId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>([]);
        public Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>(
                _profile is not null && _profile.InternalStage == stage ? [_profile] : []);
        public Task<RhshfSupportingDocument?> GetSupportingDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeCommitteeRepository : IRhshfCommitteeReviewRepository
    {
        private RhshfCommitteeReview? _review;
        public RhshfCommitteeReview? Added { get; private set; }
        public FakeCommitteeRepository(RhshfCommitteeReview? review = null) => _review = review;

        public Task<RhshfCommitteeReview?> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default)
            => Task.FromResult(_review is not null && _review.RhshfCreditProfileId == rhshfCreditProfileId && _review.CycleNumber == cycleNumber ? _review : null);

        public Task AddAsync(RhshfCommitteeReview review, CancellationToken ct = default)
        {
            _review = review;
            Added = review;
            return Task.CompletedTask;
        }
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

    private class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }
}
