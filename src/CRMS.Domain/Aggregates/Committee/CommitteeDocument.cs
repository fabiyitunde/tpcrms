using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Committee;

/// <summary>
/// Represents a document attached to a committee review.
/// </summary>
public class CommitteeDocument : Entity
{
    public Guid CommitteeReviewId { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string FilePath { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DocumentVisibility Visibility { get; private set; }
    public DateTime UploadedAt { get; private set; }

    private CommitteeDocument() { }

    internal static Result<CommitteeDocument> Create(
        Guid committeeReviewId,
        Guid uploadedByUserId,
        string fileName,
        string filePath,
        string description,
        DocumentVisibility visibility)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Result.Failure<CommitteeDocument>("File name is required");

        if (string.IsNullOrWhiteSpace(filePath))
            return Result.Failure<CommitteeDocument>("File path is required");

        return Result.Success(new CommitteeDocument
        {
            CommitteeReviewId = committeeReviewId,
            UploadedByUserId = uploadedByUserId,
            FileName = fileName,
            FilePath = filePath,
            Description = description ?? string.Empty,
            Visibility = visibility,
            UploadedAt = DateTime.UtcNow
        });
    }
}
