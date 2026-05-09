namespace CRMS.Application.LoanPack.DTOs;

/// <summary>
/// Complete data required to generate a loan pack PDF.
/// </summary>
public record LoanPackData(
    // Application Info
    string ApplicationNumber,
    DateTime ApplicationDate,
    string LoanProductName,
    string LoanProductCode,
    decimal RequestedAmount,
    string Currency,
    int RequestedTenorMonths,
    decimal RequestedInterestRate,
    string Purpose,

    // Customer Profile
    CustomerProfileData Customer,

    // Application Timeline & Status
    ApplicationTimelineData Timeline,

    // Directors & Signatories
    List<DirectorData> Directors,
    List<SignatoryData> Signatories,

    // Supporting Documents
    List<DocumentRecord> Documents,

    // Bureau Reports
    List<BureauReportData> BureauReports,

    // Financial Statements
    List<FinancialStatementData> FinancialStatements,
    FinancialRatiosData? FinancialRatios,

    // Cashflow Analysis
    CashflowAnalysisData? CashflowAnalysis,

    // Collateral
    List<CollateralData> Collaterals,
    decimal TotalCollateralValue,
    decimal CollateralCoverageRatio,

    // Guarantors
    List<GuarantorData> Guarantors,
    decimal TotalGuaranteeAmount,

    // AI Advisory
    AIAdvisoryData? AIAdvisory,

    // Approval Audit Trail (from application status history)
    List<ApprovalAuditEntry> ApprovalAuditTrail,

    // Committee Comments
    List<CommitteeCommentData> CommitteeComments,

    // Conditions of Approval (from committee decision)
    List<string> ApprovalConditions,

    // Approved Terms (set after committee approval)
    decimal? ApprovedAmount,
    int? ApprovedTenorMonths,
    decimal? ApprovedInterestRate,

    // Committee Decision Summary
    CommitteeDecisionData? CommitteeDecision,

    // Disbursement Checklist (CP/CS conditions, seeded when offer is issued)
    List<ChecklistItemData> DisbursementChecklist,

    // Credit Officer Notes (application-level comments logged during processing)
    List<ApplicationCommentData> CreditOfficerNotes,

    // Workflow History (raw transition log — kept for legacy compatibility)
    List<WorkflowHistoryData> WorkflowHistory,

    // Generation Info
    DateTime GeneratedAt,
    string GeneratedBy,
    int Version
);

public record CustomerProfileData(
    string Name,
    string RegistrationNumber,
    DateTime? IncorporationDate,
    string Industry,
    string Sector,
    string Address,
    string Phone,
    string Email,
    string AccountNumber,
    string AccountType,
    DateTime? AccountOpenDate,
    decimal? AverageMonthlyBalance
);

/// <summary>
/// Key lifecycle dates and current status of the loan application.
/// All timestamps are UTC; presented in local time in the PDF.
/// </summary>
public record ApplicationTimelineData(
    string CurrentStatus,
    string ApplicationType,           // Corporate, Individual, etc.
    DateTime? SubmittedAt,
    DateTime? BranchApprovedAt,
    DateTime? CreditCheckStartedAt,
    DateTime? CreditCheckCompletedAt,
    DateTime? FinalApprovedAt,
    DateTime? OfferIssuedAt,
    DateTime? OfferAcceptedAt,
    DateTime? CustomerSignedAt,
    string? AcceptanceMethod,         // InPerson, Electronic, etc.
    bool KfsAcknowledged,
    DateTime? DisbursedAt,
    string? CoreBankingLoanId
);

/// <summary>
/// A document attached to the loan application.
/// </summary>
public record DocumentRecord(
    string FileName,
    string Category,
    string Status,
    DateTime UploadedAt,
    string? Description
);

/// <summary>
/// A single Condition Precedent or Subsequent item from the disbursement checklist.
/// </summary>
public record ChecklistItemData(
    string ItemName,
    string Description,
    string ConditionType,             // "Precedent" or "Subsequent"
    bool IsMandatory,
    bool CanBeWaived,
    string Status,                    // Pending, PendingLegalReview, Satisfied, Waived, etc.
    DateTime? SatisfiedAt,
    string? WaiverReason,
    DateTime? WaiverProposedAt,
    DateTime? DueDate
);

/// <summary>
/// One entry in the application-level approval audit trail.
/// Derived from LoanApplicationStatusHistory which is always loaded with the application.
/// </summary>
public record ApprovalAuditEntry(
    DateTime ChangedAt,
    string Status,
    string? Comment,
    string ActorName
);

public record DirectorData(
    string Name,
    string Position,
    string BVN,
    string Phone,
    string Email,
    decimal? ShareholdingPercentage,
    int? CreditScore,
    string? CreditRating,
    bool HasActiveLoans,
    bool HasDelinquencies,
    string? CreditSummary
);

public record SignatoryData(
    string Name,
    string Position,
    string BVN,
    string Phone,
    string SignatoryClass,
    int? CreditScore,
    string? CreditRating,
    bool HasActiveLoans,
    bool HasDelinquencies
);

public record BureauReportData(
    string SubjectName,
    string SubjectType, // Director, Signatory, Corporate
    string BureauProvider,
    DateTime ReportDate,
    int? CreditScore,
    string? CreditRating,
    int ActiveLoanCount,
    decimal TotalOutstandingDebt,
    int DelinquentAccountCount,
    bool HasLegalIssues,
    string? LegalIssueDetails,
    List<ActiveLoanData> ActiveLoans,
    List<DelinquencyData> Delinquencies
);

