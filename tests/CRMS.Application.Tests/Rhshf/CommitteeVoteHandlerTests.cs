using CRMS.Application.Rhshf.Commands;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Application.Rhshf.Queries;
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
            profile.AdvanceStage(stage);
        }

        creditOfficerId = Guid.NewGuid();
        riskOfficerId = Guid.NewGuid();
        profile.Appraise(creditOfficerId, RhshfAppraisalOutcome.Proceed, null);
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
            profile.AdvanceStage(stage);
        }
        profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);

        var profileRepo = new FakeProfileRepository(profile);
        var committeeRepo = new FakeCommitteeRepository();
        var handler = new ReviewRhshfRiskHandler(profileRepo, committeeRepo, new FakeCommitteeConfig(), new FakeUnitOfWork());

        var result2 = await handler.Handle(new ReviewRhshfRiskCommand(profile.Reference, Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null, null));

        Assert.True(result2.IsSuccess);
        Assert.NotNull(committeeRepo.Added);
        Assert.Equal(1, committeeRepo.Added!.CycleNumber);
    }

    [Fact]
    public async Task CastVote_BelowQuorum_DoesNotAdvanceProfile()
    {
        var profile = MakeProfileAtCommitteeVoting(out _, out _);
        var review = RhshfCommitteeReview.Create(profile.Id, profile.CurrentCycleNumber, requiredVotes: 3, minimumApprovalVotes: 2).Value;
        var handler = new CastRhshfCommitteeVoteHandler(new FakeProfileRepository(profile), new FakeCommitteeRepository(review), new FakeUnitOfWork());

        var result = await handler.Handle(new CastRhshfCommitteeVoteCommand(profile.Reference, Guid.NewGuid(), RhshfCommitteeVoteChoice.Approve, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.CommitteeVoting, profile.InternalStage);
    }

    [Fact]
    public async Task CastVote_QuorumApproved_AdvancesProfileToRatification()
    {
        var profile = MakeProfileAtCommitteeVoting(out _, out _);
        var review = RhshfCommitteeReview.Create(profile.Id, profile.CurrentCycleNumber, requiredVotes: 1, minimumApprovalVotes: 1).Value;
        var handler = new CastRhshfCommitteeVoteHandler(new FakeProfileRepository(profile), new FakeCommitteeRepository(review), new FakeUnitOfWork());

        var result = await handler.Handle(new CastRhshfCommitteeVoteCommand(profile.Reference, Guid.NewGuid(), RhshfCommitteeVoteChoice.Approve, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.Ratification, profile.InternalStage);
    }

    [Fact]
    public async Task CastVote_QuorumRejected_DeclinesProfile()
    {
        var profile = MakeProfileAtCommitteeVoting(out _, out _);
        var review = RhshfCommitteeReview.Create(profile.Id, profile.CurrentCycleNumber, requiredVotes: 1, minimumApprovalVotes: 1).Value;
        var handler = new CastRhshfCommitteeVoteHandler(new FakeProfileRepository(profile), new FakeCommitteeRepository(review), new FakeUnitOfWork());

        var result = await handler.Handle(new CastRhshfCommitteeVoteCommand(profile.Reference, Guid.NewGuid(), RhshfCommitteeVoteChoice.Reject, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCaseStatus.Declined, profile.Status);
        Assert.Equal("CRMS Committee", profile.DecidedBy);
    }

    [Fact]
    public async Task CastVote_ByCreditOfficerFromThisCycle_Fails()
    {
        var profile = MakeProfileAtCommitteeVoting(out var creditOfficerId, out _);
        var review = RhshfCommitteeReview.Create(profile.Id, profile.CurrentCycleNumber, requiredVotes: 3, minimumApprovalVotes: 2).Value;
        var handler = new CastRhshfCommitteeVoteHandler(new FakeProfileRepository(profile), new FakeCommitteeRepository(review), new FakeUnitOfWork());

        var result = await handler.Handle(new CastRhshfCommitteeVoteCommand(profile.Reference, creditOfficerId, RhshfCommitteeVoteChoice.Approve, null));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ReturnToFac_ResetsBothAggregates()
    {
        var profile = MakeProfileAtCommitteeVoting(out _, out _);
        var review = RhshfCommitteeReview.Create(profile.Id, profile.CurrentCycleNumber, requiredVotes: 3, minimumApprovalVotes: 2).Value;
        var handler = new ReturnRhshfCommitteeToFacHandler(new FakeProfileRepository(profile), new FakeCommitteeRepository(review), new FakeUnitOfWork());

        var result = await handler.Handle(new ReturnRhshfCommitteeToFacCommand(profile.Reference, "need more info", RhshfProfilingStage.SupportingDocuments));

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCaseStatus.ProfilingInProgress, profile.Status);
        Assert.Equal(RhshfCommitteeDecision.ReturnToFac, review.FinalDecision);
    }

    [Fact]
    public async Task GetCommitteeReview_ReturnsTallyAndVotes()
    {
        var profile = MakeProfileAtCommitteeVoting(out _, out _);
        var review = RhshfCommitteeReview.Create(profile.Id, profile.CurrentCycleNumber, requiredVotes: 3, minimumApprovalVotes: 2).Value;
        review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Approve, "looks good", []);
        var handler = new GetRhshfCommitteeReviewHandler(new FakeProfileRepository(profile), new FakeCommitteeRepository(review));

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
        public Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>(
                _profile is not null && _profile.InternalStage == stage ? [_profile] : []);
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

    private class FakeCommitteeConfig : IRhshfCommitteeConfig
    {
        public int RequiredVotes => 3;
        public int MinimumApprovalVotes => 2;
    }

    private class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }
}
