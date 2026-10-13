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

    /// <summary>
    /// Set when this document was carried over from a document the same FAC supplied on an earlier
    /// session, rather than uploaded here. The bytes are copied, so the two rows are independent —
    /// removing either leaves the other intact.
    /// <para>
    /// Provenance is recorded rather than an expiry enforced: nothing in the programme defines when
    /// a document goes stale, so the reviewer is shown where it came from and how old it is and
    /// judges that themselves. <see cref="UploadedAt"/> is when it was carried over;
    /// <see cref="OriginalUploadedAt"/> is when the FAC first supplied it.
    /// </para>
    /// </summary>
    public Guid? ReusedFromDocumentId { get; private set; }

    /// <summary>Case reference the carried-over document came from, so it can be named in the UI
    /// without a second lookup.</summary>
    public string? ReusedFromReference { get; private set; }

    /// <summary>When the FAC originally supplied the document, on the source case.</summary>
    public DateTime? OriginalUploadedAt { get; private set; }

    public bool IsReused => ReusedFromDocumentId.HasValue;

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

    /// <summary>Records that this document was carried over from <paramref name="sourceDocumentId"/>
    /// on case <paramref name="sourceReference"/>, first supplied at
    /// <paramref name="originalUploadedAt"/>.</summary>
    internal void MarkReusedFrom(Guid sourceDocumentId, string sourceReference, DateTime originalUploadedAt)
    {
        ReusedFromDocumentId = sourceDocumentId;
        ReusedFromReference = sourceReference;
        OriginalUploadedAt = originalUploadedAt;
    }
}
