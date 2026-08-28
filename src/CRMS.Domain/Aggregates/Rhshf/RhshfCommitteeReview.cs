using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// Committee voting stage (design doc §3.6, Phase 5) — its own aggregate root, own table, own
/// repository. Deliberately NOT the generic CommitteeReview (hard, non-nullable FK to Corporate's
/// LoanApplicationId — confirmed by reading the class, not reusable as-is) and NOT NampCommitteeReview.
/// A single flat committee for v1 (design doc §6 #11) — no value-based tiers.
/// </summary>
public class RhshfCommitteeReview : AggregateRoot
{
    public Guid RhshfCreditProfileId { get; private set; }
    public int CycleNumber { get; private set; }
    public int RequiredVotes { get; private set; }
    public int MinimumApprovalVotes { get; private set; }
    public RhshfCommitteeDecision? FinalDecision { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? Notes { get; private set; }

    public bool IsDecided => FinalDecision is not null;

    private readonly List<RhshfCommitteeVote> _votes = [];
    public IReadOnlyCollection<RhshfCommitteeVote> Votes => _votes.AsReadOnly();

    protected RhshfCommitteeReview() { }

    public static Result<RhshfCommitteeReview> Create(Guid rhshfCreditProfileId, int cycleNumber, int requiredVotes, int minimumApprovalVotes)
    {
        if (requiredVotes <= 0)
            return Result.Failure<RhshfCommitteeReview>("requiredVotes must be greater than zero.");
        if (minimumApprovalVotes <= 0 || minimumApprovalVotes > requiredVotes)
            return Result.Failure<RhshfCommitteeReview>("minimumApprovalVotes must be positive and not exceed requiredVotes.");

        return Result.Success(new RhshfCommitteeReview
        {
            RhshfCreditProfileId = rhshfCreditProfileId,
            CycleNumber = cycleNumber,
            RequiredVotes = requiredVotes,
            MinimumApprovalVotes = minimumApprovalVotes,
        });
    }

    /// <summary>Records one member's vote; auto-finalizes to Approved/Rejected the moment quorum
    /// (RequiredVotes) is reached — no separate "close voting" action needed for v1.
    /// excludedActorIds is that cycle's Credit Officer + Risk Officer (design doc Phase 5 §4):
    /// committee membership must be distinct from the actors who already appraised/risk-reviewed
    /// this cycle.</summary>
    public Result CastVote(Guid userId, RhshfCommitteeVoteChoice vote, string? comment, IReadOnlyCollection<Guid> excludedActorIds)
    {
        if (IsDecided)
            return Result.Failure("This committee review has already reached a decision.");
        if (excludedActorIds.Contains(userId))
            return Result.Failure("This user appraised or risk-reviewed this cycle and cannot also vote on committee.");
        if (_votes.Any(v => v.UserId == userId))
            return Result.Failure("This user has already voted on this review.");

        _votes.Add(new RhshfCommitteeVote(Id, userId, vote, comment));

        if (_votes.Count >= RequiredVotes)
        {
            var approvals = _votes.Count(v => v.Vote == RhshfCommitteeVoteChoice.Approve);
            FinalDecision = approvals >= MinimumApprovalVotes ? RhshfCommitteeDecision.Approved : RhshfCommitteeDecision.Rejected;
            DecidedAt = DateTime.UtcNow;
        }

        return Result.Success();
    }

    /// <summary>A separate, explicit action (not a vote) for sending the case back to the FAC before
    /// quorum is reached — e.g. the committee needs information the profiling form didn't collect.</summary>
    public Result ReturnToFac(string? notes)
    {
        if (IsDecided)
            return Result.Failure("This committee review has already reached a decision.");

        FinalDecision = RhshfCommitteeDecision.ReturnToFac;
        DecidedAt = DateTime.UtcNow;
        Notes = notes;
        return Result.Success();
    }
}
