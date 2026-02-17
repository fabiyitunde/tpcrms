using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Committee;

/// <summary>
/// Represents a comment on a committee review.
/// </summary>
public class CommitteeComment : Entity
{
    public Guid CommitteeReviewId { get; private set; }
    public Guid UserId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public CommentVisibility Visibility { get; private set; }
    public DateTime? EditedAt { get; private set; }
    public bool IsEdited { get; private set; }

    private CommitteeComment() { }

    internal static Result<CommitteeComment> Create(
        Guid committeeReviewId,
        Guid userId,
        string content,
        CommentVisibility visibility)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Result.Failure<CommitteeComment>("Comment content is required");

        if (content.Length > 2000)
            return Result.Failure<CommitteeComment>("Comment cannot exceed 2000 characters");

        return Result.Success(new CommitteeComment
        {
            CommitteeReviewId = committeeReviewId,
            UserId = userId,
            Content = content,
            Visibility = visibility,
            IsEdited = false
        });
    }

    public Result Edit(string newContent, Guid editedByUserId)
    {
        if (editedByUserId != UserId)
            return Result.Failure("Only the author can edit a comment");

        if (string.IsNullOrWhiteSpace(newContent))
            return Result.Failure("Comment content is required");

        if (newContent.Length > 2000)
            return Result.Failure("Comment cannot exceed 2000 characters");

        Content = newContent;
        EditedAt = DateTime.UtcNow;
        IsEdited = true;

        return Result.Success();
    }
}
