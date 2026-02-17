using CRMS.Application.Advisory.Interfaces;

namespace CRMS.Infrastructure.ExternalServices.AIServices;

/// <summary>
/// Mock implementation of AI Advisory Service for development and testing.
/// Returns realistic-looking advisory data based on input metrics.
/// </summary>
public class MockAIAdvisoryService : IAIAdvisoryService
{
    private const string ModelVersion = "mock-v1.0";

    public string GetModelVersion() => ModelVersion;

    public Task<AIAdvisoryResponse> GenerateAdvisoryAsync(AIAdvisoryRequest request, CancellationToken ct = default)
    {
        var riskScores = new List<RiskScoreOutput>();
        var redFlags = new List<string>();
        var conditions = new List<string>();
        var covenants = new List<string>();

        // Calculate Credit History Score from bureau data
        var creditHistoryScore = CalculateCreditHistoryScore(request.BureauReports, redFlags);
        riskScores.Add(creditHistoryScore);

        // Calculate Financial Health Score from financial statements
        var financialHealthScore = CalculateFinancialHealthScore(request.FinancialStatements, redFlags);
        riskScores.Add(financialHealthScore);

        // Calculate Cashflow Score
        var cashflowScore = CalculateCashflowScore(request.CashflowAnalysis, redFlags);
        riskScores.Add(cashflowScore);

        // Calculate DSCR Score
        var dscrScore = CalculateDSCRScore(request.FinancialStatements, request.RequestedAmount, redFlags);
        riskScores.Add(dscrScore);

        // Calculate Collateral Coverage Score
        if (request.CollateralSummary != null)
        {
            var collateralScore = CalculateCollateralScore(request.CollateralSummary, request.RequestedAmount, redFlags);
            riskScores.Add(collateralScore);
        }

        // Calculate overall score
        var totalWeight = riskScores.Sum(s => s.Weight);
        var overallScore = totalWeight > 0 
            ? Math.Round(riskScores.Sum(s => s.Score * s.Weight) / totalWeight, 2) 
            : 50m;

        var overallRating = DetermineRating(overallScore);
        var recommendation = DetermineRecommendation(overallScore, redFlags.Count);

        // Generate loan recommendations
        var (recAmount, recTenor, recRate, maxExposure) = GenerateLoanRecommendations(
            request.RequestedAmount, 
            request.RequestedTenorMonths, 
            overallScore, 
            recommendation);

        // Generate conditions based on risk level
        GenerateConditions(overallScore, redFlags, conditions, covenants);

        // Generate analysis text
        var (summary, strengths, weaknesses, mitigating, keyRisks) = GenerateAnalysisText(
            request, riskScores, overallScore, recommendation);

        return Task.FromResult(new AIAdvisoryResponse(
            Success: true,
            ErrorMessage: null,
            RiskScores: riskScores,
            OverallScore: overallScore,
            OverallRating: overallRating,
            Recommendation: recommendation,
            RecommendedAmount: recAmount,
            RecommendedTenorMonths: recTenor,
            RecommendedInterestRate: recRate,
            MaxExposure: maxExposure,
            Conditions: conditions,
            Covenants: covenants,
            ExecutiveSummary: summary,
            StrengthsAnalysis: strengths,
            WeaknessesAnalysis: weaknesses,
            MitigatingFactors: mitigating,
            KeyRisks: keyRisks,
            RedFlags: redFlags
        ));
    }

    private RiskScoreOutput CalculateCreditHistoryScore(List<BureauDataInput> bureauReports, List<string> redFlags)
    {
        if (!bureauReports.Any())
        {
            redFlags.Add("No credit bureau data available for assessment");
            return new RiskScoreOutput(
                "CreditHistory", 50, 0.25m, "Medium",
                "Unable to assess credit history due to missing bureau data",
                new List<string> { "No bureau data" },
                new List<string>()
            );
        }

        var positiveIndicators = new List<string>();
        var scoreRedFlags = new List<string>();

        // Average credit score
        var avgScore = bureauReports.Where(b => b.CreditScore.HasValue).Average(b => b.CreditScore!.Value);
        
        // Check for defaults
        var totalDefaults = bureauReports.Sum(b => b.DefaultedLoansCount);
        var totalDelinquent = bureauReports.Sum(b => b.DelinquentLoansCount);
        var totalPerforming = bureauReports.Sum(b => b.PerformingLoansCount);

        decimal score = 70;

        if (avgScore >= 700)
        {
            score += 20;
            positiveIndicators.Add($"Strong average credit score of {avgScore:N0}");
        }
        else if (avgScore >= 650)
        {
            score += 10;
            positiveIndicators.Add($"Good average credit score of {avgScore:N0}");
        }
        else if (avgScore < 600)
        {
            score -= 20;
            scoreRedFlags.Add($"Low average credit score of {avgScore:N0}");
            redFlags.Add($"Low credit scores detected (avg: {avgScore:N0})");
        }

        if (totalDefaults > 0)
        {
            score -= 30;
            scoreRedFlags.Add($"{totalDefaults} defaulted loans on record");
            redFlags.Add($"Credit defaults detected: {totalDefaults} across related parties");
        }

        if (totalDelinquent > 0)
        {
            score -= 15;
            scoreRedFlags.Add($"{totalDelinquent} delinquent loans");
        }

        if (totalPerforming > 3)
        {
            positiveIndicators.Add($"{totalPerforming} performing loan facilities indicate good repayment history");
        }

        score = Math.Clamp(score, 0, 100);

        return new RiskScoreOutput(
            "CreditHistory",
            score,
            0.25m,
            DetermineRating(score),
            $"Credit history assessment based on {bureauReports.Count} bureau reports. " +
            $"Average credit score: {avgScore:N0}. Performing: {totalPerforming}, Delinquent: {totalDelinquent}, Defaulted: {totalDefaults}.",
            scoreRedFlags,
            positiveIndicators
        );
    }

