using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// The FAC's signed copy of the offer letter (design doc §3.6/§6 #10) — a hard precondition for
/// RhshfOffer.Accept(). Scoped to the offer/cycle that produced it, not the flat
/// RhshfCreditProfile.SupportingDocuments list from the profiling stage — a different artifact
/// from a different moment in the lifecycle.
/// </summary>
public class RhshfOfferDocument : Entity
{
    public Guid RhshfOfferId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public DateTime UploadedAt { get; private set; }

    /// <summary>Which issued document this signed upload returns (offer letter / KFS / other).</summary>
    public RhshfOfferDocumentKind Kind { get; private set; }

    protected RhshfOfferDocument() { }

    public RhshfOfferDocument(Guid rhshfOfferId, string fileName, string contentType, string storagePath, long sizeBytes,
        RhshfOfferDocumentKind kind = RhshfOfferDocumentKind.Other)
    {
        RhshfOfferId = rhshfOfferId;
        FileName = fileName;
        ContentType = contentType;
        StoragePath = storagePath;
        SizeBytes = sizeBytes;
        Kind = kind;
        UploadedAt = DateTime.UtcNow;
    }
}
