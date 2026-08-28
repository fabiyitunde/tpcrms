using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// Final Approver's ratification — fourth stage of the post-profiling pipeline (design doc §3.6).
/// One row per review cycle, append-only.
/// </summary>
public class RhshfRatification : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }
    public int CycleNumber { get; private set; }
    public Guid FinalApproverId { get; private set; }
    public DateTime RatifiedAt { get; private set; }
    public RhshfRatificationOutcome Outcome { get; private set; }
    public decimal? ApprovedAmount { get; private set; }
    public string? Notes { get; private set; }

    protected RhshfRatification() { }

    public RhshfRatification(
        Guid rhshfCreditProfileId, int cycleNumber, Guid finalApproverId,
        RhshfRatificationOutcome outcome, decimal? approvedAmount, string? notes)
    {
        RhshfCreditProfileId = rhshfCreditProfileId;
        CycleNumber = cycleNumber;
        FinalApproverId = finalApproverId;
        RatifiedAt = DateTime.UtcNow;
        Outcome = outcome;
        ApprovedAmount = approvedAmount;
        Notes = notes;
    }
}
