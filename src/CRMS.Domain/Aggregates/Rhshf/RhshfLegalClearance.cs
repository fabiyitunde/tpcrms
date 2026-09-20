using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// Legal Officer's clearance — fifth stage of the post-profiling pipeline (design doc §3.6, Phase 8).
/// Its own aggregate root, own table (design doc §3.6) — not a child of RhshfCreditProfile, unlike
/// Appraisal/RiskReview/Ratification. A one-shot decision (no build-up phase like RhshfCommitteeReview's
/// votes); a case can accumulate more than one of these per CycleNumber if Ratification repeats after
/// a Returned outcome (design doc's "Ratification (again)" loop).
/// </summary>
public class RhshfLegalClearance : AggregateRoot
{
    public Guid RhshfCreditProfileId { get; private set; }
    public int CycleNumber { get; private set; }
    public Guid LegalOfficerId { get; private set; }
    public DateTime ClearedAt { get; private set; }
    public RhshfLegalClearanceOutcome Outcome { get; private set; }
    public string? Comments { get; private set; }

    protected RhshfLegalClearance() { }

    /// <summary>finalApproverId is that cycle's ratifying Final Approver (design doc's Phase 8
    /// prompt) — the Legal Officer clearing a case must be a different person.</summary>
    public static Result<RhshfLegalClearance> Create(
        Guid rhshfCreditProfileId, int cycleNumber, Guid legalOfficerId, Guid finalApproverId,
        RhshfLegalClearanceOutcome outcome, string? comments)
    {
        if (legalOfficerId == finalApproverId)
            return Result.Failure<RhshfLegalClearance>("The Legal Officer must be a different person from the Final Approver who ratified this case.");

        return Result.Success(new RhshfLegalClearance
        {
            RhshfCreditProfileId = rhshfCreditProfileId,
            CycleNumber = cycleNumber,
            LegalOfficerId = legalOfficerId,
            ClearedAt = DateTime.UtcNow,
            Outcome = outcome,
            Comments = comments,
        });
    }
}
