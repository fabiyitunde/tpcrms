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
    int CurrentCycleNumber,
    string CompanyName,
    string RcNumber,
    string Tin,
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
    DateTime ReceivedAt,
    DateTime UpdatedAt);

public record RhshfAppraisalDto(int CycleNumber, Guid CreditOfficerId, DateTime AppraisedAt, RhshfAppraisalOutcome Outcome, string? Notes);

public record RhshfRiskReviewDto(int CycleNumber, Guid RiskOfficerId, DateTime ReviewedAt, RhshfRiskReviewOutcome Outcome, string? Notes);

public record RhshfRatificationDto(int CycleNumber, Guid FinalApproverId, DateTime RatifiedAt, RhshfRatificationOutcome Outcome, decimal? ApprovedAmount, string? Notes);

public record RhshfRoutingConfigDto(
    Guid Id,
    string Tier,
    decimal MinEopValue,
    decimal MaxEopValue,
    int Priority,
    bool IsActive,
    DateTime CreatedAt
);
