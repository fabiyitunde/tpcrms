using CRMS.Domain.Enums;

namespace CRMS.Application.Rhshf.DTOs;

/// <summary>Row in a staff queue (Appraisal or RiskReview) — deliberately thin; the full picture
/// lives in RhshfCaseWorkspaceDto, opened when a staff member picks up a case.</summary>
public record RhshfQueueItemDto(
    string Reference,
    string CompanyName,
    decimal TotalEopValue,
    string Currency,
    int CurrentCycleNumber,
    DateTime UpdatedAt);

/// <summary>The staff-side case review workspace (design doc Phase 4 §7) — everything a Credit
/// Officer or Risk Officer needs to make a decision: company verification data, bureau report, EOP
/// breakdown, and uploaded documents, plus this cycle's appraisal/risk-review history.</summary>
public record RhshfCaseWorkspaceDto(
    string Reference,
    Guid SubmissionId,
    RhshfCaseStatus Status,
    RhshfInternalStage? InternalStage,
    /// <summary>Where the FAC is in the profiling form, when the case is with them. Null once it
    /// has been submitted — Status/InternalStage carry the position from then on.</summary>
    RhshfProfilingStage? CurrentProfilingStage,
    int CurrentCycleNumber,
    string CompanyName,
    string RcNumber,
    string? Tin,
    string BoaAccountNumber,
    string State,
    string Lga,
    decimal TotalEopValue,
    string Currency,
    int? FarmerCount,
    List<RhshfEopLineDto> EopLines,
    RhshfBureauOutcome BureauCheckOutcome,
    int? BureauTotalLoans,
    int? BureauActiveLoans,
    int? BureauDelinquentFacilities,
    decimal? BureauTotalOutstanding,
    decimal? BureauTotalOverdue,
    string? BureauRawJson,
    List<RhshfSupportingDocumentDto> SupportingDocuments,
    List<RhshfAppraisalDto> Appraisals,
    List<RhshfRiskReviewDto> RiskReviews,
    List<RhshfRatificationDto> Ratifications,
    RhshfDecisionOutcome? DecisionOutcome,
    decimal? ApprovedAmount,
    DateTime? DecidedAt,
    string? DecidedBy,
    string? DecisionNotes,
    /// <summary>Why this case landed on the branch it did (resolved from the BOA account via
    /// Fineract at submission). Written since day one and displayed nowhere until now — staff had
    /// no way to see, or challenge, the routing decision.</summary>
    string? BranchResolutionNote,
    DateTime ReceivedAt,
    DateTime UpdatedAt,
    /// <summary>The committee tier the facility routes to, resolved from TotalEopValue against the
    /// RhshfRoutingConfig bands — available from submission, before any committee review exists, so
    /// the header/Overview can show it up front. Null only if no band matches (misconfiguration).
    /// "Regional", "Branch", etc. — the friendly label, with CommitteeTierBand carrying the range.</summary>
    string? CommitteeTier = null,
    string? CommitteeTierBand = null);

// Every actor-carrying DTO below gets a resolved *Name companion alongside the raw id. The id stays
// (callers still need it for "is this me?" comparisons); the name is what the UI renders. Populated
// via IUserNameResolver in the query handler — see Phase A of the RH-SHF/NAMP alignment plan.
public record RhshfAppraisalDto(int CycleNumber, Guid CreditOfficerId, string CreditOfficerName, DateTime AppraisedAt, RhshfAppraisalOutcome Outcome, string? Notes);

public record RhshfRiskReviewDto(int CycleNumber, Guid RiskOfficerId, string RiskOfficerName, DateTime ReviewedAt, RhshfRiskReviewOutcome Outcome, string? Notes);

public record RhshfRatificationDto(int CycleNumber, Guid FinalApproverId, string FinalApproverName, DateTime RatifiedAt, RhshfRatificationOutcome Outcome, decimal? ApprovedAmount, string? Notes);

public record RhshfAdvisoryDto(
    Guid Id,
    Guid RhshfCreditProfileId,
    string Status,
    decimal OverallScore,
    string OverallRating,
    string Recommendation,
    decimal? RecommendedAmount,
    string? ExecutiveSummary,
    string? StrengthsAnalysis,
    string? WeaknessesAnalysis,
    string? MitigatingFactors,
    string? KeyRisks,
    bool HasCriticalRedFlags,
    string ModelVersion,
    DateTime GeneratedAt,
    string? ErrorMessage,
    List<RhshfAdvisoryRiskScoreDto> RiskScores,
    List<string> RedFlags,
    List<string> Conditions,
    List<string> Covenants
);

public record RhshfAdvisoryRiskScoreDto(
    string Category, decimal Score, decimal Weight, decimal WeightedScore, string Rating, string Rationale,
    List<string> RedFlags, List<string> PositiveIndicators);

public record RhshfLoanAccountDto(
    long LoanId,
    string AccountNo,
    string ProductName,
    string Status,
    DateTime? DisbursementDate,
    DateTime? MaturityDate,
    decimal TotalExpectedRepayment,
    decimal TotalRepayment,
    decimal TotalOutstanding,
    decimal PrincipalDisbursed,
    decimal PrincipalPaid,
    decimal PrincipalOutstanding,
    decimal InterestCharged,
    decimal InterestPaid,
    decimal InterestOutstanding,
    decimal PenaltyChargesOutstanding,
    IReadOnlyList<RhshfLoanSchedulePeriodDto> Schedule
);

public record RhshfLoanSchedulePeriodDto(
    int Period,
    DateTime DueDate,
    decimal PrincipalDue,
    decimal PrincipalPaid,
    decimal InterestDue,
    decimal InterestPaid,
    decimal TotalDue,
    decimal TotalPaid,
    decimal TotalOutstanding,
    bool Complete,
    bool IsOverdue
);

public record RhshfPreDeploymentChecklistItemDto(
    Guid Id,
    string Title,
    string? Description,
    bool IsMandatory,
    bool? IsConfirmed,
    Guid? ConfirmedByUserId,
    string? ConfirmedByName,
    DateTime? ConfirmedAt,
    string? Notes);

public record RhshfPreDeploymentChecklistTemplateDto(
    Guid Id,
    string Title,
    string? Description,
    bool IsMandatory,
    int SortOrder,
    bool IsActive
);

/// <summary>Admin view of one FAC required-document rule. Category is shown but never edited
/// after creation — cases already judged against it must stay explicable.</summary>
public record RhshfDocumentRequirementDto(
    Guid Id,
    RhshfDocumentCategory Category,
    string Title,
    string? Description,
    bool IsMandatory,
    int SortOrder,
    bool IsActive
);

public record RhshfRoutingConfigDto(
    Guid Id,
    string Tier,
    decimal MinEopValue,
    decimal MaxEopValue,
    int Priority,
    bool IsActive,
    DateTime CreatedAt
);
