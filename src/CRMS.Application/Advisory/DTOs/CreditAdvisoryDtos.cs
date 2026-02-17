namespace CRMS.Application.Advisory.DTOs;

public record CreditAdvisoryDto(
    Guid Id,
    Guid LoanApplicationId,
    string Status,
    
    // Overall Assessment
    decimal OverallScore,
    string OverallRating,
    string Recommendation,
    
    // Risk Scores
    List<RiskScoreDto> RiskScores,
    
    // Loan Recommendations
    decimal? RecommendedAmount,
    int? RecommendedTenorMonths,
    decimal? RecommendedInterestRate,
    decimal? MaxExposure,
    
    // Conditions
    List<string> Conditions,
    List<string> Covenants,
    
    // Analysis
    string? ExecutiveSummary,
    string? StrengthsAnalysis,
    string? WeaknessesAnalysis,
    string? MitigatingFactors,
    string? KeyRisks,
    
    // Red Flags
    List<string> RedFlags,
    bool HasCriticalRedFlags,
    
    // Audit
    string ModelVersion,
    DateTime GeneratedAt,
    string? ErrorMessage
);

public record RiskScoreDto(
    string Category,
    decimal Score,
    decimal Weight,
    decimal WeightedScore,
    string Rating,
    string Rationale,
    List<string> RedFlags,
    List<string> PositiveIndicators
);

public record CreditAdvisorySummaryDto(
    Guid Id,
    Guid LoanApplicationId,
    string Status,
    decimal OverallScore,
    string OverallRating,
    string Recommendation,
    bool HasCriticalRedFlags,
    int RedFlagsCount,
    DateTime GeneratedAt
);

public record ScoreMatrixDto(
    Guid AdvisoryId,
    decimal CreditHistoryScore,
    decimal FinancialHealthScore,
    decimal CashflowScore,
    decimal DSCRScore,
    decimal? CollateralScore,
    decimal? ManagementScore,
    decimal OverallScore,
    string OverallRating
);
