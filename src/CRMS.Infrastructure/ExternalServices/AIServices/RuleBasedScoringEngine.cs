using CRMS.Application.Advisory.Interfaces;
using CRMS.Domain.Configuration;

namespace CRMS.Infrastructure.ExternalServices.AIServices;

/// <summary>
/// Deterministic rule-based scoring engine for credit advisory.
/// Extracted from MockAIAdvisoryService for use in hybrid architecture.
/// All scores are calculated using configurable thresholds from database.
/// </summary>
public class RuleBasedScoringEngine
{
    /// <summary>
    /// Result of rule-based scoring calculation.
    /// </summary>
    public record ScoringResult(
        List<RiskScoreOutput> RiskScores,
        decimal OverallScore,
        string OverallRating,
        string Recommendation,
        List<string> RedFlags,
        List<string> Conditions,
        List<string> Covenants,
        decimal? RecommendedAmount,
        int? RecommendedTenorMonths,
        decimal? RecommendedInterestRate,
        decimal? MaxExposure
    );

    public ScoringResult CalculateScores(AIAdvisoryRequest request, ScoringConfiguration config)
    {
        var riskScores = new List<RiskScoreOutput>();
        var redFlags = new List<string>();
        var conditions = new List<string>();
        var covenants = new List<string>();

        // Calculate all category scores
        var creditHistoryScore = CalculateCreditHistoryScore(request.BureauReports, redFlags, config);
        riskScores.Add(creditHistoryScore);

        var financialHealthScore = CalculateFinancialHealthScore(request.FinancialStatements, redFlags, config);
        riskScores.Add(financialHealthScore);

        var cashflowScore = CalculateCashflowScore(request.CashflowAnalysis, redFlags, config);
        riskScores.Add(cashflowScore);

        var dscrScore = CalculateDSCRScore(request.FinancialStatements, request.RequestedAmount, redFlags, config);
        riskScores.Add(dscrScore);

        if (request.CollateralSummary != null)
        {
            var collateralScore = CalculateCollateralScore(request.CollateralSummary, request.RequestedAmount, redFlags, config);
            riskScores.Add(collateralScore);
        }

        // Calculate overall score
        var totalWeight = riskScores.Sum(s => s.Weight);
        var overallScore = totalWeight > 0
            ? Math.Round(riskScores.Sum(s => s.Score * s.Weight) / totalWeight, 2)
            : 50m;

        var overallRating = DetermineRating(overallScore);
        var recommendation = DetermineRecommendation(overallScore, redFlags.Count, config);

        // Generate loan recommendations
        var (recAmount, recTenor, recRate, maxExposure) = GenerateLoanRecommendations(
            request.RequestedAmount,
            request.RequestedTenorMonths,
            overallScore,
            recommendation,
            config);

        // Generate conditions based on risk level
        GenerateConditions(overallScore, redFlags, conditions, covenants);

        return new ScoringResult(
            riskScores,
            overallScore,
            overallRating,
            recommendation,
            redFlags,
            conditions,
            covenants,
            recAmount,
            recTenor,
            recRate,
            maxExposure
        );
    }

