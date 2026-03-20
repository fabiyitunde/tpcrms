using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CRMS.Application.Advisory.Interfaces;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.ExternalServices.AIServices;

/// <summary>
/// Generates narrative text for credit advisories using LLM.
/// The LLM enhances the presentation but never changes scores or recommendations.
/// </summary>
public class LLMNarrativeGenerator
{
    private readonly ILLMService _llmService;
    private readonly ILogger<LLMNarrativeGenerator> _logger;

    public LLMNarrativeGenerator(ILLMService llmService, ILogger<LLMNarrativeGenerator> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    /// <summary>
    /// Result of LLM narrative generation.
    /// </summary>
    public record NarrativeResult
    {
        [JsonPropertyName("executiveSummary")]
        public string? ExecutiveSummary { get; init; }

        [JsonPropertyName("strengthsAnalysis")]
        public string? StrengthsAnalysis { get; init; }

        [JsonPropertyName("weaknessesAnalysis")]
        public string? WeaknessesAnalysis { get; init; }

        [JsonPropertyName("mitigatingFactors")]
        public string? MitigatingFactors { get; init; }

        [JsonPropertyName("keyRisks")]
        public string? KeyRisks { get; init; }

        [JsonPropertyName("suggestedConditions")]
        public List<string>? SuggestedConditions { get; init; }

        [JsonPropertyName("suggestedCovenants")]
        public List<string>? SuggestedCovenants { get; init; }
    }

    public async Task<NarrativeResult?> GenerateNarrativeAsync(
        AIAdvisoryRequest request,
        RuleBasedScoringEngine.ScoringResult scoringResult,
        CancellationToken ct = default)
    {
        try
        {
            var systemPrompt = BuildSystemPrompt();
            var userPrompt = BuildUserPrompt(request, scoringResult);

            _logger.LogDebug("Generating LLM narrative for loan application {LoanApplicationId}", request.LoanApplicationId);

            var result = await _llmService.CompleteAsJsonAsync<NarrativeResult>(systemPrompt, userPrompt, ct);

            if (result == null)
            {
                _logger.LogWarning("LLM returned null response for loan application {LoanApplicationId}", request.LoanApplicationId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate LLM narrative for loan application {LoanApplicationId}", request.LoanApplicationId);
            return null;
        }
    }

    private static string BuildSystemPrompt()
    {
        return @"You are a senior credit analyst at a Nigerian commercial bank. Your role is to write clear, professional credit advisory narratives based on the data and scores provided.

IMPORTANT RULES:
1. You are NOT making the credit decision - the scores and recommendation are already determined by the rule-based engine
2. Your job is to explain and contextualize the decision in professional banking language
3. Be specific - reference actual numbers from the data provided
4. Highlight the most significant factors (both positive and negative)
5. Use Nigerian banking terminology where appropriate (e.g., NGN for currency, CBN references if relevant)
6. Keep each section concise but informative (2-4 sentences per section)
7. Do NOT invent or assume any data not explicitly provided
8. If data is missing for a category, acknowledge the gap rather than making assumptions

TONE:
- Professional and objective
- Clear and concise
- Balanced - acknowledge both strengths and risks
- Actionable - conditions and covenants should be specific and enforceable

OUTPUT FORMAT (JSON):
{
    ""executiveSummary"": ""2-3 sentence overview of the application, key risk factors, and recommendation"",
    ""strengthsAnalysis"": ""Key positive factors supporting the application (reference specific metrics)"",
    ""weaknessesAnalysis"": ""Key risk factors and concerns identified (reference specific metrics)"",
    ""mitigatingFactors"": ""Factors that offset identified risks, or null if none significant"",
    ""keyRisks"": ""Most critical risks requiring ongoing monitoring, or null if low risk overall"",
    ""suggestedConditions"": [""specific precedent condition 1"", ""specific precedent condition 2""],
    ""suggestedCovenants"": [""specific ongoing covenant 1"", ""specific ongoing covenant 2""]
}";
    }

    private static string BuildUserPrompt(AIAdvisoryRequest request, RuleBasedScoringEngine.ScoringResult scoringResult)
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== LOAN APPLICATION DETAILS ===");
        sb.AppendLine($"Requested Amount: NGN {request.RequestedAmount:N0}");
        sb.AppendLine($"Requested Tenor: {request.RequestedTenorMonths} months");
        sb.AppendLine($"Industry/Sector: {request.Industry}");
        sb.AppendLine($"Product Type: {request.ProductType}");
        sb.AppendLine($"Existing Exposure: NGN {request.ExistingExposure:N0} ({request.ExistingFacilitiesCount} existing facilities)");
        sb.AppendLine();

        sb.AppendLine("=== RULE-BASED SCORING RESULTS (FINAL - DO NOT CHANGE) ===");
        sb.AppendLine($"Overall Score: {scoringResult.OverallScore:N1}/100");
        sb.AppendLine($"Risk Rating: {scoringResult.OverallRating}");
        sb.AppendLine($"Recommendation: {scoringResult.Recommendation}");
        if (scoringResult.RecommendedAmount.HasValue)
        {
            sb.AppendLine($"Recommended Amount: NGN {scoringResult.RecommendedAmount:N0}");
            sb.AppendLine($"Recommended Tenor: {scoringResult.RecommendedTenorMonths} months");
            sb.AppendLine($"Recommended Rate: {scoringResult.RecommendedInterestRate:N2}%");
        }
        sb.AppendLine();

        sb.AppendLine("=== CATEGORY SCORES ===");
        foreach (var score in scoringResult.RiskScores)
        {
            sb.AppendLine($"\n{score.Category}: {score.Score:N1}/100 ({score.Rating} Risk)");
            sb.AppendLine($"  Rationale: {score.Rationale}");
            if (score.PositiveIndicators.Any())
                sb.AppendLine($"  Positives: {string.Join("; ", score.PositiveIndicators)}");
            if (score.RedFlags.Any())
                sb.AppendLine($"  Concerns: {string.Join("; ", score.RedFlags)}");
        }
        sb.AppendLine();

        if (scoringResult.RedFlags.Any())
        {
            sb.AppendLine("=== RED FLAGS IDENTIFIED ===");
            foreach (var flag in scoringResult.RedFlags)
                sb.AppendLine($"- {flag}");
            sb.AppendLine();
        }

        sb.AppendLine("=== BUREAU DATA SUMMARY ===");
        if (request.BureauReports.Any())
        {
            var realReports = request.BureauReports.Where(b => !b.IsPlaceholder).ToList();
            var placeholders = request.BureauReports.Where(b => b.IsPlaceholder).ToList();

            sb.AppendLine($"Total Parties Checked: {request.BureauReports.Count} ({realReports.Count} with data, {placeholders.Count} missing)");

            foreach (var report in realReports)
            {
                sb.AppendLine($"\n  {report.SubjectType}: {report.SubjectName}");
                sb.AppendLine($"    Credit Score: {report.CreditScore?.ToString() ?? "N/A"}");
                sb.AppendLine($"    Active Loans: {report.ActiveLoansCount}, Outstanding: NGN {report.TotalOutstandingDebt:N0}");
                sb.AppendLine($"    Performing: {report.PerformingLoansCount}, Delinquent: {report.DelinquentLoansCount}, Defaulted: {report.DefaultedLoansCount}");
                sb.AppendLine($"    Max Delinquency: {report.MaxDelinquencyDays} days, Legal Actions: {(report.HasLegalActions ? "Yes" : "No")}");
                if (report.FraudRiskScore.HasValue)
                    sb.AppendLine($"    Fraud Risk Score: {report.FraudRiskScore} ({report.FraudRecommendation ?? "N/A"})");
            }

            if (placeholders.Any())
            {
                sb.AppendLine("\n  Missing Bureau Data For:");
                foreach (var p in placeholders)
                    sb.AppendLine($"    - {p.SubjectType}: {p.SubjectName}");
            }
        }
        else
        {
            sb.AppendLine("No bureau data available");
        }
        sb.AppendLine();

        sb.AppendLine("=== FINANCIAL SUMMARY ===");
        if (request.FinancialStatements.Any())
        {
            var latest = request.FinancialStatements.OrderByDescending(f => f.Year).First();
            sb.AppendLine($"Latest Year: FY{latest.Year} ({latest.YearType})");
            sb.AppendLine($"Revenue: NGN {latest.Revenue:N0}");
            sb.AppendLine($"Net Profit: NGN {latest.NetProfit:N0} (Margin: {latest.NetProfitMarginPercent:N1}%)");
            sb.AppendLine($"EBITDA: NGN {latest.EBITDA:N0}");
            sb.AppendLine($"Total Assets: NGN {latest.TotalAssets:N0}");
            sb.AppendLine($"Total Equity: NGN {latest.TotalEquity:N0}");
            sb.AppendLine($"Current Ratio: {latest.CurrentRatio:N2}");
            sb.AppendLine($"Debt-to-Equity: {latest.DebtToEquityRatio:N2}");
            sb.AppendLine($"DSCR: {latest.DebtServiceCoverageRatio:N2}x");
            sb.AppendLine($"Interest Coverage: {latest.InterestCoverageRatio:N2}x");
            sb.AppendLine($"ROE: {latest.ReturnOnEquity:N1}%");
            sb.AppendLine($"Assessments - Liquidity: {latest.LiquidityAssessment}, Leverage: {latest.LeverageAssessment}, Profitability: {latest.ProfitabilityAssessment}");
            if (latest.IsUnverified)
                sb.AppendLine("*** Note: Financial statements pending verification ***");
        }
        else
        {
            sb.AppendLine("No financial statements available");
        }
        sb.AppendLine();

        sb.AppendLine("=== CASHFLOW SUMMARY ===");
        if (request.CashflowAnalysis != null)
        {
            var cf = request.CashflowAnalysis;
            sb.AppendLine($"Period Analyzed: {cf.MonthsAnalyzed} months");
            sb.AppendLine($"Statement Sources: {(cf.HasInternalStatement ? "Internal (own bank)" : "No internal")}{(cf.ExternalStatementsCount > 0 ? $" + {cf.ExternalStatementsCount} external" : "")}");
            sb.AppendLine($"Trust Score: {cf.OverallTrustScore:N0}/100");
            sb.AppendLine($"Average Monthly Inflow: NGN {cf.AverageMonthlyInflow:N0}");
            sb.AppendLine($"Average Monthly Outflow: NGN {cf.AverageMonthlyOutflow:N0}");
            sb.AppendLine($"Net Monthly Cashflow: NGN {cf.NetMonthlyCashflow:N0}");
            sb.AppendLine($"Cashflow Volatility: {cf.CashflowVolatility:P1}");
            sb.AppendLine($"Recurring Credits: {cf.RecurringCreditsCount}, Recurring Debits: {cf.RecurringDebitsCount}");
            sb.AppendLine($"Health Assessment: {cf.CashflowHealthAssessment}");

            if (cf.HasSalaryCredits && cf.DetectedMonthlySalary.HasValue)
                sb.AppendLine($"Detected Salary: NGN {cf.DetectedMonthlySalary:N0}/month from {cf.SalarySource ?? "employer"}");

            // Risk indicators
            if (cf.GamblingTransactionCount > 0)
                sb.AppendLine($"*** WARNING: {cf.GamblingTransactionCount} gambling transactions totaling NGN {cf.GamblingTransactionTotal:N0} ***");
            if (cf.BouncedTransactionCount > 0)
                sb.AppendLine($"*** WARNING: {cf.BouncedTransactionCount} bounced transactions ***");
            if (cf.DaysWithNegativeBalance > 0)
                sb.AppendLine($"*** WARNING: {cf.DaysWithNegativeBalance} days with negative balance ***");

            if (cf.AnalysisWarnings?.Any() == true)
            {
                sb.AppendLine("Analysis Warnings:");
                foreach (var w in cf.AnalysisWarnings)
                    sb.AppendLine($"  - {w}");
            }
        }
        else
        {
            sb.AppendLine("No cashflow analysis available");
        }
        sb.AppendLine();

        sb.AppendLine("=== COLLATERAL SUMMARY ===");
        if (request.CollateralSummary != null)
        {
            var col = request.CollateralSummary;
            sb.AppendLine($"Total Items: {col.TotalCollateralCount}");
            sb.AppendLine($"Approved: {col.ApprovedCount}, Valued but not approved: {col.ValuedButNotApprovedCount}");
            sb.AppendLine($"Total Market Value: NGN {col.TotalMarketValue:N0}");
            sb.AppendLine($"Total Forced Sale Value: NGN {col.TotalForcedSaleValue:N0}");
            sb.AppendLine($"Collateral Types: {string.Join(", ", col.CollateralTypes)}");
            sb.AppendLine($"Liens Perfected: {(col.HasPerfectedLiens ? "Yes" : "No/Pending")}");
            var ltv = col.TotalForcedSaleValue > 0 ? (request.RequestedAmount / col.TotalForcedSaleValue) * 100 : 0;
            sb.AppendLine($"Effective LTV: {ltv:N1}%");
        }
        else
        {
            sb.AppendLine("No collateral pledged");
        }
        sb.AppendLine();

        sb.AppendLine("=== GUARANTOR SUMMARY ===");
        if (request.Guarantors.Any())
        {
            sb.AppendLine($"Total Guarantors: {request.Guarantors.Count}");
            foreach (var g in request.Guarantors)
            {
                sb.AppendLine($"\n  {g.Type}: {g.Name}");
                sb.AppendLine($"    Net Worth: NGN {g.NetWorth:N0}");
                sb.AppendLine($"    Guarantee Amount: NGN {g.GuaranteeAmount:N0}");
                sb.AppendLine($"    Credit Score: {g.CreditScore?.ToString() ?? "N/A"}");
                sb.AppendLine($"    Bureau Report Available: {(g.HasBureauReport ? "Yes" : "No")}");
            }
        }
        else
        {
            sb.AppendLine("No guarantors");
        }
        sb.AppendLine();

        sb.AppendLine("=== EXISTING CONDITIONS FROM RULES ENGINE ===");
        if (scoringResult.Conditions.Any())
        {
            foreach (var c in scoringResult.Conditions)
                sb.AppendLine($"- {c}");
        }
        else
        {
            sb.AppendLine("None specified");
        }
        sb.AppendLine();

        sb.AppendLine("=== EXISTING COVENANTS FROM RULES ENGINE ===");
        if (scoringResult.Covenants.Any())
        {
            foreach (var c in scoringResult.Covenants)
                sb.AppendLine($"- {c}");
        }
        else
        {
            sb.AppendLine("None specified");
        }
        sb.AppendLine();

        sb.AppendLine("Based on the above data and the FINAL scores/recommendation from the rules engine, generate the narrative analysis in JSON format.");

        return sb.ToString();
    }
}
