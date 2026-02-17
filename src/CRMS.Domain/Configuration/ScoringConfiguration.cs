namespace CRMS.Domain.Configuration;

/// <summary>
/// Configuration for AI Advisory scoring weights and thresholds.
/// These parameters can be adjusted by the bank without code changes.
/// </summary>
public class ScoringConfiguration
{
    public const string SectionName = "Scoring";

    // Category Weights (must sum to 1.0)
    public CategoryWeights Weights { get; set; } = new();

    // Credit History Scoring Parameters
    public CreditHistoryConfig CreditHistory { get; set; } = new();

    // Financial Health Scoring Parameters
    public FinancialHealthConfig FinancialHealth { get; set; } = new();

    // Cashflow Scoring Parameters
    public CashflowConfig Cashflow { get; set; } = new();

    // DSCR Scoring Parameters
    public DSCRConfig DSCR { get; set; } = new();

    // Collateral Scoring Parameters
    public CollateralConfig Collateral { get; set; } = new();

    // Recommendation Thresholds
    public RecommendationThresholds Recommendations { get; set; } = new();

    // Loan Adjustment Parameters
    public LoanAdjustmentConfig LoanAdjustments { get; set; } = new();

    // Trust Weights for Statement Sources
    public StatementTrustWeights StatementTrust { get; set; } = new();
}

public class CategoryWeights
{
    public decimal CreditHistory { get; set; } = 0.25m;
    public decimal FinancialHealth { get; set; } = 0.25m;
    public decimal CashflowStability { get; set; } = 0.15m;
    public decimal DebtServiceCapacity { get; set; } = 0.20m;
    public decimal CollateralCoverage { get; set; } = 0.15m;
}

public class CreditHistoryConfig
{
    public decimal BaseScore { get; set; } = 70m;
    
    // Credit Score Thresholds
    public int ExcellentCreditScoreThreshold { get; set; } = 700;
    public int GoodCreditScoreThreshold { get; set; } = 650;
    public int PoorCreditScoreThreshold { get; set; } = 600;
    
    // Score Adjustments
    public decimal ExcellentCreditScoreBonus { get; set; } = 20m;
    public decimal GoodCreditScoreBonus { get; set; } = 10m;
    public decimal PoorCreditScorePenalty { get; set; } = 20m;
    public decimal DefaultPenalty { get; set; } = 30m;
    public decimal DelinquencyPenalty { get; set; } = 15m;
    public decimal PerformingLoansBonus { get; set; } = 5m;
    public int MinPerformingLoansForBonus { get; set; } = 3;
}

public class FinancialHealthConfig
{
    public decimal BaseScore { get; set; } = 60m;
    
    // Liquidity Thresholds
    public decimal StrongCurrentRatio { get; set; } = 2.0m;
    public decimal WeakCurrentRatio { get; set; } = 1.0m;
    public decimal StrongCurrentRatioBonus { get; set; } = 10m;
    public decimal WeakCurrentRatioPenalty { get; set; } = 15m;
    
    // Leverage Thresholds
    public decimal ConservativeDebtToEquity { get; set; } = 1.0m;
    public decimal HighDebtToEquity { get; set; } = 3.0m;
    public decimal ConservativeLeverageBonus { get; set; } = 10m;
    public decimal HighLeveragePenalty { get; set; } = 20m;
    
    // Profitability Thresholds
    public decimal StrongNetMarginPercent { get; set; } = 10m;
    public decimal StrongNetMarginBonus { get; set; } = 15m;
    public decimal LossMakingPenalty { get; set; } = 25m;
    
    // ROE
    public decimal StrongROE { get; set; } = 15m;
    public decimal StrongROEBonus { get; set; } = 10m;
}

public class CashflowConfig
{
    public decimal BaseScore { get; set; } = 60m;
    
    // Statement Source Bonuses/Penalties
    public decimal InternalStatementBonus { get; set; } = 10m;
    public decimal MissingInternalPenalty { get; set; } = 15m;
    public decimal VerifiedExternalBonus { get; set; } = 5m;
    
    // Cashflow Metrics
    public decimal PositiveCashflowBonus { get; set; } = 15m;
    public decimal NegativeCashflowPenalty { get; set; } = 20m;
    
    // Volatility Thresholds
    public decimal LowVolatilityThreshold { get; set; } = 0.3m;
    public decimal HighVolatilityThreshold { get; set; } = 0.5m;
    public decimal LowVolatilityBonus { get; set; } = 10m;
    public decimal HighVolatilityPenalty { get; set; } = 10m;
    
