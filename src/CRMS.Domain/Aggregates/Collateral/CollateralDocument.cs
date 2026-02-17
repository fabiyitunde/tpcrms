using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Collateral;

public class CollateralDocument : Entity
{
    public Guid CollateralId { get; private set; }
    public string DocumentType { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string? StoragePath { get; private set; }
    public long FileSizeBytes { get; private set; }
    public string? ContentType { get; private set; }
    public string? Description { get; private set; }
    public bool IsVerified { get; private set; }
    public Guid? VerifiedByUserId { get; private set; }
    public DateTime? VerifiedAt { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public DateTime UploadedAt { get; private set; }

    private CollateralDocument() { }

    public static CollateralDocument Create(
        Guid collateralId,
        string documentType,
        string fileName,
        string? storagePath,
        long fileSizeBytes,
        string? contentType,
        Guid uploadedByUserId,
        string? description = null)
    {
        return new CollateralDocument
        {
            CollateralId = collateralId,
            DocumentType = documentType,
            FileName = fileName,
            StoragePath = storagePath,
            FileSizeBytes = fileSizeBytes,
            ContentType = contentType,
            Description = description,
            IsVerified = false,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = DateTime.UtcNow
        };
    }

    public void Verify(Guid verifiedByUserId)
    {
        IsVerified = true;
        VerifiedByUserId = verifiedByUserId;
        VerifiedAt = DateTime.UtcNow;
    }
}
