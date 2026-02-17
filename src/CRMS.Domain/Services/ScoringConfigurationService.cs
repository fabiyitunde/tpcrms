using CRMS.Domain.Configuration;
using CRMS.Domain.Interfaces;

namespace CRMS.Domain.Services;

/// <summary>
/// Service that loads scoring configuration from database.
/// Falls back to default values if parameters are not yet configured.
/// </summary>
public class ScoringConfigurationService
{
    private readonly IScoringParameterRepository _repository;

    public ScoringConfigurationService(IScoringParameterRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Load the current active scoring configuration from database.
    /// </summary>
    public async Task<ScoringConfiguration> LoadConfigurationAsync(CancellationToken ct = default)
    {
        var parameters = await _repository.GetAllActiveAsLookupAsync(ct);
        var config = new ScoringConfiguration();

        // Load weights
        config.Weights.CreditHistory = GetValue(parameters, "Weights", "CreditHistory", 0.25m);
        config.Weights.FinancialHealth = GetValue(parameters, "Weights", "FinancialHealth", 0.25m);
        config.Weights.CashflowStability = GetValue(parameters, "Weights", "CashflowStability", 0.15m);
        config.Weights.DebtServiceCapacity = GetValue(parameters, "Weights", "DebtServiceCapacity", 0.20m);
        config.Weights.CollateralCoverage = GetValue(parameters, "Weights", "CollateralCoverage", 0.15m);

        // Load Credit History config
        config.CreditHistory.BaseScore = GetValue(parameters, "CreditHistory", "BaseScore", 70m);
        config.CreditHistory.ExcellentCreditScoreThreshold = (int)GetValue(parameters, "CreditHistory", "ExcellentCreditScoreThreshold", 700m);
        config.CreditHistory.GoodCreditScoreThreshold = (int)GetValue(parameters, "CreditHistory", "GoodCreditScoreThreshold", 650m);
        config.CreditHistory.PoorCreditScoreThreshold = (int)GetValue(parameters, "CreditHistory", "PoorCreditScoreThreshold", 600m);
        config.CreditHistory.ExcellentCreditScoreBonus = GetValue(parameters, "CreditHistory", "ExcellentCreditScoreBonus", 20m);
        config.CreditHistory.GoodCreditScoreBonus = GetValue(parameters, "CreditHistory", "GoodCreditScoreBonus", 10m);
        config.CreditHistory.PoorCreditScorePenalty = GetValue(parameters, "CreditHistory", "PoorCreditScorePenalty", 20m);
        config.CreditHistory.DefaultPenalty = GetValue(parameters, "CreditHistory", "DefaultPenalty", 30m);
        config.CreditHistory.DelinquencyPenalty = GetValue(parameters, "CreditHistory", "DelinquencyPenalty", 15m);
        config.CreditHistory.PerformingLoansBonus = GetValue(parameters, "CreditHistory", "PerformingLoansBonus", 5m);
        config.CreditHistory.MinPerformingLoansForBonus = (int)GetValue(parameters, "CreditHistory", "MinPerformingLoansForBonus", 3m);

        // Load Financial Health config
        config.FinancialHealth.BaseScore = GetValue(parameters, "FinancialHealth", "BaseScore", 60m);
        config.FinancialHealth.StrongCurrentRatio = GetValue(parameters, "FinancialHealth", "StrongCurrentRatio", 2.0m);
        config.FinancialHealth.WeakCurrentRatio = GetValue(parameters, "FinancialHealth", "WeakCurrentRatio", 1.0m);
        config.FinancialHealth.StrongCurrentRatioBonus = GetValue(parameters, "FinancialHealth", "StrongCurrentRatioBonus", 10m);
        config.FinancialHealth.WeakCurrentRatioPenalty = GetValue(parameters, "FinancialHealth", "WeakCurrentRatioPenalty", 15m);
        config.FinancialHealth.ConservativeDebtToEquity = GetValue(parameters, "FinancialHealth", "ConservativeDebtToEquity", 1.0m);
        config.FinancialHealth.HighDebtToEquity = GetValue(parameters, "FinancialHealth", "HighDebtToEquity", 3.0m);
        config.FinancialHealth.ConservativeLeverageBonus = GetValue(parameters, "FinancialHealth", "ConservativeLeverageBonus", 10m);
        config.FinancialHealth.HighLeveragePenalty = GetValue(parameters, "FinancialHealth", "HighLeveragePenalty", 20m);
        config.FinancialHealth.StrongNetMarginPercent = GetValue(parameters, "FinancialHealth", "StrongNetMarginPercent", 10m);
        config.FinancialHealth.StrongNetMarginBonus = GetValue(parameters, "FinancialHealth", "StrongNetMarginBonus", 15m);
        config.FinancialHealth.LossMakingPenalty = GetValue(parameters, "FinancialHealth", "LossMakingPenalty", 25m);
        config.FinancialHealth.StrongROE = GetValue(parameters, "FinancialHealth", "StrongROE", 15m);
        config.FinancialHealth.StrongROEBonus = GetValue(parameters, "FinancialHealth", "StrongROEBonus", 10m);

        // Load Cashflow config
        config.Cashflow.BaseScore = GetValue(parameters, "Cashflow", "BaseScore", 60m);
        config.Cashflow.InternalStatementBonus = GetValue(parameters, "Cashflow", "InternalStatementBonus", 10m);
        config.Cashflow.MissingInternalPenalty = GetValue(parameters, "Cashflow", "MissingInternalPenalty", 15m);
        config.Cashflow.VerifiedExternalBonus = GetValue(parameters, "Cashflow", "VerifiedExternalBonus", 5m);
        config.Cashflow.PositiveCashflowBonus = GetValue(parameters, "Cashflow", "PositiveCashflowBonus", 15m);
        config.Cashflow.NegativeCashflowPenalty = GetValue(parameters, "Cashflow", "NegativeCashflowPenalty", 20m);
        config.Cashflow.LowVolatilityThreshold = GetValue(parameters, "Cashflow", "LowVolatilityThreshold", 0.3m);
        config.Cashflow.HighVolatilityThreshold = GetValue(parameters, "Cashflow", "HighVolatilityThreshold", 0.5m);
        config.Cashflow.LowVolatilityBonus = GetValue(parameters, "Cashflow", "LowVolatilityBonus", 10m);
        config.Cashflow.HighVolatilityPenalty = GetValue(parameters, "Cashflow", "HighVolatilityPenalty", 10m);
        config.Cashflow.GamblingPenalty = GetValue(parameters, "Cashflow", "GamblingPenalty", 15m);
        config.Cashflow.BouncedTransactionPenalty = GetValue(parameters, "Cashflow", "BouncedTransactionPenalty", 20m);
        config.Cashflow.HighNegativeBalanceDaysThreshold = (int)GetValue(parameters, "Cashflow", "HighNegativeBalanceDaysThreshold", 10m);
        config.Cashflow.ModerateNegativeBalanceDaysThreshold = (int)GetValue(parameters, "Cashflow", "ModerateNegativeBalanceDaysThreshold", 5m);
        config.Cashflow.HighNegativeBalancePenalty = GetValue(parameters, "Cashflow", "HighNegativeBalancePenalty", 15m);
        config.Cashflow.ModerateNegativeBalancePenalty = GetValue(parameters, "Cashflow", "ModerateNegativeBalancePenalty", 5m);
        config.Cashflow.MinimumMonthsRequired = (int)GetValue(parameters, "Cashflow", "MinimumMonthsRequired", 6m);
        config.Cashflow.IdealMonthsCoverage = (int)GetValue(parameters, "Cashflow", "IdealMonthsCoverage", 12m);
        config.Cashflow.InsufficientCoveragePenalty = GetValue(parameters, "Cashflow", "InsufficientCoveragePenalty", 10m);
        config.Cashflow.IdealCoverageBonus = GetValue(parameters, "Cashflow", "IdealCoverageBonus", 5m);

        // Load DSCR config
        config.DSCR.ExcellentDSCR = GetValue(parameters, "DSCR", "ExcellentDSCR", 2.0m);
        config.DSCR.GoodDSCR = GetValue(parameters, "DSCR", "GoodDSCR", 1.5m);
        config.DSCR.AdequateDSCR = GetValue(parameters, "DSCR", "AdequateDSCR", 1.25m);
        config.DSCR.MinimumDSCR = GetValue(parameters, "DSCR", "MinimumDSCR", 1.0m);
        config.DSCR.ExcellentDSCRScore = GetValue(parameters, "DSCR", "ExcellentDSCRScore", 90m);
        config.DSCR.GoodDSCRScore = GetValue(parameters, "DSCR", "GoodDSCRScore", 75m);
        config.DSCR.AdequateDSCRScore = GetValue(parameters, "DSCR", "AdequateDSCRScore", 60m);
        config.DSCR.MinimumDSCRScore = GetValue(parameters, "DSCR", "MinimumDSCRScore", 45m);
        config.DSCR.BelowMinimumDSCRScore = GetValue(parameters, "DSCR", "BelowMinimumDSCRScore", 25m);
        config.DSCR.StrongInterestCoverage = GetValue(parameters, "DSCR", "StrongInterestCoverage", 5.0m);
        config.DSCR.WeakInterestCoverage = GetValue(parameters, "DSCR", "WeakInterestCoverage", 2.0m);
        config.DSCR.StrongInterestCoverageBonus = GetValue(parameters, "DSCR", "StrongInterestCoverageBonus", 5m);
        config.DSCR.WeakInterestCoveragePenalty = GetValue(parameters, "DSCR", "WeakInterestCoveragePenalty", 10m);

        // Load Collateral config
        config.Collateral.ExcellentLTV = GetValue(parameters, "Collateral", "ExcellentLTV", 50m);
        config.Collateral.GoodLTV = GetValue(parameters, "Collateral", "GoodLTV", 70m);
        config.Collateral.AdequateLTV = GetValue(parameters, "Collateral", "AdequateLTV", 100m);
        config.Collateral.ExcellentLTVScore = GetValue(parameters, "Collateral", "ExcellentLTVScore", 90m);
        config.Collateral.GoodLTVScore = GetValue(parameters, "Collateral", "GoodLTVScore", 75m);
        config.Collateral.AdequateLTVScore = GetValue(parameters, "Collateral", "AdequateLTVScore", 55m);
        config.Collateral.UnderCollateralizedScore = GetValue(parameters, "Collateral", "UnderCollateralizedScore", 35m);
        config.Collateral.PerfectedLienBonus = GetValue(parameters, "Collateral", "PerfectedLienBonus", 5m);
        config.Collateral.UnperfectedLienPenalty = GetValue(parameters, "Collateral", "UnperfectedLienPenalty", 10m);

        // Load Recommendation thresholds
        config.Recommendations.StrongApproveMinScore = GetValue(parameters, "Recommendations", "StrongApproveMinScore", 75m);
        config.Recommendations.StrongApproveMaxRedFlags = (int)GetValue(parameters, "Recommendations", "StrongApproveMaxRedFlags", 0m);
        config.Recommendations.ApproveMinScore = GetValue(parameters, "Recommendations", "ApproveMinScore", 65m);
        config.Recommendations.ApproveMaxRedFlags = (int)GetValue(parameters, "Recommendations", "ApproveMaxRedFlags", 1m);
        config.Recommendations.ApproveWithConditionsMinScore = GetValue(parameters, "Recommendations", "ApproveWithConditionsMinScore", 50m);
        config.Recommendations.ReferMinScore = GetValue(parameters, "Recommendations", "ReferMinScore", 35m);
        config.Recommendations.CriticalRedFlagsThreshold = (int)GetValue(parameters, "Recommendations", "CriticalRedFlagsThreshold", 3m);

        // Load Loan Adjustment config
        config.LoanAdjustments.Score80PlusMultiplier = GetValue(parameters, "LoanAdjustments", "Score80PlusMultiplier", 1.0m);
        config.LoanAdjustments.Score70PlusMultiplier = GetValue(parameters, "LoanAdjustments", "Score70PlusMultiplier", 0.9m);
        config.LoanAdjustments.Score60PlusMultiplier = GetValue(parameters, "LoanAdjustments", "Score60PlusMultiplier", 0.75m);
        config.LoanAdjustments.Score50PlusMultiplier = GetValue(parameters, "LoanAdjustments", "Score50PlusMultiplier", 0.6m);
        config.LoanAdjustments.BelowScore50Multiplier = GetValue(parameters, "LoanAdjustments", "BelowScore50Multiplier", 0.5m);
        config.LoanAdjustments.BaseInterestRate = GetValue(parameters, "LoanAdjustments", "BaseInterestRate", 18.0m);
        config.LoanAdjustments.Score80PlusRateAdjustment = GetValue(parameters, "LoanAdjustments", "Score80PlusRateAdjustment", -2.0m);
        config.LoanAdjustments.Score70PlusRateAdjustment = GetValue(parameters, "LoanAdjustments", "Score70PlusRateAdjustment", -1.0m);
        config.LoanAdjustments.Score60PlusRateAdjustment = GetValue(parameters, "LoanAdjustments", "Score60PlusRateAdjustment", 0m);
        config.LoanAdjustments.Score50PlusRateAdjustment = GetValue(parameters, "LoanAdjustments", "Score50PlusRateAdjustment", 2.0m);
        config.LoanAdjustments.BelowScore50RateAdjustment = GetValue(parameters, "LoanAdjustments", "BelowScore50RateAdjustment", 4.0m);
        config.LoanAdjustments.MaxTenorForLowScores = (int)GetValue(parameters, "LoanAdjustments", "MaxTenorForLowScores", 36m);
        config.LoanAdjustments.LowScoreThresholdForTenorRestriction = GetValue(parameters, "LoanAdjustments", "LowScoreThresholdForTenorRestriction", 70m);

        // Load Statement Trust weights
        config.StatementTrust.CoreBanking = GetValue(parameters, "StatementTrust", "CoreBanking", 1.0m);
        config.StatementTrust.OpenBanking = GetValue(parameters, "StatementTrust", "OpenBanking", 0.95m);
        config.StatementTrust.MonoConnect = GetValue(parameters, "StatementTrust", "MonoConnect", 0.90m);
        config.StatementTrust.ManualUploadVerified = GetValue(parameters, "StatementTrust", "ManualUploadVerified", 0.85m);
        config.StatementTrust.ManualUploadPending = GetValue(parameters, "StatementTrust", "ManualUploadPending", 0.70m);

        return config;
    }

    private static decimal GetValue(Dictionary<string, decimal> parameters, string category, string key, decimal defaultValue)
    {
        var fullKey = $"{category}.{key}";
        return parameters.TryGetValue(fullKey, out var value) ? value : defaultValue;
    }
}
