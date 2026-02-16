using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.LoanApplication;

public class LoanApplicationDocument : Entity
{
    public Guid LoanApplicationId { get; private set; }
    public DocumentCategory Category { get; private set; }
    public DocumentStatus Status { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string FilePath { get; private set; } = string.Empty;
    public long FileSize { get; private set; }
    public string ContentType { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public DateTime UploadedAt { get; private set; }
    public Guid? VerifiedByUserId { get; private set; }
    public DateTime? VerifiedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    private LoanApplicationDocument() { }

    public static Result<LoanApplicationDocument> Create(
        Guid loanApplicationId,
        DocumentCategory category,
        string fileName,
        string filePath,
        long fileSize,
        string contentType,
        Guid uploadedByUserId,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Result.Failure<LoanApplicationDocument>("File name is required");

        if (string.IsNullOrWhiteSpace(filePath))
            return Result.Failure<LoanApplicationDocument>("File path is required");

        if (fileSize <= 0)
            return Result.Failure<LoanApplicationDocument>("File size must be greater than zero");

        return Result.Success(new LoanApplicationDocument
        {
            LoanApplicationId = loanApplicationId,
            Category = category,
            Status = DocumentStatus.Uploaded,
            FileName = fileName,
            FilePath = filePath,
            FileSize = fileSize,
            ContentType = contentType,
            Description = description,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = DateTime.UtcNow
        });
    }

    public Result Verify(Guid userId)
    {
        if (Status != DocumentStatus.Uploaded)
            return Result.Failure("Document must be in Uploaded status to verify");

        Status = DocumentStatus.Verified;
        VerifiedByUserId = userId;
        VerifiedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result Reject(Guid userId, string reason)
    {
        if (Status != DocumentStatus.Uploaded)
            return Result.Failure("Document must be in Uploaded status to reject");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Rejection reason is required");

        Status = DocumentStatus.Rejected;
        VerifiedByUserId = userId;
        VerifiedAt = DateTime.UtcNow;
        RejectionReason = reason;
        return Result.Success();
    }
}