    private RiskScoreOutput CalculateCreditHistoryScore(List<BureauDataInput> bureauReports, List<string> redFlags, ScoringConfiguration config)
    {
        var cfg = config.CreditHistory;
        var weight = config.Weights.CreditHistory;

        if (!bureauReports.Any())
        {
            redFlags.Add("No credit bureau data available for assessment");
            return new RiskScoreOutput(
                "CreditHistory", 50, weight, "Medium",
                "Unable to assess credit history due to missing bureau data",
                new List<string> { "No bureau data" },
                new List<string>()
            );
        }

        var positiveIndicators = new List<string>();
        var scoreRedFlags = new List<string>();

        var reportsWithScores = bureauReports.Where(b => b.CreditScore.HasValue).ToList();
        var avgScore = reportsWithScores.Any() ? reportsWithScores.Average(b => b.CreditScore!.Value) : 0;

        var totalDefaults = bureauReports.Sum(b => b.DefaultedLoansCount);
        var totalDelinquent = bureauReports.Sum(b => b.DelinquentLoansCount);
        var totalPerforming = bureauReports.Sum(b => b.PerformingLoansCount);

        decimal score = cfg.BaseScore;

        if (reportsWithScores.Any())
        {
            if (avgScore >= cfg.ExcellentCreditScoreThreshold)
            {
                score += cfg.ExcellentCreditScoreBonus;
                positiveIndicators.Add($"Strong average credit score of {avgScore:N0}");
            }
            else if (avgScore >= cfg.GoodCreditScoreThreshold)
            {
                score += cfg.GoodCreditScoreBonus;
                positiveIndicators.Add($"Good average credit score of {avgScore:N0}");
            }
            else if (avgScore < cfg.PoorCreditScoreThreshold)
            {
                score -= cfg.PoorCreditScorePenalty;
                scoreRedFlags.Add($"Low average credit score of {avgScore:N0}");
                redFlags.Add($"Low credit scores detected (avg: {avgScore:N0})");
            }
        }

        if (totalDefaults > 0)
        {
            score -= cfg.DefaultPenalty;
            scoreRedFlags.Add($"{totalDefaults} defaulted loans on record");
            redFlags.Add($"Credit defaults detected: {totalDefaults} across related parties");
        }

        if (totalDelinquent > 0)
        {
            score -= cfg.DelinquencyPenalty;
            scoreRedFlags.Add($"{totalDelinquent} delinquent loans");
        }

        if (totalPerforming >= cfg.MinPerformingLoansForBonus)
        {
            score += cfg.PerformingLoansBonus;
            positiveIndicators.Add($"{totalPerforming} performing loan facilities indicate good repayment history");
        }

        var hasLegal = bureauReports.Any(b => b.HasLegalActions);
        if (hasLegal)
        {
            score -= cfg.LegalActionsPenalty;
            scoreRedFlags.Add("Legal actions recorded against one or more parties");
            redFlags.Add("Legal actions detected in credit bureau records");
        }

        var maxDays = bureauReports.Max(b => b.MaxDelinquencyDays);
        if (maxDays >= cfg.SevereDelinquencyDaysThreshold)
        {
            score -= cfg.SevereDelinquencyPenalty;
            scoreRedFlags.Add($"Severe delinquency: up to {maxDays} days overdue");
        }
        else if (maxDays >= cfg.WatchListDaysThreshold)
        {
            score -= cfg.WatchListPenalty;
            scoreRedFlags.Add($"Delinquency of {maxDays} days recorded");
        }

        var highFraud = bureauReports.Where(b => b.FraudRiskScore.HasValue).ToList();
        if (highFraud.Any(b => b.FraudRiskScore >= cfg.HighFraudRiskScoreThreshold))
        {
            score -= cfg.HighFraudRiskPenalty;
            scoreRedFlags.Add("High fraud risk score flagged by bureau");
            redFlags.Add("High fraud risk score detected — manual verification required");
        }
        else if (highFraud.Any(b => b.FraudRiskScore >= cfg.ElevatedFraudRiskScoreThreshold))
        {
            score -= cfg.ElevatedFraudRiskPenalty;
            scoreRedFlags.Add("Elevated fraud risk score");
        }

        var missingCount = bureauReports.Count(b => b.IsPlaceholder);
        if (missingCount > 0)
        {
            score -= missingCount * cfg.MissingBureauDataPenaltyPerParty;
            scoreRedFlags.Add($"Bureau data unavailable for {missingCount} party(ies)");
        }

        score = Math.Clamp(score, 0, 100);

        var realReportCount = bureauReports.Count(b => !b.IsPlaceholder);
        return new RiskScoreOutput(
            "CreditHistory",
            score,
            weight,
            DetermineRating(score),
            $"Credit history assessment based on {realReportCount} bureau report(s) ({missingCount} missing). " +
            $"Average credit score: {avgScore:N0}. Performing: {totalPerforming}, Delinquent: {totalDelinquent}, Defaulted: {totalDefaults}. " +
            $"Max delinquency: {maxDays} days. Legal actions: {(hasLegal ? "Yes" : "No")}.",
            scoreRedFlags,
            positiveIndicators
        );
    }

