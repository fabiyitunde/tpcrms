using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// The generated offer (design doc §3.6, Phase 6) — its own aggregate root, like
/// RhshfCommitteeReview, not a child of RhshfCreditProfile. Acceptance/rejection by the FAC is
/// Phase 7 (design doc §6 #10 — needs its own design pass); this phase only generates it.
/// </summary>
public class RhshfOffer : AggregateRoot
{
    public Guid RhshfCreditProfileId { get; private set; }
    public int CycleNumber { get; private set; }
    public DateTime GeneratedAt { get; private set; }
    public string OfferDocumentPath { get; private set; } = string.Empty;
    public RhshfOfferStatus Status { get; private set; }
    public DateTime? FacRespondedAt { get; private set; }

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
}
