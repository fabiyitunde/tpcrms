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
}

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
    int ExistingFacilitiesCount
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
    DateTime ReportDate
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
    string OverallAssessment
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
    bool HasPerfectedLiens
);

public record GuarantorDataInput(
    Guid GuarantorId,
    string Name,
    string Type, // Individual, Corporate
    decimal NetWorth,
    decimal GuaranteeAmount,
    int? CreditScore,
    string CreditStatus
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
