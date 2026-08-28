using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// A document the FAC attached at the SupportingDocuments stage (§4 of the design doc). No fixed
/// required-document checklist in v1 — the FAC can attach any number of supporting files.
/// </summary>
public class RhshfSupportingDocument : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public DateTime UploadedAt { get; private set; }

    protected RhshfSupportingDocument() { }

    public RhshfSupportingDocument(Guid rhshfCreditProfileId, string fileName, string contentType, string storagePath, long sizeBytes)
    {
        RhshfCreditProfileId = rhshfCreditProfileId;
        FileName = fileName;
        ContentType = contentType;
        StoragePath = storagePath;
        SizeBytes = sizeBytes;
        UploadedAt = DateTime.UtcNow;
    }
}