    private RiskScoreOutput CalculateFinancialHealthScore(List<FinancialDataInput> statements, List<string> redFlags, ScoringConfiguration config)
    {
        var cfg = config.FinancialHealth;
        var weight = config.Weights.FinancialHealth;

        if (!statements.Any())
        {
            redFlags.Add("No financial statements available for assessment");
            return new RiskScoreOutput(
                "FinancialHealth", 50, weight, "Medium",
                "Unable to assess financial health due to missing financial statements",
                new List<string> { "No financial data" },
                new List<string>()
            );
        }

        var latest = statements.OrderByDescending(s => s.Year).First();
        var positiveIndicators = new List<string>();
        var scoreRedFlags = new List<string>();

        decimal score = cfg.BaseScore;

        if (latest.CurrentRatio >= cfg.StrongCurrentRatio)
        {
            score += cfg.StrongCurrentRatioBonus;
            positiveIndicators.Add($"Strong liquidity position (Current Ratio: {latest.CurrentRatio:N2})");
        }
        else if (latest.CurrentRatio < cfg.WeakCurrentRatio)
        {
            score -= cfg.WeakCurrentRatioPenalty;
            scoreRedFlags.Add($"Weak liquidity (Current Ratio: {latest.CurrentRatio:N2})");
            redFlags.Add("Liquidity concerns - Current ratio below 1.0");
        }

        if (latest.DebtToEquityRatio <= cfg.ConservativeDebtToEquity)
        {
            score += cfg.ConservativeLeverageBonus;
            positiveIndicators.Add($"Conservative leverage (D/E: {latest.DebtToEquityRatio:N2})");
        }
        else if (latest.DebtToEquityRatio > cfg.HighDebtToEquity)
        {
            score -= cfg.HighLeveragePenalty;
            scoreRedFlags.Add($"High leverage (D/E: {latest.DebtToEquityRatio:N2})");
            redFlags.Add($"High debt-to-equity ratio of {latest.DebtToEquityRatio:N2}");
        }

        if (latest.NetProfitMarginPercent >= cfg.StrongNetMarginPercent)
        {
            score += cfg.StrongNetMarginBonus;
            positiveIndicators.Add($"Strong profitability (Net Margin: {latest.NetProfitMarginPercent:N1}%)");
        }
        else if (latest.NetProfitMarginPercent < 0)
        {
            score -= cfg.LossMakingPenalty;
            scoreRedFlags.Add($"Loss-making operation (Net Margin: {latest.NetProfitMarginPercent:N1}%)");
            redFlags.Add("Company is currently loss-making");
        }

        if (latest.ReturnOnEquity >= cfg.StrongROE)
        {
            score += cfg.StrongROEBonus;
            positiveIndicators.Add($"Strong return on equity ({latest.ReturnOnEquity:N1}%)");
        }

        score = Math.Clamp(score, 0, 100);

        return new RiskScoreOutput(
            "FinancialHealth",
            score,
            weight,
            DetermineRating(score),
            $"Financial health assessment for FY{latest.Year}. Overall: {latest.OverallAssessment}. " +
            $"Liquidity: {latest.LiquidityAssessment}, Leverage: {latest.LeverageAssessment}, " +
            $"Profitability: {latest.ProfitabilityAssessment}.",
            scoreRedFlags,
            positiveIndicators
        );
    }

