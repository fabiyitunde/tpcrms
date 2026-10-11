using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

public class RhshfCommitteeReviewTests
{
    private static RhshfCommitteeReview CreateReview(int requiredVotes = 3, int minimumApprovalVotes = 2)
        => RhshfCommitteeReview.Create(Guid.NewGuid(), cycleNumber: 1, requiredVotes, minimumApprovalVotes, CommitteeType.BranchCredit, null).Value;

    [Fact]
    public void Create_WithInvalidThresholds_Fails()
    {
        Assert.True(RhshfCommitteeReview.Create(Guid.NewGuid(), 1, requiredVotes: 0, minimumApprovalVotes: 1, CommitteeType.BranchCredit, null).IsFailure);
        Assert.True(RhshfCommitteeReview.Create(Guid.NewGuid(), 1, requiredVotes: 3, minimumApprovalVotes: 4, CommitteeType.BranchCredit, null).IsFailure);
    }

    [Fact]
    public void CastVote_BelowQuorum_DoesNotFinalize()
    {
        var review = CreateReview();

        var result = review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Approve, null, []);

        Assert.True(result.IsSuccess);
        Assert.False(review.IsDecided);
        Assert.Null(review.FinalDecision);
    }

    [Fact]
    public void CastVote_QuorumReached_MajorityApprove_FinalizesApproved()
    {
        var review = CreateReview(requiredVotes: 3, minimumApprovalVotes: 2);

        review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Approve, null, []);
        review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Approve, null, []);
        var result = review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Reject, null, []);

        Assert.True(result.IsSuccess);
        Assert.True(review.IsDecided);
        Assert.Equal(RhshfCommitteeDecision.Approved, review.FinalDecision);
    }

    [Fact]
    public void CastVote_QuorumReached_MajorityNotMet_FinalizesRejected()
    {
        var review = CreateReview(requiredVotes: 3, minimumApprovalVotes: 2);

        review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Approve, null, []);
        review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Reject, null, []);
        review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Reject, null, []);

        Assert.True(review.IsDecided);
        Assert.Equal(RhshfCommitteeDecision.Rejected, review.FinalDecision);
    }

    [Fact]
    public void CastVote_ByExcludedActor_Fails()
    {
        var review = CreateReview();
        var creditOfficerId = Guid.NewGuid();

        var result = review.CastVote(creditOfficerId, RhshfCommitteeVoteChoice.Approve, null, [creditOfficerId]);

        Assert.True(result.IsFailure);
        Assert.Empty(review.Votes);
    }

    [Fact]
    public void CastVote_SameUserTwice_SecondCallFails()
    {
        var review = CreateReview();
        var userId = Guid.NewGuid();
        review.CastVote(userId, RhshfCommitteeVoteChoice.Approve, null, []);

        var result = review.CastVote(userId, RhshfCommitteeVoteChoice.Reject, null, []);

        Assert.True(result.IsFailure);
        Assert.Single(review.Votes);
    }

    [Fact]
    public void CastVote_AfterDecided_Fails()
    {
        var review = CreateReview(requiredVotes: 1, minimumApprovalVotes: 1);
        review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Approve, null, []); // reaches quorum immediately

        var result = review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Approve, null, []);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ReturnToFac_BeforeQuorum_Succeeds()
    {
        var review = CreateReview();
        review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Approve, null, []);

        var result = review.ReturnToFac("Need updated EOP figures");

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCommitteeDecision.ReturnToFac, review.FinalDecision);
    }

    [Fact]
    public void ReturnToFac_AfterDecided_Fails()
    {
        var review = CreateReview(requiredVotes: 1, minimumApprovalVotes: 1);
        review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Approve, null, []);

        var result = review.ReturnToFac("too late");

        Assert.True(result.IsFailure);
    }

    // ── Perfection disposition: what an approval means when the security isn't perfected ─────────

    [Fact]
    public void CastVote_Approve_WithUnperfectedSecurity_RequiresADisposition()
    {
        var review = CreateReview();

        var result = review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Approve, null, [], perfectionRequired: true);

        Assert.True(result.IsFailure);
        Assert.Empty(review.Votes);
    }

    [Fact]
    public void CastVote_Reject_NeedsNoDisposition_EvenWithUnperfectedSecurity()
    {
        var review = CreateReview();

        var result = review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Reject, "no", [], perfectionRequired: true);

        Assert.True(result.IsSuccess);
        Assert.Null(review.Votes.Single().PerfectionDisposition);
    }

    [Fact]
    public void Approval_WithEveryVoterWaiving_RecordsWaived()
    {
        var review = CreateReview(requiredVotes: 2, minimumApprovalVotes: 2);

        review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Approve, null, [], true, RhshfPerfectionDisposition.Waived);
        review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Approve, null, [], true, RhshfPerfectionDisposition.Waived);

        Assert.Equal(RhshfCommitteeDecision.Approved, review.FinalDecision);
        Assert.Equal(RhshfPerfectionDisposition.Waived, review.PerfectionDisposition);
    }

    [Fact]
    public void Approval_WithAnyVoterRequiringPerfectionFirst_RecordsDeferred()
    {
        var review = CreateReview(requiredVotes: 2, minimumApprovalVotes: 2);

        review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Approve, null, [], true, RhshfPerfectionDisposition.Waived);
        review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Approve, null, [], true, RhshfPerfectionDisposition.Deferred);

        // The stricter reading wins — one member insisting on perfection binds the case.
        Assert.Equal(RhshfCommitteeDecision.Approved, review.FinalDecision);
        Assert.Equal(RhshfPerfectionDisposition.Deferred, review.PerfectionDisposition);
    }

    [Fact]
    public void Approval_WithPerfectedSecurity_LeavesTheDispositionNull()
    {
        var review = CreateReview(requiredVotes: 2, minimumApprovalVotes: 2);

        review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Approve, null, []);
        review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Approve, null, []);

        // The question never arose, so nothing is recorded about it.
        Assert.Equal(RhshfCommitteeDecision.Approved, review.FinalDecision);
        Assert.Null(review.PerfectionDisposition);
    }

    [Fact]
    public void RejectedDecision_CarriesNoDisposition()
    {
        var review = CreateReview(requiredVotes: 2, minimumApprovalVotes: 2);

        review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Approve, null, [], true, RhshfPerfectionDisposition.Deferred);
        review.CastVote(Guid.NewGuid(), RhshfCommitteeVoteChoice.Reject, "security too weak", [], true);

        Assert.Equal(RhshfCommitteeDecision.Rejected, review.FinalDecision);
        Assert.Null(review.PerfectionDisposition);
    }
}
