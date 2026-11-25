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

    // ── Person this document belongs to (per-person requirements only) ────
    /// <summary>Set on a director's CV or ID. Null on a case-level document.</summary>
    public Guid? DirectorId { get; private set; }
    /// <summary>Set on a management staff member's CV. Null otherwise.</summary>
    public Guid? ManagementStaffId { get; private set; }

    public bool IsPersonal => DirectorId.HasValue || ManagementStaffId.HasValue;

    // ── Reviewer's check ───────────────────────────────────────────────────
    /// <summary>Rows that predate verification read back as Pending (the column default), so an
    /// officer checks them like any new upload.</summary>
    public RhshfDocumentVerificationStatus VerificationStatus { get; private set; } = RhshfDocumentVerificationStatus.Pending;
    public Guid? VerifiedByUserId { get; private set; }
    public DateTime? VerifiedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    protected RhshfSupportingDocument() { }

    public RhshfSupportingDocument(
        Guid rhshfCreditProfileId, RhshfDocumentCategory category,
        string fileName, string contentType, string storagePath, long sizeBytes,
        Guid? directorId = null, Guid? managementStaffId = null)
    {
        RhshfCreditProfileId = rhshfCreditProfileId;
        Category = category;
        FileName = fileName;
        ContentType = contentType;
        StoragePath = storagePath;
        SizeBytes = sizeBytes;
        UploadedAt = DateTime.UtcNow;
        DirectorId = directorId;
        ManagementStaffId = managementStaffId;
    }

    /// <summary>Whether this document fills the slot for <paramref name="category"/> and person.</summary>
    public bool FillsSlot(RhshfDocumentCategory category, Guid? directorId, Guid? managementStaffId) =>
        Category == category && DirectorId == directorId && ManagementStaffId == managementStaffId;

    public Result Verify(Guid userId)
    {
        if (VerificationStatus == RhshfDocumentVerificationStatus.Verified)
            return Result.Failure("This document has already been verified.");

        VerificationStatus = RhshfDocumentVerificationStatus.Verified;
        VerifiedByUserId = userId;
        VerifiedAt = DateTime.UtcNow;
        RejectionReason = null;
        return Result.Success();
    }

    /// <summary>The reason is shown to the FAC when the case is returned, so it is required.</summary>
    public Result Reject(Guid userId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Give a reason for rejecting the document. The FAC will see it.");
        if (reason.Trim().Length > 1000)
            return Result.Failure("The reason is too long.");

        VerificationStatus = RhshfDocumentVerificationStatus.Rejected;
        VerifiedByUserId = userId;
        VerifiedAt = DateTime.UtcNow;
        RejectionReason = reason.Trim();
        return Result.Success();
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