    private RiskScoreOutput CalculateFinancialHealthScore(List<FinancialDataInput> statements, List<string> redFlags)
    {
        if (!statements.Any())
        {
            redFlags.Add("No financial statements available for assessment");
            return new RiskScoreOutput(
                "FinancialHealth", 50, 0.25m, "Medium",
                "Unable to assess financial health due to missing financial statements",
                new List<string> { "No financial data" },
                new List<string>()
            );
        }

        var latest = statements.OrderByDescending(s => s.Year).First();
        var positiveIndicators = new List<string>();
        var scoreRedFlags = new List<string>();

        decimal score = 60;

        // Liquidity assessment
        if (latest.CurrentRatio >= 2.0m)
        {
            score += 10;
            positiveIndicators.Add($"Strong liquidity position (Current Ratio: {latest.CurrentRatio:N2})");
        }
        else if (latest.CurrentRatio < 1.0m)
        {
            score -= 15;
            scoreRedFlags.Add($"Weak liquidity (Current Ratio: {latest.CurrentRatio:N2})");
            redFlags.Add("Liquidity concerns - Current ratio below 1.0");
        }

        // Leverage assessment
        if (latest.DebtToEquityRatio <= 1.0m)
        {
            score += 10;
            positiveIndicators.Add($"Conservative leverage (D/E: {latest.DebtToEquityRatio:N2})");
        }
        else if (latest.DebtToEquityRatio > 3.0m)
        {
            score -= 20;
            scoreRedFlags.Add($"High leverage (D/E: {latest.DebtToEquityRatio:N2})");
            redFlags.Add($"High debt-to-equity ratio of {latest.DebtToEquityRatio:N2}");
        }

        // Profitability
        if (latest.NetProfitMarginPercent >= 10)
        {
            score += 15;
            positiveIndicators.Add($"Strong profitability (Net Margin: {latest.NetProfitMarginPercent:N1}%)");
        }
        else if (latest.NetProfitMarginPercent < 0)
        {
            score -= 25;
            scoreRedFlags.Add($"Loss-making operation (Net Margin: {latest.NetProfitMarginPercent:N1}%)");
            redFlags.Add("Company is currently loss-making");
        }

        // ROE
        if (latest.ReturnOnEquity >= 15)
        {
            score += 10;
            positiveIndicators.Add($"Strong return on equity ({latest.ReturnOnEquity:N1}%)");
        }

        score = Math.Clamp(score, 0, 100);

        return new RiskScoreOutput(
            "FinancialHealth",
            score,
            0.25m,
            DetermineRating(score),
            $"Financial health assessment for FY{latest.Year}. Overall: {latest.OverallAssessment}. " +
            $"Liquidity: {latest.LiquidityAssessment}, Leverage: {latest.LeverageAssessment}, " +
            $"Profitability: {latest.ProfitabilityAssessment}.",
            scoreRedFlags,
            positiveIndicators
        );
    }