    private RiskScoreOutput CalculateCashflowScore(CashflowDataInput? cashflow, List<string> redFlags, ScoringConfiguration config)
    {
        var cfg = config.Cashflow;
        var weight = config.Weights.CashflowStability;

        if (cashflow == null)
        {
            redFlags.Add("No bank statement analysis available");
            return new RiskScoreOutput(
                "CashflowStability", 50, weight, "Medium",
                "Cashflow analysis not available - bank statements required for complete assessment",
                new List<string> { "No bank statements analyzed" },
                new List<string>()
            );
        }

        var positiveIndicators = new List<string>();
        var scoreRedFlags = new List<string>();
        decimal score = cfg.BaseScore;

        if (cashflow.HasInternalStatement)
        {
            score += cfg.InternalStatementBonus;
            positiveIndicators.Add("Internal bank statement (from our core banking) provided");
        }
        else
        {
            score -= cfg.MissingInternalPenalty;
            scoreRedFlags.Add("No internal bank statement - analysis based on external statements only");
            redFlags.Add("Missing internal bank statement reduces reliability of cashflow analysis");
        }

        if (cashflow.ExternalStatementsCount > 0 && cashflow.AllExternalStatementsVerified)
        {
            score += cfg.VerifiedExternalBonus;
            positiveIndicators.Add($"{cashflow.ExternalStatementsCount} external statement(s) verified");
        }
        else if (cashflow.ExternalStatementsCount > 0 && !cashflow.AllExternalStatementsVerified)
        {
            scoreRedFlags.Add("Some external statements pending verification");
        }

        if (cashflow.NetMonthlyCashflow > 0)
        {
            score += cfg.PositiveCashflowBonus;
            positiveIndicators.Add($"Positive net monthly cashflow of {cashflow.NetMonthlyCashflow:N0}");
        }
        else
        {
            score -= cfg.NegativeCashflowPenalty;
            scoreRedFlags.Add($"Negative net monthly cashflow of {cashflow.NetMonthlyCashflow:N0}");
            redFlags.Add("Negative monthly cashflow detected in bank statements");
        }

        if (cashflow.CashflowVolatility < cfg.LowVolatilityThreshold)
        {
            score += cfg.LowVolatilityBonus;
            positiveIndicators.Add("Low cashflow volatility indicates stable operations");
        }
        else if (cashflow.CashflowVolatility > cfg.HighVolatilityThreshold)
        {
            score -= cfg.HighVolatilityPenalty;
            scoreRedFlags.Add($"High cashflow volatility ({cashflow.CashflowVolatility:P0})");
        }

        if (cashflow.HasSalaryCredits && cashflow.DetectedMonthlySalary.HasValue)
        {
            positiveIndicators.Add($"Regular salary credits detected: {cashflow.DetectedMonthlySalary:N0}/month from {cashflow.SalarySource ?? "employer"}");
        }

        if (cashflow.GamblingTransactionCount > 0)
        {
            score -= cfg.GamblingPenalty;
            scoreRedFlags.Add($"Gambling transactions detected: {cashflow.GamblingTransactionCount} transactions totaling {cashflow.GamblingTransactionTotal:N0}");
            redFlags.Add($"Gambling activity: {cashflow.GamblingTransactionCount} transactions worth {cashflow.GamblingTransactionTotal:N0}");
        }

        if (cashflow.BouncedTransactionCount > 0)
        {
            score -= cfg.BouncedTransactionPenalty;
            scoreRedFlags.Add($"{cashflow.BouncedTransactionCount} bounced/failed transactions detected");
            redFlags.Add($"Bounced transactions indicate cash management issues ({cashflow.BouncedTransactionCount} occurrences)");
        }

        if (cashflow.DaysWithNegativeBalance > cfg.HighNegativeBalanceDaysThreshold)
        {
            score -= cfg.HighNegativeBalancePenalty;
            scoreRedFlags.Add($"Account in negative balance for {cashflow.DaysWithNegativeBalance} days");
            redFlags.Add($"Frequent negative balance: {cashflow.DaysWithNegativeBalance} days");
        }
        else if (cashflow.DaysWithNegativeBalance > cfg.ModerateNegativeBalanceDaysThreshold)
        {
            score -= cfg.ModerateNegativeBalancePenalty;
            scoreRedFlags.Add($"Account in negative balance for {cashflow.DaysWithNegativeBalance} days");
        }

        if (cashflow.MonthsAnalyzed < cfg.MinimumMonthsRequired)
        {
            score -= cfg.InsufficientCoveragePenalty;
            scoreRedFlags.Add($"Insufficient statement coverage: only {cashflow.MonthsAnalyzed} months");
        }
        else if (cashflow.MonthsAnalyzed >= cfg.IdealMonthsCoverage)
        {
            score += cfg.IdealCoverageBonus;
            positiveIndicators.Add($"Comprehensive {cashflow.MonthsAnalyzed}-month statement history");
        }

        foreach (var warning in cashflow.AnalysisWarnings ?? new List<string>())
        {
            if (!scoreRedFlags.Contains(warning))
                scoreRedFlags.Add(warning);
        }

        score = Math.Clamp(score, 0, 100);

        var trustNote = cashflow.OverallTrustScore >= 80 ? "High confidence" :
                        cashflow.OverallTrustScore >= 60 ? "Moderate confidence" : "Low confidence";

        return new RiskScoreOutput(
            "CashflowStability",
            score,
            weight,
            DetermineRating(score),
            $"Cashflow analysis over {cashflow.MonthsAnalyzed} months ({trustNote}, trust score: {cashflow.OverallTrustScore:N0}/100). " +
            $"Avg inflow: {cashflow.AverageMonthlyInflow:N0}, Avg outflow: {cashflow.AverageMonthlyOutflow:N0}. " +
            $"Health: {cashflow.CashflowHealthAssessment}. " +
            $"Sources: {(cashflow.HasInternalStatement ? "Internal" : "No internal")}" +
            $"{(cashflow.ExternalStatementsCount > 0 ? $" + {cashflow.ExternalStatementsCount} external" : "")}.",
            scoreRedFlags,
            positiveIndicators
        );
    }

