using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Committee;

/// <summary>
/// Represents a committee member assigned to review a loan application.
/// </summary>
public class CommitteeMember : Entity
{
    public Guid CommitteeReviewId { get; private set; }
    public Guid UserId { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public bool IsChairperson { get; private set; }
    
    // Voting
    public CommitteeVote? Vote { get; private set; }
    public DateTime? VotedAt { get; private set; }
    public string? VoteComment { get; private set; }
    
    // Tracking
    public DateTime AssignedAt { get; private set; }
    public DateTime? FirstViewedAt { get; private set; }
    public int ViewCount { get; private set; }

    public bool HasVoted => Vote.HasValue;

    private CommitteeMember() { }

    internal static Result<CommitteeMember> Create(
        Guid committeeReviewId,
        Guid userId,
        string userName,
        string role,
        bool isChairperson)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return Result.Failure<CommitteeMember>("User name is required");

        if (string.IsNullOrWhiteSpace(role))
            return Result.Failure<CommitteeMember>("Role is required");

        return Result.Success(new CommitteeMember
        {
            CommitteeReviewId = committeeReviewId,
            UserId = userId,
            UserName = userName,
            Role = role,
            IsChairperson = isChairperson,
            AssignedAt = DateTime.UtcNow,
            ViewCount = 0
        });
    }

    internal Result CastVote(CommitteeVote vote, string? comment)
    {
        if (HasVoted)
            return Result.Failure("Vote already cast");

        Vote = vote;
        VotedAt = DateTime.UtcNow;
        VoteComment = comment;

        return Result.Success();
    }

    public void RecordView()
    {
        if (!FirstViewedAt.HasValue)
            FirstViewedAt = DateTime.UtcNow;
        
        ViewCount++;
    }
}
