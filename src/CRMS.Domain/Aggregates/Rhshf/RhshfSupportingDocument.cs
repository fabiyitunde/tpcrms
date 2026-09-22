using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// A document the FAC attached at the SupportingDocuments stage (§4 of the design doc).
///
/// Categorised as of Phase D — uploads were previously untyped, so nothing could express which
/// documents a case actually needed, and submission could not be gated on them.
/// </summary>
public class RhshfSupportingDocument : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }
    public RhshfDocumentCategory Category { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public DateTime UploadedAt { get; private set; }

    protected RhshfSupportingDocument() { }

    public RhshfSupportingDocument(
        Guid rhshfCreditProfileId, RhshfDocumentCategory category,
        string fileName, string contentType, string storagePath, long sizeBytes)
    {
        RhshfCreditProfileId = rhshfCreditProfileId;
        Category = category;
        FileName = fileName;
        ContentType = contentType;
        StoragePath = storagePath;
        SizeBytes = sizeBytes;
        UploadedAt = DateTime.UtcNow;
    }
}