    private RiskScoreOutput CalculateCashflowScore(CashflowDataInput? cashflow, List<string> redFlags)
    {
        if (cashflow == null)
        {
            return new RiskScoreOutput(
                "CashflowStability", 60, 0.15m, "Medium",
                "Cashflow analysis not available - using default assessment",
                new List<string>(),
                new List<string>()
            );
        }

        var positiveIndicators = new List<string>();
        var scoreRedFlags = new List<string>();
        decimal score = 65;

        if (cashflow.NetMonthlyCashflow > 0)
        {
            score += 15;
            positiveIndicators.Add($"Positive net monthly cashflow of {cashflow.NetMonthlyCashflow:N0}");
        }
        else
        {
            score -= 20;
            scoreRedFlags.Add("Negative net monthly cashflow");
            redFlags.Add("Negative monthly cashflow detected");
        }

        if (cashflow.CashflowVolatility < 0.3m)
        {
            score += 10;
            positiveIndicators.Add("Low cashflow volatility indicates stable operations");
        }
        else if (cashflow.CashflowVolatility > 0.5m)
        {
            score -= 10;
            scoreRedFlags.Add("High cashflow volatility");
        }

        score = Math.Clamp(score, 0, 100);

        return new RiskScoreOutput(
            "CashflowStability",
            score,
            0.15m,
            DetermineRating(score),
            $"Cashflow analysis over {cashflow.MonthsAnalyzed} months. " +
            $"Avg inflow: {cashflow.AverageMonthlyInflow:N0}, Avg outflow: {cashflow.AverageMonthlyOutflow:N0}. " +
            $"Health: {cashflow.CashflowHealthAssessment}.",
            scoreRedFlags,
            positiveIndicators
        );
    }

    private RiskScoreOutput CalculateDSCRScore(List<FinancialDataInput> statements, decimal requestedAmount, List<string> redFlags)
    {
        if (!statements.Any())
        {
            return new RiskScoreOutput(
                "DebtServiceCapacity", 50, 0.20m, "Medium",
                "Unable to calculate DSCR due to missing financial data",
                new List<string> { "DSCR cannot be calculated" },
                new List<string>()
            );
        }

        var latest = statements.OrderByDescending(s => s.Year).First();
        var positiveIndicators = new List<string>();
        var scoreRedFlags = new List<string>();
        decimal score = 60;

        var dscr = latest.DebtServiceCoverageRatio;

        if (dscr >= 2.0m)
        {
            score = 90;
            positiveIndicators.Add($"Excellent debt service coverage (DSCR: {dscr:N2})");
        }
        else if (dscr >= 1.5m)
        {
            score = 75;
            positiveIndicators.Add($"Good debt service coverage (DSCR: {dscr:N2})");
        }
        else if (dscr >= 1.25m)
        {
            score = 60;
            positiveIndicators.Add($"Adequate debt service coverage (DSCR: {dscr:N2})");
        }
        else if (dscr >= 1.0m)
        {
            score = 45;
            scoreRedFlags.Add($"Marginal debt service coverage (DSCR: {dscr:N2})");
            redFlags.Add($"DSCR of {dscr:N2} is below recommended minimum of 1.25x");
        }
        else
        {
            score = 25;
            scoreRedFlags.Add($"Insufficient debt service coverage (DSCR: {dscr:N2})");
            redFlags.Add($"Critical: DSCR of {dscr:N2} indicates inability to service debt");
        }

        // Interest coverage consideration
        if (latest.InterestCoverageRatio >= 5.0m)
        {
            score += 5;
            positiveIndicators.Add($"Strong interest coverage ({latest.InterestCoverageRatio:N2}x)");
        }
        else if (latest.InterestCoverageRatio < 2.0m)
        {
            score -= 10;
            scoreRedFlags.Add($"Low interest coverage ({latest.InterestCoverageRatio:N2}x)");
        }

        score = Math.Clamp(score, 0, 100);

        return new RiskScoreOutput(
            "DebtServiceCapacity",
            score,
            0.20m,
            DetermineRating(score),
            $"Debt service capacity based on FY{latest.Year} EBITDA. DSCR: {dscr:N2}x, " +
            $"Interest Coverage: {latest.InterestCoverageRatio:N2}x.",
            scoreRedFlags,
            positiveIndicators
        );
    }

    private RiskScoreOutput CalculateCollateralScore(CollateralDataInput collateral, decimal requestedAmount, List<string> redFlags)
    {
        var positiveIndicators = new List<string>();
        var scoreRedFlags = new List<string>();
        decimal score = 65;

        var ltv = requestedAmount > 0 ? (requestedAmount / collateral.TotalForcedSaleValue) * 100 : 0;

        if (ltv <= 50)
        {
            score = 90;
            positiveIndicators.Add($"Strong collateral coverage (LTV: {ltv:N1}%)");
        }
        else if (ltv <= 70)
        {
            score = 75;
            positiveIndicators.Add($"Good collateral coverage (LTV: {ltv:N1}%)");
        }
        else if (ltv <= 100)
        {
            score = 55;
            scoreRedFlags.Add($"Moderate collateral coverage (LTV: {ltv:N1}%)");
        }
        else
        {
            score = 35;
            scoreRedFlags.Add($"Under-collateralized (LTV: {ltv:N1}%)");
            redFlags.Add($"Loan is under-collateralized with LTV of {ltv:N1}%");
        }

        if (collateral.HasPerfectedLiens)
        {
            score += 5;
            positiveIndicators.Add("All liens are perfected");
        }
        else
        {
            score -= 10;
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
            0.15m,
            DetermineRating(score),
            $"Collateral assessment: {collateral.TotalCollateralCount} items with FSV of {collateral.TotalForcedSaleValue:N0}. " +
            $"Effective LTV: {ltv:N1}%.",
            scoreRedFlags,
            positiveIndicators
        );
    }

