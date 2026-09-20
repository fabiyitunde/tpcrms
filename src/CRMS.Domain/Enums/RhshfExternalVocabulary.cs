namespace CRMS.Domain.Enums;

/// <summary>
/// Maps RH-SHF's internal enums to the portal-facing UPPER_SNAKE_CASE vocabulary from the
/// integration brief §5 — used by both the outcome webhook (§4.4, Phase 10) and the status
/// endpoint (§4.5, Phase 11), kept here (Domain) so neither has to duplicate it.
/// </summary>
public static class RhshfExternalVocabulary
{
    public static string ToExternalStatus(this RhshfCaseStatus status) => status switch
    {
        RhshfCaseStatus.Received => "RECEIVED",
        RhshfCaseStatus.ProfilingPending => "PROFILING_PENDING",
        RhshfCaseStatus.ProfilingInProgress => "PROFILING_IN_PROGRESS",
        RhshfCaseStatus.UnderReview => "UNDER_REVIEW",
        RhshfCaseStatus.Approved => "APPROVED",
        RhshfCaseStatus.Declined => "DECLINED",
        RhshfCaseStatus.InfoRequired => "INFO_REQUIRED",
        RhshfCaseStatus.Expired => "EXPIRED",
        RhshfCaseStatus.Cancelled => "CANCELLED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped RhshfCaseStatus."),
    };

    public static string ToExternalOutcome(this RhshfDecisionOutcome outcome) => outcome switch
    {
        RhshfDecisionOutcome.Approved => "APPROVED",
        RhshfDecisionOutcome.Declined => "DECLINED",
        RhshfDecisionOutcome.InfoRequired => "INFO_REQUIRED",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unmapped RhshfDecisionOutcome."),
    };

    /// <summary>camelCase slug for the status endpoint's stage.current (§4.5, Phase 11) — the
    /// brief's own example ("documents") doesn't map cleanly onto our 5 actual stage names, and the
    /// brief itself says the wire format is negotiable, so this uses CRMS's own stage vocabulary;
    /// confirm naming with the portal during onboarding.</summary>
    public static string ToExternalStageName(this RhshfProfilingStage stage) => stage switch
    {
        RhshfProfilingStage.CompanyVerification => "companyVerification",
        RhshfProfilingStage.CreditBureauCheck => "creditBureauCheck",
        RhshfProfilingStage.EopReview => "eopReview",
        RhshfProfilingStage.SupportingDocuments => "supportingDocuments",
        RhshfProfilingStage.ReviewAndSubmit => "reviewAndSubmit",
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unmapped RhshfProfilingStage."),
    };
}
