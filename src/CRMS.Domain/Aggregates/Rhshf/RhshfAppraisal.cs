using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// Credit Officer's appraisal at the first stage of the post-profiling pipeline (design doc §3.6).
/// One row per review cycle — a fresh row is created if the case ever returns to the FAC and comes
/// back around (design doc §6 #8).
/// </summary>
public class RhshfAppraisal : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }
    public int CycleNumber { get; private set; }
    public Guid CreditOfficerId { get; private set; }
    public DateTime AppraisedAt { get; private set; }
    public RhshfAppraisalOutcome Outcome { get; private set; }
    public string? Notes { get; private set; }

    protected RhshfAppraisal() { }

    public RhshfAppraisal(Guid rhshfCreditProfileId, int cycleNumber, Guid creditOfficerId, RhshfAppraisalOutcome outcome, string? notes)
    {
        RhshfCreditProfileId = rhshfCreditProfileId;
        CycleNumber = cycleNumber;
        CreditOfficerId = creditOfficerId;
        AppraisedAt = DateTime.UtcNow;
        Outcome = outcome;
        Notes = notes;
    }
}