    private static string DetermineRating(decimal score) => score switch
    {
        >= 80 => "VeryLow",
        >= 65 => "Low",
        >= 50 => "Medium",
        >= 35 => "High",
        _ => "VeryHigh"
    };

    private static string DetermineRecommendation(decimal score, int redFlagCount)
    {
        if (redFlagCount >= 3 || score < 35) return "Decline";
        if (score >= 75 && redFlagCount == 0) return "StrongApprove";
        if (score >= 65 && redFlagCount <= 1) return "Approve";
        if (score >= 50) return "ApproveWithConditions";
        return "Refer";
    }

    private static (decimal? amount, int? tenor, decimal? rate, decimal? maxExposure) GenerateLoanRecommendations(
        decimal requestedAmount, int requestedTenor, decimal score, string recommendation)
    {
        if (recommendation == "Decline")
            return (null, null, null, null);

        decimal amountMultiplier = score switch
        {
            >= 80 => 1.0m,
            >= 70 => 0.9m,
            >= 60 => 0.75m,
            >= 50 => 0.6m,
            _ => 0.5m
        };

        var recAmount = Math.Round(requestedAmount * amountMultiplier, -3); // Round to thousands
        var recTenor = score >= 70 ? requestedTenor : Math.Min(requestedTenor, 36);
        
        decimal baseRate = 18.0m; // Base rate for Nigeria
        var rateAdjustment = score switch
        {
            >= 80 => -2.0m,
            >= 70 => -1.0m,
            >= 60 => 0m,
            >= 50 => 2.0m,
            _ => 4.0m
        };
        var recRate = baseRate + rateAdjustment;

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

    private static (string summary, string strengths, string weaknesses, string? mitigating, string? keyRisks) 
        GenerateAnalysisText(AIAdvisoryRequest request, List<RiskScoreOutput> scores, decimal overallScore, string recommendation)
    {
        var summary = $"Credit assessment for {request.Industry} sector loan application of {request.RequestedAmount:N0} NGN " +
                     $"over {request.RequestedTenorMonths} months. Overall risk score: {overallScore:N1}/100 ({DetermineRating(overallScore)} risk). " +
                     $"Recommendation: {recommendation}. Assessment based on {request.BureauReports.Count} bureau reports, " +
                     $"{request.FinancialStatements.Count} financial statements, and " +
                     $"{(request.CollateralSummary != null ? $"{request.CollateralSummary.TotalCollateralCount} collateral items" : "no collateral pledged")}.";

        var strengthsList = scores.SelectMany(s => s.PositiveIndicators).Take(5).ToList();
        var strengths = strengthsList.Any() 
            ? string.Join(". ", strengthsList) + "."
            : "Limited positive indicators identified.";

        var weaknessesList = scores.SelectMany(s => s.RedFlags).Take(5).ToList();
        var weaknesses = weaknessesList.Any()
            ? string.Join(". ", weaknessesList) + "."
            : "No significant weaknesses identified.";

        string? mitigating = null;
        if (request.Guarantors.Any())
        {
            mitigating = $"Facility supported by {request.Guarantors.Count} guarantor(s) with combined net worth of " +
                        $"{request.Guarantors.Sum(g => g.NetWorth):N0} NGN.";
        }
        if (request.CollateralSummary != null)
        {
            var collateralNote = $"Collateral coverage of {request.CollateralSummary.TotalForcedSaleValue:N0} NGN (FSV) provides downside protection.";
            mitigating = mitigating != null ? $"{mitigating} {collateralNote}" : collateralNote;
        }

        var keyRisks = recommendation switch
        {
            "Decline" => "Multiple critical risk factors identified. Loan does not meet minimum underwriting standards.",
            "Refer" => "Borderline case requiring senior credit committee review. Key concerns include marginal financials and/or credit history issues.",
            "ApproveWithConditions" => "Acceptable risk with conditions. Ongoing monitoring recommended.",
            _ => null
        };

        return (summary, strengths, weaknesses, mitigating, keyRisks);
    }
}
