using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>One committee member's vote (design doc §3.6, Phase 5) — append-only, never updated
/// after being cast.</summary>
public class RhshfCommitteeVote : Entity
{
    public Guid RhshfCommitteeReviewId { get; private set; }
    public Guid UserId { get; private set; }
    public RhshfCommitteeVoteChoice Vote { get; private set; }
    public DateTime VotedAt { get; private set; }
    public string? Comment { get; private set; }

    protected RhshfCommitteeVote() { }

    public RhshfCommitteeVote(Guid rhshfCommitteeReviewId, Guid userId, RhshfCommitteeVoteChoice vote, string? comment)
    {
        RhshfCommitteeReviewId = rhshfCommitteeReviewId;
        UserId = userId;
        Vote = vote;
        VotedAt = DateTime.UtcNow;
        Comment = comment;
    }
}
