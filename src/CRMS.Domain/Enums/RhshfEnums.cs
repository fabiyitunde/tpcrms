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
/// <summary>
/// Category of a document the FAC attaches during profiling. Replaces the original v1 behaviour of
/// accepting any number of uncategorised files with no notion of what was actually required — which
/// meant a case could be submitted for credit review with nothing attached at all.
/// </summary>
public enum RhshfDocumentCategory
{
    /// <summary>Anything that doesn't fit a defined requirement.</summary>
    Other = 0,
    CacCertificate,
    /// <summary>Audited financial statements (eligibility criterion S/N 15).</summary>
    AuditedFinancials,
    /// <summary>Signed off-take / market linkage agreement.</summary>
    OffTakeAgreement,
    /// <summary>Evidence of the out-grower network and farmer register.</summary>
    FarmerRegister,
    /// <summary>Proof of warehousing and transport capacity.</summary>
    WarehousingEvidence,
    /// <summary>Board/partnership resolution authorising the facility.</summary>
    BoardResolution,
    /// <summary>Bank statements for the BOA operational account.</summary>
    BankStatement,
    /// <summary>Land title, lease or usage rights for the cultivated hectares.</summary>
    LandDocumentation,
}

/// <summary>Repayment capacity band, derived from DSCR relative to the configured minimum.</summary>
public enum RhshfRepaymentCapacityRating
{
    Strong,
    Adequate,
    Marginal,
    Insufficient,
}

/// <summary>Credit Officer's conclusion on the financial appraisal. Binary like NAMP's — the
/// nuance lives in the gate outcomes and the override justification, not in a third enum value.</summary>
public enum RhshfCreditRecommendation
{
    Pass,
    Fail,
}

public enum RhshfInternalStage
{
    Appraisal,
    RiskReview,
    CommitteeVoting,
    Ratification,
    OfferGenerated,
    AwaitingOfferAcceptance,
    LegalClearance,
    PreDeploymentVerification,
    Disbursement,
    /// <summary>Legacy terminal marker — pre-dates Active/Closed monitoring. New disbursements set
    /// Active instead; kept for backward compatibility with rows already in this state (this enum is
    /// persisted by name, not ordinal, so old rows keep reading back correctly either way).</summary>
    Completed,
    Active,
    Closed,
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
/// <summary>Which issued document a FAC's signed upload is returning. Legacy/uncategorised uploads are
/// Other; acceptance is gated on the signed offer letter (and the signed KFS when one was issued).</summary>
public enum RhshfOfferDocumentKind
{
    Other,
    SignedOfferLetter,
    SignedKfs,
}

/// <summary>How a pre-deployment checklist item is satisfied — bind the gate to evidence that usually
/// already exists in the system rather than asking for a blind re-upload.
/// <list type="bullet">
/// <item><b>Manual</b> — a plain Disbursement-Officer attestation (fallback).</item>
/// <item><b>OfferDocuments</b> — auto-satisfied when the FAC's signed offer letter (and KFS, if one was
/// issued) is already on the offer; the officer doesn't confirm it, the system derives it.</item>
/// <item><b>AccountConfirmation</b> — the officer confirms after reviewing the live BOA account details
/// pulled from Core Banking.</item>
/// <item><b>CollateralReview</b> — the officer confirms after reviewing the collateral records and their
/// documents captured at profiling.</item>
/// </list></summary>
public enum RhshfPreDeploymentVerificationKind
{
    Manual,
    OfferDocuments,
    AccountConfirmation,
    CollateralReview,
}

public enum RhshfOfferStatus
{
    Generated,
    AwaitingFacResponse,
    Accepted,
    Rejected,
    Expired,
}

/// <summary>Legal Officer's outcome at the LegalClearance stage (design doc §3.6, Phase 8).
/// Returned routes back to Ratification, not Appraisal — a legal issue isn't a re-appraisal of the
/// credit (§6 #12). Must differ from that cycle's Final Approver.</summary>
public enum RhshfLegalClearanceOutcome
{
    Granted,
    Returned,
    Declined,
}

/// <summary>Outcome of a Disbursement Officer's booking attempt (design doc §3.6, Phase 9). Failed
/// is retryable — the case stays at the Disbursement stage, not a terminal outcome.</summary>
public enum RhshfDisbursementStatus
{
    Booked,
    Failed,
}

/// <summary>Which outbound webhook shape an RhshfCallbackAttempt represents (design doc §4.4,
/// Phase 10). Decided is the terminal outcome call; OfferReady is the non-terminal
/// actionRequired: "REVIEW_OFFER" nudge (§6 #10) — same signing/retry mechanics, different payload.</summary>
public enum RhshfCallbackEventType
{
    Decided,
    OfferReady,
}

/// <summary>Collateral instrument types named in the RH-SHF programme document (S/N 16) — a fixed,
/// small set of specific instruments (unlike NAMP's generic asset-collateral shape), so
/// RhshfCollateral models these directly rather than a free-text "collateral type" string.</summary>
public enum RhshfCollateralType
{
    BankGuarantee,
    NirsalCrg,
    LegalMortgage,
}

/// <summary>Perfection status of a legal mortgage collateral instrument.</summary>
public enum RhshfCollateralPerfectionStatus
{
    Pending,
    Perfected,
}

/// <summary>A guarantor is either a person (credit-checked individually by BVN) or a company
/// (checked as a business by RC number) — which determines the bureau path and the identifier shown.</summary>
public enum RhshfGuarantorType
{
    Individual,
    Corporate,
}

/// <summary>Review lifecycle of a captured financial statement year (mirrors Corporate's
/// FinancialStatementStatus): entered as Draft, Submitted for review, then Verified or Rejected.</summary>
public enum RhshfFinancialStatementStatus
{
    Draft,
    PendingReview,
    Verified,
    Rejected,
}

/// <summary>How a financial statement year was captured — pasted/typed in CRMS, or parsed from the
/// uploaded Excel template the FAC filled.</summary>
public enum RhshfFinancialInputMethod
{
    ManualEntry,
    ExcelUpload,
}

/// <summary>Reliability tier of a financial statement year (audited carries most weight).</summary>
public enum RhshfFinancialYearType
{
    Audited,
    Management,
    Draft,
    Projected,
}
