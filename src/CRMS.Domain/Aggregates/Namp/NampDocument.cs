using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Namp;

public class NampDocument : Entity
{
    public Guid NampApplicationId { get; private set; }
    public NampDocumentStage Stage { get; private set; }
    public NampDocumentCategory Category { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSize { get; private set; }
    public string StoragePath { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime UploadedAt { get; private set; }
    public Guid UploadedByUserId { get; private set; }

    private NampDocument() { }

    public static NampDocument Create(
        Guid nampApplicationId,
        NampDocumentStage stage,
        string fileName,
        string contentType,
        long fileSize,
        string storagePath,
        Guid uploadedByUserId,
        NampDocumentCategory category = NampDocumentCategory.General,
        string? description = null)
    {
        return new NampDocument
        {
            NampApplicationId = nampApplicationId,
            Stage = stage,
            Category = category,
            FileName = fileName,
            ContentType = contentType,
            FileSize = fileSize,
            StoragePath = storagePath,
            UploadedByUserId = uploadedByUserId,
            Description = description,
            UploadedAt = DateTime.UtcNow,
        };
    }

    public void UpdateMetadata(NampDocumentCategory category, string? description)
    {
        Category = category;
        Description = description;
    }
}
