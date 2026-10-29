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

    /// <summary>
    /// Core banking charges that were left off this letter because the EOP already finances them,
    /// null when there were none.
    /// <para>
    /// Recorded on the offer rather than only logged: the letter is correct, but core banking will
    /// still deduct the fee at booking unless somebody changes the product, and a log line is not
    /// something an officer working this case will ever see. Kept as what was true when the letter
    /// was issued, so a later product fix does not erase the fact that an offer went out against a
    /// misconfigured product.
    /// </para>
    /// </summary>
    public string? SuppressedProductFees { get; private set; }

    // ── Terms printed on this offer ───────────────────────────────────────
    // What the letter told the FAC, kept with the offer. The appraisal's figures can differ for offers
    // made before tenor and rate came from core banking (Grandbai's version 4 said six months while
    // the product books nine), so screens that show "the offer" read these, not the appraisal.
    // Null on offers issued before these were recorded.

    public int? TenorMonths { get; private set; }
    public decimal? InterestRatePercent { get; private set; }
    /// <summary>The interest the letter estimated over the full tenor. The booked figure depends on
    /// the actual disbursement date.</summary>
    public decimal? EstimatedInterest { get; private set; }

    public void RecordTerms(int tenorMonths, decimal interestRatePercent, decimal estimatedInterest)
    {
        TenorMonths = tenorMonths;
        InterestRatePercent = interestRatePercent;
        EstimatedInterest = estimatedInterest;
    }

    private readonly List<RhshfOfferDocument> _documents = [];
    public IReadOnlyCollection<RhshfOfferDocument> Documents => _documents.AsReadOnly();

    protected RhshfOffer() { }

    /// <summary>
    /// Notes the core banking charges left off the letter. Called on generation and again on a
    /// reissue, so the record follows the document actually in the borrower's hands — including
    /// clearing it once the product has been fixed and nothing collides any more.
    /// </summary>
    public void RecordSuppressedProductFees(IEnumerable<string>? feeNames)
    {
        var names = feeNames?.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Distinct().ToList();
        SuppressedProductFees = names is null || names.Count == 0 ? null : string.Join(", ", names);
    }

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
    /// only while the FAC has not yet decided — the document's content is fully determined by the
    /// ratified amount and case data, which don't change in this state, so a regenerated copy is
    /// identical to one the FAC may already have downloaded. Once Accepted/Rejected the document is
    /// part of the record and must not be silently replaced; changing terms is a new offer instead.
    /// <para>
    /// A regenerated offer is always the single merged document, so any Key Facts Statement from
    /// before the merge is dropped: the borrower now has one document to sign, and
    /// <see cref="HasRequiredSignedDocuments"/> must ask for one signature rather than keep demanding
    /// a signed copy of a statement that is no longer issued.
    /// </para>
    /// </summary>
    public Result RegenerateDocument(string offerDocumentPath)
    {
        if (string.IsNullOrWhiteSpace(offerDocumentPath))
            return Result.Failure("offerDocumentPath is required.");
        if (Status is not (RhshfOfferStatus.Generated or RhshfOfferStatus.AwaitingFacResponse))
            return Result.Failure("The offer document can only be regenerated before the FAC has accepted or rejected it.");

        OfferDocumentPath = offerDocumentPath;
        KfsDocumentPath = null;
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
    public Result<RhshfOfferDocument> AddDocument(string fileName, string contentType, string storagePath, long sizeBytes,
        RhshfOfferDocumentKind kind = RhshfOfferDocumentKind.Other)
    {
        if (Status != RhshfOfferStatus.Generated)
            return Result.Failure<RhshfOfferDocument>("This offer has already been decided — no further documents can be attached.");

        var document = new RhshfOfferDocument(Id, fileName, contentType, storagePath, sizeBytes, kind);
        _documents.Add(document);
        return Result.Success(document);
    }

    /// <summary>Removes a signed upload (e.g. the FAC uploaded the wrong file) — only while the offer
    /// is still undecided.</summary>
    public Result RemoveDocument(Guid documentId)
    {
        if (Status != RhshfOfferStatus.Generated)
            return Result.Failure("This offer has already been decided — its documents can no longer be changed.");

        var document = _documents.FirstOrDefault(d => d.Id == documentId);
        if (document is null)
            return Result.Failure("Document not found on this offer.");

        _documents.Remove(document);
        return Result.Success();
    }

    /// <summary>Whether the FAC's signed package is on file — the signed offer letter (a legacy "Other"
    /// upload also counts, so in-flight cases aren't stranded), plus the signed KFS when one was issued.
    /// The single source of truth for both Accept()'s precondition and the pre-deployment auto gate.</summary>
    public bool HasRequiredSignedDocuments =>
        _documents.Any(d => d.Kind is RhshfOfferDocumentKind.SignedOfferLetter or RhshfOfferDocumentKind.Other)
        && (string.IsNullOrEmpty(KfsDocumentPath) || _documents.Any(d => d.Kind == RhshfOfferDocumentKind.SignedKfs));

    /// <summary>Fails if no signed copy has been uploaded yet — checked here, not trusted from the
    /// caller (design doc §6 #10).</summary>
    public Result Accept(string? notes)
    {
        if (Status != RhshfOfferStatus.Generated)
            return Result.Failure("This offer has already been decided.");

        // The FAC must return the signed offer letter — and the signed KFS too, when one was issued.
        if (!_documents.Any(d => d.Kind is RhshfOfferDocumentKind.SignedOfferLetter or RhshfOfferDocumentKind.Other))
            return Result.Failure("Upload the signed offer letter before accepting.");
        if (!string.IsNullOrEmpty(KfsDocumentPath) && !_documents.Any(d => d.Kind == RhshfOfferDocumentKind.SignedKfs))
            return Result.Failure("Upload the signed Key Facts Statement before accepting.");

        Status = RhshfOfferStatus.Accepted;
        FacRespondedAt = DateTime.UtcNow;
        FacResponseNotes = notes;
        return Result.Success();
    }

    /// <summary>
    /// Retires an offer that turned out to be wrong, so a corrected one can be issued in its place.
    /// Not a rewrite: this row, its document and any signed copies stay exactly as they were. Works
    /// on an accepted offer and on one the FAC has not answered yet — the second matters because the
    /// FAC may already hold that version, and replacing it in place would leave two documents in
    /// circulation under one version. A rejected offer has ended the case and is not replaced.
    /// </summary>
    public Result Supersede()
    {
        if (Status is not (RhshfOfferStatus.Accepted or RhshfOfferStatus.Generated or RhshfOfferStatus.AwaitingFacResponse))
            return Result.Failure("Only an issued or accepted offer can be replaced by a corrected one.");

        Status = RhshfOfferStatus.Superseded;
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
