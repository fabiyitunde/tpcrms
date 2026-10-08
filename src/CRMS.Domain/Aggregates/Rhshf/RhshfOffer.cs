using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// The generated offer (design doc §3.6, Phases 6-7) — its own aggregate root, like
/// RhshfCommitteeReview, not a child of RhshfCreditProfile. Accept() requires a signed copy to
/// already be uploaded (design doc §6 #10) — a hard precondition, not a UI-only nudge.
/// </summary>
public class RhshfOffer : AggregateRoot
{
    public Guid RhshfCreditProfileId { get; private set; }
    public int CycleNumber { get; private set; }
    public DateTime GeneratedAt { get; private set; }
    public string OfferDocumentPath { get; private set; } = string.Empty;
    /// <summary>The generated Key Facts Statement that accompanies the offer letter (null until
    /// attached). A separate document so the FAC can sign and return it on its own.</summary>
    public string? KfsDocumentPath { get; private set; }
    public RhshfOfferStatus Status { get; private set; }
    public DateTime? FacRespondedAt { get; private set; }
    public string? FacResponseNotes { get; private set; }

    private readonly List<RhshfOfferDocument> _documents = [];
    public IReadOnlyCollection<RhshfOfferDocument> Documents => _documents.AsReadOnly();

    protected RhshfOffer() { }

    public static Result<RhshfOffer> Create(Guid rhshfCreditProfileId, int cycleNumber, string offerDocumentPath)
    {
        if (string.IsNullOrWhiteSpace(offerDocumentPath))
            return Result.Failure<RhshfOffer>("offerDocumentPath is required.");

        return Result.Success(new RhshfOffer
        {
            RhshfCreditProfileId = rhshfCreditProfileId,
            CycleNumber = cycleNumber,
            GeneratedAt = DateTime.UtcNow,
            OfferDocumentPath = offerDocumentPath,
            Status = RhshfOfferStatus.Generated,
        });
    }

    /// <summary>
    /// Re-points the offer at a freshly generated PDF (recovery for a missing/corrupt file). Allowed
    /// only while the FAC has not yet decided — the letter's content is fully determined by the
    /// ratified amount and case data, which don't change in this state, so a regenerated copy is
    /// identical to one the FAC may already have downloaded. Once Accepted/Rejected the document is
    /// part of the record and must not be silently replaced; changing terms is a new offer instead.
    /// </summary>
    public Result RegenerateDocument(string offerDocumentPath)
    {
        if (string.IsNullOrWhiteSpace(offerDocumentPath))
            return Result.Failure("offerDocumentPath is required.");
        if (Status is not (RhshfOfferStatus.Generated or RhshfOfferStatus.AwaitingFacResponse))
            return Result.Failure("The offer letter can only be regenerated before the FAC has accepted or rejected it.");

        OfferDocumentPath = offerDocumentPath;
        GeneratedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Attaches (or replaces) the generated Key Facts Statement. Same window as regeneration —
    /// only before the FAC has responded, so a signed-off package isn't altered underneath them.</summary>
    public Result AttachKfs(string kfsDocumentPath)
    {
        if (string.IsNullOrWhiteSpace(kfsDocumentPath))
            return Result.Failure("kfsDocumentPath is required.");
        if (Status is not (RhshfOfferStatus.Generated or RhshfOfferStatus.AwaitingFacResponse))
            return Result.Failure("The Key Facts Statement can only be (re)generated before the FAC has responded.");

        KfsDocumentPath = kfsDocumentPath;
        return Result.Success();
    }

    /// <summary>No fixed checklist beyond "at least one" — the FAC can upload more than once
    /// (e.g. a re-scan) while the offer is still undecided.</summary>
    public Result<RhshfOfferDocument> AddDocument(string fileName, string contentType, string storagePath, long sizeBytes)
    {
        if (Status != RhshfOfferStatus.Generated)
            return Result.Failure<RhshfOfferDocument>("This offer has already been decided — no further documents can be attached.");

        var document = new RhshfOfferDocument(Id, fileName, contentType, storagePath, sizeBytes);
        _documents.Add(document);
        return Result.Success(document);
    }

    /// <summary>Fails if no signed copy has been uploaded yet — checked here, not trusted from the
    /// caller (design doc §6 #10).</summary>
    public Result Accept(string? notes)
    {
        if (Status != RhshfOfferStatus.Generated)
            return Result.Failure("This offer has already been decided.");
        if (_documents.Count == 0)
            return Result.Failure("A signed copy of the offer must be uploaded before it can be accepted.");

        Status = RhshfOfferStatus.Accepted;
        FacRespondedAt = DateTime.UtcNow;
        FacResponseNotes = notes;
        return Result.Success();
    }

    /// <summary>No document requirement — rejecting doesn't need paperwork.</summary>
    public Result Reject(string? notes)
    {
        if (Status != RhshfOfferStatus.Generated)
            return Result.Failure("This offer has already been decided.");

        Status = RhshfOfferStatus.Rejected;
        FacRespondedAt = DateTime.UtcNow;
        FacResponseNotes = notes;
        return Result.Success();
    }
}