    private RiskScoreOutput CalculateDSCRScore(List<FinancialDataInput> statements, decimal requestedAmount, List<string> redFlags, ScoringConfiguration config)
    {
        var cfg = config.DSCR;
        var weight = config.Weights.DebtServiceCapacity;

        if (!statements.Any())
        {
            return new RiskScoreOutput(
                "DebtServiceCapacity", 50, weight, "Medium",
                "Unable to calculate DSCR due to missing financial data",
                new List<string> { "DSCR cannot be calculated" },
                new List<string>()
            );
        }

        var latest = statements.OrderByDescending(s => s.Year).First();
        var positiveIndicators = new List<string>();
        var scoreRedFlags = new List<string>();
        decimal score;

        var dscr = latest.DebtServiceCoverageRatio;

        if (dscr >= cfg.ExcellentDSCR)
        {
            score = cfg.ExcellentDSCRScore;
            positiveIndicators.Add($"Excellent debt service coverage (DSCR: {dscr:N2})");
        }
        else if (dscr >= cfg.GoodDSCR)
        {
            score = cfg.GoodDSCRScore;
            positiveIndicators.Add($"Good debt service coverage (DSCR: {dscr:N2})");
        }
        else if (dscr >= cfg.AdequateDSCR)
        {
            score = cfg.AdequateDSCRScore;
            positiveIndicators.Add($"Adequate debt service coverage (DSCR: {dscr:N2})");
        }
        else if (dscr >= cfg.MinimumDSCR)
        {
            score = cfg.MinimumDSCRScore;
            scoreRedFlags.Add($"Marginal debt service coverage (DSCR: {dscr:N2})");
            redFlags.Add($"DSCR of {dscr:N2} is below recommended minimum of 1.25x");
        }
        else
        {
            score = cfg.BelowMinimumDSCRScore;
            scoreRedFlags.Add($"Insufficient debt service coverage (DSCR: {dscr:N2})");
            redFlags.Add($"Critical: DSCR of {dscr:N2} indicates inability to service debt");
        }

        if (latest.InterestCoverageRatio >= cfg.StrongInterestCoverage)
        {
            score += cfg.StrongInterestCoverageBonus;
            positiveIndicators.Add($"Strong interest coverage ({latest.InterestCoverageRatio:N2}x)");
        }
        else if (latest.InterestCoverageRatio < cfg.WeakInterestCoverage)
        {
            score -= cfg.WeakInterestCoveragePenalty;
            scoreRedFlags.Add($"Low interest coverage ({latest.InterestCoverageRatio:N2}x)");
        }

        score = Math.Clamp(score, 0, 100);

        return new RiskScoreOutput(
            "DebtServiceCapacity",
            score,
            weight,
            DetermineRating(score),
            $"Debt service capacity based on FY{latest.Year} EBITDA. DSCR: {dscr:N2}x, " +
            $"Interest Coverage: {latest.InterestCoverageRatio:N2}x.",
            scoreRedFlags,
            positiveIndicators
        );
    }

    private RiskScoreOutput CalculateCollateralScore(CollateralDataInput collateral, decimal requestedAmount, List<string> redFlags, ScoringConfiguration config)
    {
        var cfg = config.Collateral;
        var weight = config.Weights.CollateralCoverage;

        var positiveIndicators = new List<string>();
        var scoreRedFlags = new List<string>();
        decimal score;

        var ltv = collateral.TotalForcedSaleValue > 0 ? (requestedAmount / collateral.TotalForcedSaleValue) * 100 : 999;

        if (ltv <= cfg.ExcellentLTV)
        {
            score = cfg.ExcellentLTVScore;
            positiveIndicators.Add($"Strong collateral coverage (LTV: {ltv:N1}%)");
        }
        else if (ltv <= cfg.GoodLTV)
        {
            score = cfg.GoodLTVScore;
            positiveIndicators.Add($"Good collateral coverage (LTV: {ltv:N1}%)");
        }
        else if (ltv <= cfg.AdequateLTV)
        {
            score = cfg.AdequateLTVScore;
            scoreRedFlags.Add($"Moderate collateral coverage (LTV: {ltv:N1}%)");
        }
        else
        {
            score = cfg.UnderCollateralizedScore;
            scoreRedFlags.Add($"Under-collateralized (LTV: {ltv:N1}%)");
            redFlags.Add($"Loan is under-collateralized with LTV of {ltv:N1}%");
        }

        if (collateral.HasPerfectedLiens)
        {
            score += cfg.PerfectedLienBonus;
            positiveIndicators.Add("All liens are perfected");
        }
        else
        {
            score -= cfg.UnperfectedLienPenalty;
            scoreRedFlags.Add("Liens not perfected on all collateral");
            redFlags.Add("Collateral liens pending perfection");
        }

        if (collateral.CollateralTypes.Count > 1)
        {
            positiveIndicators.Add($"Diversified collateral pool ({collateral.CollateralTypes.Count} types)");
        }

        score = Math.Clamp(score, 0, 100);

        return new RiskScoreOutput(
            "CollateralCoverage",
            score,
            weight,
            DetermineRating(score),
            $"Collateral assessment: {collateral.TotalCollateralCount} items with FSV of {collateral.TotalForcedSaleValue:N0}. " +
            $"Effective LTV: {ltv:N1}%.",
            scoreRedFlags,
            positiveIndicators
        );
    }

