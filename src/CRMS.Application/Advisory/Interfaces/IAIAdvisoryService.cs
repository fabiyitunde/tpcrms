using CRMS.Application.Advisory.DTOs;

namespace CRMS.Application.Advisory.Interfaces;

/// <summary>
/// Interface for AI-powered credit advisory generation.
/// </summary>
public interface IAIAdvisoryService
{
    /// <summary>
    /// Generates a complete credit advisory based on aggregated data.
    /// </summary>
    Task<AIAdvisoryResponse> GenerateAdvisoryAsync(AIAdvisoryRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// Gets the current model version being used.
    /// </summary>
    string GetModelVersion();

    /// <summary>
    /// Scores one individual for the customer credit check (docs/Customer_Credit_Check.md, aiAdvisory): a
    /// profile built for a person rather than a company, from what the other sections found.
    /// The rules decide the numbers; the narrative only explains them.
    /// </summary>
    Task<CustomerAdvisoryResponse> GenerateCustomerAdvisoryAsync(CustomerAdvisoryRequest request, CancellationToken ct = default);
}

// ── Customer credit check advisory ──────────────────────────────────────────────────────────────────────
// Everything below is figures and flags only. No name, BVN, account number, phone or date of birth
// is carried here, so nothing identifying can reach the prompt or the narrative.

public record CustomerAdvisoryRequest(
    Guid ReferenceId,
    decimal ProposedAmount,
    int ProposedTenorMonths,
    /// <summary>False when the amount and tenor are the configured stand-ins, not the request's.</summary>
    bool ProposalSupplied,
    CustomerBureauInput? Bureau,
    CustomerCashflowInput? Cashflow,
    CustomerExposureInput Exposure,
    CustomerIdentityInput Identity);

public record CustomerBureauInput(
    string Bureau,
    bool Reused,
    int? CreditScore,
    int TotalLoans,
    int ActiveLoans,
    int ClosedLoans,
    int PerformingLoans,
    int DelinquentLoans,
    int WrittenOffLoans,
    decimal TotalOutstanding,
    decimal TotalOverdue,
    int MaxDaysOverdue,
    int RecentEnquiries);

public record CustomerCashflowInput(
    int WindowMonths,
    int AccountsRead,
    int TransactionsAnalyzed,
    int MonthsWithActivity,
    decimal AverageMonthlyInflow,
    decimal AverageMonthlyOutflow,
    decimal NetMonthlyCashflow,
    /// <summary>Standard deviation of monthly inflow over its mean; 0 when inflow is nil.</summary>
    decimal InflowVolatility,
    int BouncedTransactionCount,
    int DaysWithNegativeBalance,
    decimal TotalBalance);

public record CustomerExposureInput(
    bool BankDataAvailable,
    int BankRunningLoans,
    int BankOverdueLoans,
    int BankPaidLoans,
    int BankWrittenOffLoans,
    decimal BankOutstanding,
    decimal BankOverdue,
    int BankMaxDaysPastDue,
    /// <summary>Sum of the next instalment due on each running bank loan.</summary>
    decimal BankMonthlyInstalments,
    int CrmsDirectFacilities,
    int CrmsIndirectFacilities,
    decimal? CrmsActiveOutstanding);

public record CustomerIdentityInput(
    bool? BvnVerified,
    bool? WatchListed,
    int? FraudRiskScore,
    string? FraudRiskBand,
    bool? BankCustomerFound,
    string? KycLevel,
    int? MonthsAsCustomer);

/// <summary>A category the profile could not score, with the weight it would have carried.</summary>
public record CustomerAdvisoryGap(string Category, decimal Weight, string Reason);

public record CustomerAdvisoryResponse(
    bool Success,
    string? ErrorMessage,
    List<RiskScoreOutput> Scores,
    List<CustomerAdvisoryGap> NotAssessed,
    decimal OverallScore,
    string OverallRating,
    string Recommendation,
    decimal? RecommendedAmount,
    int? RecommendedTenorMonths,
    List<string> Conditions,
    List<string> RedFlags,
    string ExecutiveSummary,
    string StrengthsAnalysis,
    string WeaknessesAnalysis,
    string? MitigatingFactors,
    string? KeyRisks,
    bool NarrativeFromLlm,
    string ModelVersion);

public record AIAdvisoryRequest(
    Guid LoanApplicationId,
    decimal RequestedAmount,
    int RequestedTenorMonths,
    string ProductType,
    string Industry,
    
    // Bureau Data
    List<BureauDataInput> BureauReports,
    
    // Financial Data
    List<FinancialDataInput> FinancialStatements,
    
    // Cashflow Data
    CashflowDataInput? CashflowAnalysis,
    
    // Collateral Data
    CollateralDataInput? CollateralSummary,
    
    // Guarantor Data
    List<GuarantorDataInput> Guarantors,
    
    // Existing Exposure
    decimal ExistingExposure,
    int ExistingFacilitiesCount,

    // Optional free-text context injected verbatim into the LLM prompt
    // (e.g. NAMP technical appraisal summary, product-specific notes)
    string? AdditionalContext = null
);

public record BureauDataInput(
    Guid ReportId,
    string SubjectName,
    string SubjectType, // Director, Signatory, Guarantor, Corporate
    int? CreditScore,
    int ActiveLoansCount,
    decimal TotalOutstandingDebt,
    int PerformingLoansCount,
    int DelinquentLoansCount,
    int DefaultedLoansCount,
    string? WorstStatus,
    DateTime ReportDate,
    // Extended fields from bureau response
    int MaxDelinquencyDays = 0,
    bool HasLegalActions = false,
    decimal TotalOverdue = 0,
    int? FraudRiskScore = null,
    string? FraudRecommendation = null,
    bool IsPlaceholder = false  // true = no actual bureau data available
);

public record FinancialDataInput(
    Guid StatementId,
    int Year,
    string YearType,
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal TotalEquity,
    decimal Revenue,
    decimal NetProfit,
    decimal EBITDA,
    decimal CurrentRatio,
    decimal QuickRatio,
    decimal DebtToEquityRatio,
    decimal InterestCoverageRatio,
    decimal DebtServiceCoverageRatio,
    decimal NetProfitMarginPercent,
    decimal ReturnOnEquity,
    string LiquidityAssessment,
    string LeverageAssessment,
    string ProfitabilityAssessment,
    string OverallAssessment,
    bool IsUnverified = false // true = Submitted/PendingReview, not yet verified by credit analyst
);

public record CashflowDataInput(
    Guid AnalysisId,
    int MonthsAnalyzed,
    decimal AverageMonthlyInflow,
    decimal AverageMonthlyOutflow,
    decimal NetMonthlyCashflow,
    decimal CashflowVolatility,
    int RecurringCreditsCount,
    int RecurringDebitsCount,
    decimal LoanRepaymentRatio,
    bool HasSalaryCredits,
    string CashflowHealthAssessment,
    // Statement source trust indicators
    bool HasInternalStatement,
    int ExternalStatementsCount,
    bool AllExternalStatementsVerified,
    decimal OverallTrustScore,
    // Risk indicators from bank statements
    int GamblingTransactionCount,
    decimal GamblingTransactionTotal,
    int BouncedTransactionCount,
    int DaysWithNegativeBalance,
    // Salary detection
    decimal? DetectedMonthlySalary,
    string? SalarySource,
    // Warnings from analysis
    List<string> AnalysisWarnings
);

public record CollateralDataInput(
    int TotalCollateralCount,
    decimal TotalMarketValue,
    decimal TotalForcedSaleValue,
    decimal AverageLTV,
    List<string> CollateralTypes,
    bool HasPerfectedLiens,
    int ApprovedCount = 0,
    int ValuedButNotApprovedCount = 0,
    decimal ValuedButNotApprovedMarketValue = 0
);

public record GuarantorDataInput(
    Guid GuarantorId,
    string Name,
    string Type, // Individual, Corporate
    decimal NetWorth,
    decimal GuaranteeAmount,
    int? CreditScore,
    string CreditStatus,
    bool HasBureauReport = true
);

public record AIAdvisoryResponse(
    bool Success,
    string? ErrorMessage,
    
    // Scores
    List<RiskScoreOutput> RiskScores,
    decimal OverallScore,
    string OverallRating,
    string Recommendation,
    
    // Loan Recommendations
    decimal? RecommendedAmount,
    int? RecommendedTenorMonths,
    decimal? RecommendedInterestRate,
    decimal? MaxExposure,
    
    // Conditions
    List<string> Conditions,
    List<string> Covenants,
    
    // Analysis
    string ExecutiveSummary,
    string StrengthsAnalysis,
    string WeaknessesAnalysis,
    string? MitigatingFactors,
    string? KeyRisks,
    
    // Red Flags
    List<string> RedFlags
);

public record RiskScoreOutput(
    string Category,
    decimal Score,
    decimal Weight,
    string Rating,
    string Rationale,
    List<string> RedFlags,
    List<string> PositiveIndicators
);
