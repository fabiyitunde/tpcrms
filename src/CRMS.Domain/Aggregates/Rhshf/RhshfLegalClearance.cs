using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// Legal Officer's clearance — the SECOND internal stage, between Appraisal and Risk Review. The
/// security the FAC declared at profiling is sighted, opined on and perfected here, before the case
/// is ever circulated for voting, so the committee decides on a file with nothing left to discover
/// and an approved case can no longer be killed by a later legal finding.
/// Its own aggregate root, own table (design doc §3.6) — not a child of RhshfCreditProfile, unlike
/// Appraisal/RiskReview/Ratification. A one-shot decision (no build-up phase like RhshfCommitteeReview's
/// votes); a case can accumulate more than one of these per CycleNumber if it is returned to the FAC
/// and comes back round.
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

    /// <summary>
    /// No Final Approver check here any more: legal clearance now runs BEFORE committee voting and
    /// ratification, so at this point no one has ratified the case yet. The distinctness requirement
    /// is inverted instead — the Legal Officer is excluded from that cycle's risk review, committee
    /// vote and ratification, enforced at each of those steps.
    /// </summary>
    public static Result<RhshfLegalClearance> Create(
        Guid rhshfCreditProfileId, int cycleNumber, Guid legalOfficerId,
        RhshfLegalClearanceOutcome outcome, string? comments)
    {
        if (legalOfficerId == Guid.Empty)
            return Result.Failure<RhshfLegalClearance>("The clearing Legal Officer is required.");

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