    public static string DetermineRating(decimal score) => score switch
    {
        >= 80 => "VeryLow",
        >= 65 => "Low",
        >= 50 => "Medium",
        >= 35 => "High",
        _ => "VeryHigh"
    };

    private static string DetermineRecommendation(decimal score, int redFlagCount, ScoringConfiguration config)
    {
        var cfg = config.Recommendations;

        if (redFlagCount >= cfg.CriticalRedFlagsThreshold || score < cfg.ReferMinScore)
            return "Decline";
        if (score >= cfg.StrongApproveMinScore && redFlagCount <= cfg.StrongApproveMaxRedFlags)
            return "StrongApprove";
        if (score >= cfg.ApproveMinScore && redFlagCount <= cfg.ApproveMaxRedFlags)
            return "Approve";
        if (score >= cfg.ApproveWithConditionsMinScore)
            return "ApproveWithConditions";
        return "Refer";
    }

    private static (decimal? amount, int? tenor, decimal? rate, decimal? maxExposure) GenerateLoanRecommendations(
        decimal requestedAmount, int requestedTenor, decimal score, string recommendation, ScoringConfiguration config)
    {
        if (recommendation == "Decline")
            return (null, null, null, null);

        var cfg = config.LoanAdjustments;

        decimal amountMultiplier = score switch
        {
            >= 80 => cfg.Score80PlusMultiplier,
            >= 70 => cfg.Score70PlusMultiplier,
            >= 60 => cfg.Score60PlusMultiplier,
            >= 50 => cfg.Score50PlusMultiplier,
            _ => cfg.BelowScore50Multiplier
        };

        var recAmount = Math.Round(requestedAmount * amountMultiplier, -3);
        var recTenor = score >= cfg.LowScoreThresholdForTenorRestriction
            ? requestedTenor
            : Math.Min(requestedTenor, cfg.MaxTenorForLowScores);

        var rateAdjustment = score switch
        {
            >= 80 => cfg.Score80PlusRateAdjustment,
            >= 70 => cfg.Score70PlusRateAdjustment,
            >= 60 => cfg.Score60PlusRateAdjustment,
            >= 50 => cfg.Score50PlusRateAdjustment,
            _ => cfg.BelowScore50RateAdjustment
        };
        var recRate = cfg.BaseInterestRate + rateAdjustment;

        var maxExposure = recAmount * 1.2m;

        return (recAmount, recTenor, recRate, maxExposure);
    }

    private static void GenerateConditions(decimal score, List<string> redFlags, List<string> conditions, List<string> covenants)
    {
        if (score < 70)
        {
            conditions.Add("Quarterly financial statements submission required");
            covenants.Add("Maintain minimum current ratio of 1.2x");
        }

        if (score < 60)
        {
            conditions.Add("Monthly bank statement submission for first 12 months");
            conditions.Add("Personal guarantee from principal shareholders required");
            covenants.Add("Maintain DSCR above 1.25x");
        }

        if (redFlags.Any(f => f.Contains("collateral", StringComparison.OrdinalIgnoreCase)))
        {
            conditions.Add("Additional collateral to achieve 70% LTV required");
        }

        if (redFlags.Any(f => f.Contains("delinquent", StringComparison.OrdinalIgnoreCase) ||
                              f.Contains("default", StringComparison.OrdinalIgnoreCase)))
        {
            conditions.Add("Clear all outstanding delinquent facilities before disbursement");
        }

        covenants.Add("No additional borrowing without bank consent");
        covenants.Add("Maintain insurance coverage on all pledged assets");
    }
}
