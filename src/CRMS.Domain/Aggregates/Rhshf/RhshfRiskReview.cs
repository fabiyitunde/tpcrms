using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// Risk Officer's review — second stage of the post-profiling pipeline (design doc §3.6). Must be a
/// different person from that cycle's RhshfAppraisal.CreditOfficerId; enforced in the aggregate,
/// not just here.
/// </summary>
public class RhshfRiskReview : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }
    public int CycleNumber { get; private set; }
    public Guid RiskOfficerId { get; private set; }
    public DateTime ReviewedAt { get; private set; }
    public RhshfRiskReviewOutcome Outcome { get; private set; }
    public string? Notes { get; private set; }

    protected RhshfRiskReview() { }

    public RhshfRiskReview(Guid rhshfCreditProfileId, int cycleNumber, Guid riskOfficerId, RhshfRiskReviewOutcome outcome, string? notes)
    {
        RhshfCreditProfileId = rhshfCreditProfileId;
        CycleNumber = cycleNumber;
        RiskOfficerId = riskOfficerId;
        ReviewedAt = DateTime.UtcNow;
        Outcome = outcome;
        Notes = notes;
    }
}