    // Risk Indicators
    public decimal GamblingPenalty { get; set; } = 15m;
    public decimal BouncedTransactionPenalty { get; set; } = 20m;
    public int HighNegativeBalanceDaysThreshold { get; set; } = 10;
    public int ModerateNegativeBalanceDaysThreshold { get; set; } = 5;
    public decimal HighNegativeBalancePenalty { get; set; } = 15m;
    public decimal ModerateNegativeBalancePenalty { get; set; } = 5m;
    
    // Period Coverage
    public int MinimumMonthsRequired { get; set; } = 6;
    public int IdealMonthsCoverage { get; set; } = 12;
    public decimal InsufficientCoveragePenalty { get; set; } = 10m;
    public decimal IdealCoverageBonus { get; set; } = 5m;
}

public class DSCRConfig
{
    public decimal ExcellentDSCR { get; set; } = 2.0m;
    public decimal GoodDSCR { get; set; } = 1.5m;
    public decimal AdequateDSCR { get; set; } = 1.25m;
    public decimal MinimumDSCR { get; set; } = 1.0m;
    
    public decimal ExcellentDSCRScore { get; set; } = 90m;
    public decimal GoodDSCRScore { get; set; } = 75m;
    public decimal AdequateDSCRScore { get; set; } = 60m;
    public decimal MinimumDSCRScore { get; set; } = 45m;
    public decimal BelowMinimumDSCRScore { get; set; } = 25m;
    
    // Interest Coverage
    public decimal StrongInterestCoverage { get; set; } = 5.0m;
    public decimal WeakInterestCoverage { get; set; } = 2.0m;
    public decimal StrongInterestCoverageBonus { get; set; } = 5m;
    public decimal WeakInterestCoveragePenalty { get; set; } = 10m;
}

public class CollateralConfig
{
    // LTV Thresholds
    public decimal ExcellentLTV { get; set; } = 50m;
    public decimal GoodLTV { get; set; } = 70m;
    public decimal AdequateLTV { get; set; } = 100m;
    
    public decimal ExcellentLTVScore { get; set; } = 90m;
    public decimal GoodLTVScore { get; set; } = 75m;
    public decimal AdequateLTVScore { get; set; } = 55m;
    public decimal UnderCollateralizedScore { get; set; } = 35m;
    
    // Lien Status
    public decimal PerfectedLienBonus { get; set; } = 5m;
    public decimal UnperfectedLienPenalty { get; set; } = 10m;
}

public class RecommendationThresholds
{
    // Score thresholds for recommendations
    public decimal StrongApproveMinScore { get; set; } = 75m;
    public int StrongApproveMaxRedFlags { get; set; } = 0;
    
    public decimal ApproveMinScore { get; set; } = 65m;
    public int ApproveMaxRedFlags { get; set; } = 1;
    
    public decimal ApproveWithConditionsMinScore { get; set; } = 50m;
    
    public decimal ReferMinScore { get; set; } = 35m;
    
    // Below ReferMinScore = Decline
    
    // Critical red flags threshold
    public int CriticalRedFlagsThreshold { get; set; } = 3;
}

public class LoanAdjustmentConfig
{
    // Amount multipliers based on score
    public decimal Score80PlusMultiplier { get; set; } = 1.0m;
    public decimal Score70PlusMultiplier { get; set; } = 0.9m;
    public decimal Score60PlusMultiplier { get; set; } = 0.75m;
    public decimal Score50PlusMultiplier { get; set; } = 0.6m;
    public decimal BelowScore50Multiplier { get; set; } = 0.5m;
    
    // Interest rate adjustments (added to base rate)
    public decimal BaseInterestRate { get; set; } = 18.0m;
    public decimal Score80PlusRateAdjustment { get; set; } = -2.0m;
    public decimal Score70PlusRateAdjustment { get; set; } = -1.0m;
    public decimal Score60PlusRateAdjustment { get; set; } = 0m;
    public decimal Score50PlusRateAdjustment { get; set; } = 2.0m;
    public decimal BelowScore50RateAdjustment { get; set; } = 4.0m;
    
    // Tenor restrictions
    public int MaxTenorForLowScores { get; set; } = 36;
    public decimal LowScoreThresholdForTenorRestriction { get; set; } = 70m;
}

public class StatementTrustWeights
{
    public decimal CoreBanking { get; set; } = 1.0m;
    public decimal OpenBanking { get; set; } = 0.95m;
    public decimal MonoConnect { get; set; } = 0.90m;
    public decimal ManualUploadVerified { get; set; } = 0.85m;
    public decimal ManualUploadPending { get; set; } = 0.70m;
}
