namespace CRMS.Domain.Enums;

/// <summary>
/// External case status for RH-SHF credit-profiling, matching §5 of the portal integration brief.
/// RH-SHF is a separate loan track from NAMP — this enum must never be merged with NampEnums.
/// </summary>
public enum RhshfCaseStatus
{
    Received,
    ProfilingPending,
    ProfilingInProgress,
    UnderReview,
    Approved,
    Declined,
    InfoRequired,
    Expired,
    Cancelled,
}

/// <summary>
/// FAC-facing steps inside the CRMS-hosted profiling form (design doc §4).
/// Director/shareholder capture is deliberately excluded from v1 — see design doc §6 #3.
/// </summary>
public enum RhshfProfilingStage
{
    CompanyVerification = 1,
    CreditBureauCheck = 2,
    EopReview = 3,
    SupportingDocuments = 4,
    ReviewAndSubmit = 5,
}

/// <summary>
/// Final decision outcome, matching §4.4/§5 of the integration brief.
/// </summary>
public enum RhshfDecisionOutcome
{
    Approved,
    Declined,
    InfoRequired,
}

/// <summary>Outcome of the automated business credit-bureau pull at the CreditBureauCheck stage.
/// Informational for now — feeds the Phase 4+ credit officer's review, not an automated gate.</summary>
public enum RhshfBureauOutcome
{
    NotRun,
    Cleared,
    Flagged,
    Failed,
}

/// <summary>Internal-only granularity behind the external UnderReview status (design doc §4/§5) —
/// tracks where a case sits in the post-profiling staff pipeline. Null before the case first
/// reaches UnderReview, and reset to null on any ReturnToFac or terminal Decline.</summary>
public enum RhshfInternalStage
{
    Appraisal,
    RiskReview,
    CommitteeVoting,
    Ratification,
    OfferGenerated,
    AwaitingOfferAcceptance,
    LegalClearance,
    Disbursement,
    Completed,
}

/// <summary>Credit Officer's outcome at the Appraisal stage (design doc §3.6).</summary>
public enum RhshfAppraisalOutcome
{
    Proceed,
    ReturnToFac,
    Decline,
}

/// <summary>Risk Officer's outcome at the RiskReview stage (design doc §3.6) — must be a different
/// person from that cycle's appraising Credit Officer.</summary>
public enum RhshfRiskReviewOutcome
{
    Cleared,
    ReturnToFac,
    Decline,
}

/// <summary>An individual committee member's vote (design doc §3.6, Phase 5).</summary>
public enum RhshfCommitteeVoteChoice
{
    Approve,
    Reject,
    Abstain,
}

/// <summary>Committee's outcome. Approved/Rejected are reached automatically once quorum is met
/// (design doc §3.6); Deferred is reserved for a future chair action, not reachable in v1;
/// ReturnToFac is a separate explicit action, not a vote tally result.</summary>
public enum RhshfCommitteeDecision
{
    Approved,
    Rejected,
    Deferred,
    ReturnToFac,
}

/// <summary>Final Approver's outcome at the Ratification stage (design doc §3.6, Phase 6) — must
/// differ from the committee members who approved (and from that cycle's Appraisal/RiskReview
/// actors). Ratified requires ApprovedAmount == TotalEopValue exactly (design doc §6 #1).</summary>
public enum RhshfRatificationOutcome
{
    Ratified,
    ReturnToFac,
    Declined,
}

/// <summary>Lifecycle of the generated offer document (design doc §3.6). Acceptance/rejection by
/// the FAC is Phase 7 — not wired yet; Generated is the only status this phase produces.</summary>
public enum RhshfOfferStatus
{
    Generated,
    AwaitingFacResponse,
    Accepted,
    Rejected,
    Expired,
}
