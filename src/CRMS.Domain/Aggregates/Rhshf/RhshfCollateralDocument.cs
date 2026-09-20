using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>Scan of a collateral instrument (Bank Guarantee letter, NIRSAL CRG certificate, mortgage
/// title deed) — same shape as RhshfSupportingDocument, scoped to the RhshfCollateral record it
/// evidences rather than the flat profiling-stage document list.</summary>
public class RhshfCollateralDocument : Entity
{
    public Guid RhshfCollateralId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public DateTime UploadedAt { get; private set; }

    protected RhshfCollateralDocument() { }

    public RhshfCollateralDocument(Guid rhshfCollateralId, string fileName, string contentType, string storagePath, long sizeBytes)
    {
        RhshfCollateralId = rhshfCollateralId;
        FileName = fileName;
        ContentType = contentType;
        StoragePath = storagePath;
        SizeBytes = sizeBytes;
        UploadedAt = DateTime.UtcNow;
    }
}
