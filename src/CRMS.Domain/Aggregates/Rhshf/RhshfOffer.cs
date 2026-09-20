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