public record ActiveLoanData(
    string LenderName,
    string FacilityType,
    decimal OriginalAmount,
    decimal OutstandingBalance,
    DateTime? MaturityDate,
    string Status
);

public record DelinquencyData(
    string LenderName,
    string FacilityType,
    decimal Amount,
    int DaysOverdue,
    string Status
);

public record FinancialStatementData(
    int Year,
    string StatementType, // Audited, Management
    string AuditorName,

    // Balance Sheet
    decimal? TotalAssets,
    decimal? CurrentAssets,
    decimal? FixedAssets,
    decimal? TotalLiabilities,
    decimal? CurrentLiabilities,
    decimal? LongTermDebt,
    decimal? ShareholdersEquity,

    // Income Statement
    decimal? Revenue,
    decimal? GrossProfit,
    decimal? OperatingProfit,
    decimal? NetProfit,
    decimal? EBITDA
);

public record FinancialRatiosData(
    // Liquidity
    decimal? CurrentRatio,
    decimal? QuickRatio,
    decimal? CashRatio,

    // Leverage
    decimal? DebtToEquity,
    decimal? DebtToAssets,
    decimal? InterestCoverage,

    // Profitability
    decimal? GrossMargin,
    decimal? OperatingMargin,
    decimal? NetMargin,
    decimal? ReturnOnAssets,
    decimal? ReturnOnEquity,

    // Efficiency
    decimal? AssetTurnover,
    decimal? InventoryTurnover,
    decimal? ReceivablesDays,
    decimal? PayablesDays,

    // Coverage
    decimal? DebtServiceCoverageRatio,

    // Trends
    decimal? RevenueGrowthYoY,
    decimal? ProfitGrowthYoY
);

public record CashflowAnalysisData(
    // Bank Statement Summary
    int MonthsAnalyzed,
    decimal AverageMonthlyInflow,
    decimal AverageMonthlyOutflow,
    decimal NetCashflow,
    decimal LowestMonthlyBalance,
    decimal HighestMonthlyBalance,
    decimal AverageBalance,

    // Inflow Analysis
    decimal SalaryInflows,
    decimal BusinessInflows,
    decimal OtherInflows,

    // Outflow Analysis
    decimal LoanRepayments,
    decimal RentUtilities,
    decimal SalaryPayments,
    decimal OtherOutflows,

    // Quality Metrics
    decimal InflowVolatility,
    decimal BalanceVolatility,
    int ReturnedChequeCount,
    int OverdraftCount,
    decimal OverdraftUtilization,

    // Trust Weighting
    decimal TrustWeightedScore,
    string TrustLevel // High, Medium, Low
);

public record CollateralData(
    string Type,
    string Description,
    string Location,
    decimal MarketValue,
    decimal ForcedSaleValue,
    decimal AcceptableValue,
    string ValuationDate,
    string ValuerName,
    string Status,
    string LienType,
    string? LienReference,
    string? InsurancePolicy,
    DateTime? InsuranceExpiry,
    bool IsLegalCleared,
    DateTime? LegalClearedAt
);

public record GuarantorData(
    string Name,
    string Type, // Individual, Corporate
    string Relationship,
    string Address,
    string Phone,
    decimal NetWorth,
    decimal GuaranteeAmount,
    int? CreditScore,
    string? CreditRating,
    string Status,
    bool HasActiveLoans,
    bool HasDelinquencies
);

public record AIAdvisoryData(
    // Overall Assessment
    int OverallRiskScore,
    string RiskRating, // Low, Moderate, High, VeryHigh
    string RiskSummary,

    // Component Scores
    int CreditHistoryScore,
    int FinancialStrengthScore,
    int CashflowQualityScore,
    int CollateralCoverageScore,
    int IndustryRiskScore,
    int ManagementQualityScore,
    int RelationshipStrengthScore,
    int ExternalFactorsScore,

    // Recommendations
    string AmountRecommendation,
    decimal? RecommendedAmount,
    string TenorRecommendation,
    int? RecommendedTenorMonths,
    string PricingRecommendation,
    decimal? RecommendedInterestRate,
    string StructuringRecommendation,

    // Red Flags
    List<string> RedFlags,

    // Mitigating Factors
    List<string> MitigatingFactors,

    // Conditions
    List<string> RecommendedConditions
);

public record WorkflowHistoryData(
    DateTime Timestamp,
    string FromStatus,
    string ToStatus,
    string Action,
    string PerformedBy,
    string? Comment
);

public record CommitteeCommentData(
    DateTime Timestamp,
    string MemberName,
    string MemberRole,
    string Comment,
    string? Vote,
    string Visibility
);

public record CommitteeDecisionData(
    string Decision,           // Approved, Rejected, Pending
    int ApprovalVotes,
    int RejectionVotes,
    int AbstainVotes,
    int PendingVotes,
    string? DecisionRationale,
    decimal? RecommendedAmount,
    int? RecommendedTenorMonths,
    decimal? RecommendedInterestRate,
    List<CommitteeMemberVoteData> MemberVotes
);

public record CommitteeMemberVoteData(
    string MemberName,
    string MemberRole,
    string? Vote,
    string? VoteComment,
    DateTime? VotedAt
);

public record ApplicationCommentData(
    DateTime CreatedAt,
    string Content,
    string? Category,
    string UserId
);
