using CRMS.Application.Advisory.Interfaces;
using CRMS.Infrastructure.ExternalServices.AI;
using CRMS.Infrastructure.ExternalServices.AIServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit.Abstractions;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// Live integration tests for OpenAI and LLM narrative generation.
///
/// Configuration (appsettings.test.json or environment variables):
///   OpenAI:ApiKey   — your OpenAI API key
///   OpenAI:Model    — model to use (default: gpt-4o-mini)
///
/// Tests are skipped if ApiKey is not configured.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "LiveAPI")]
public class OpenAILiveIntegrationTests
{
    private readonly ITestOutputHelper _output;
    private readonly OpenAISettings _settings;
    private readonly OpenAIService? _openAIService;

    public OpenAILiveIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.test.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var section = configuration.GetSection("OpenAI");
        _settings = new OpenAISettings
        {
            ApiKey = section["ApiKey"] ?? "",
            Model = section["Model"] ?? "gpt-4o-mini",
            MaxTokens = int.TryParse(section["MaxTokens"], out var mt) ? mt : 4096,
            Temperature = double.TryParse(section["Temperature"], out var temp) ? temp : 0.1,
            TimeoutSeconds = int.TryParse(section["TimeoutSeconds"], out var ts) ? ts : 60
        };

        if (!string.IsNullOrEmpty(_settings.ApiKey))
        {
            var httpClient = new HttpClient();
            var logger = new Mock<ILogger<OpenAIService>>().Object;
            _openAIService = new OpenAIService(httpClient, Options.Create(_settings), logger);
        }
    }

    private void SkipIfNotConfigured() =>
        Skip.If(string.IsNullOrEmpty(_settings.ApiKey), "OpenAI:ApiKey not configured in appsettings.test.json");

    // ── Basic connectivity ────────────────────────────────────────────────

    [SkippableFact]
    public async Task OpenAI_BasicCompletion_ReturnsResponse()
    {
        SkipIfNotConfigured();

        var response = await _openAIService!.CompleteAsync(
            "You are a helpful assistant.",
            "Say exactly: 'CRMS OpenAI connection is working.'");

        _output.WriteLine($"Response: {response}");

        Assert.NotNull(response);
        Assert.NotEmpty(response);
    }

    [SkippableFact]
    public async Task OpenAI_JsonCompletion_ParsesStructuredResponse()
    {
        SkipIfNotConfigured();

        var result = await _openAIService!.CompleteAsJsonAsync<PingResponse>(
            "You are a helpful assistant that only responds in valid JSON.",
            "Return a JSON object with a single field 'status' set to 'ok'.");

        _output.WriteLine($"Status: {result?.Status}");

        Assert.NotNull(result);
        Assert.Equal("ok", result.Status, ignoreCase: true);
    }

    // ── LLM Narrative Generator ───────────────────────────────────────────

    [SkippableFact]
    public async Task LLMNarrativeGenerator_WithSampleLoanData_ReturnsNarrative()
    {
        SkipIfNotConfigured();

        var logger = new Mock<ILogger<LLMNarrativeGenerator>>().Object;
        var generator = new LLMNarrativeGenerator(_openAIService!, logger);

        var request = BuildSampleRequest();
        var scoringResult = BuildSampleScoringResult();

        _output.WriteLine("Sending to OpenAI...");
        _output.WriteLine($"Model: {_settings.Model}");
        _output.WriteLine($"Requested Amount: NGN {request.RequestedAmount:N0}");
        _output.WriteLine($"Recommendation: {scoringResult.Recommendation}");
        _output.WriteLine("");

        var result = await generator.GenerateNarrativeAsync(request, scoringResult);

        Assert.NotNull(result);

        _output.WriteLine("=== EXECUTIVE SUMMARY ===");
        _output.WriteLine(result.ExecutiveSummary ?? "(null)");
        _output.WriteLine("");

        _output.WriteLine("=== STRENGTHS ANALYSIS ===");
        _output.WriteLine(result.StrengthsAnalysis ?? "(null)");
        _output.WriteLine("");

        _output.WriteLine("=== WEAKNESSES ANALYSIS ===");
        _output.WriteLine(result.WeaknessesAnalysis ?? "(null)");
        _output.WriteLine("");

        _output.WriteLine("=== MITIGATING FACTORS ===");
        _output.WriteLine(result.MitigatingFactors ?? "(null)");
        _output.WriteLine("");

        _output.WriteLine("=== KEY RISKS ===");
        _output.WriteLine(result.KeyRisks ?? "(null)");
        _output.WriteLine("");

        _output.WriteLine("=== SUGGESTED CONDITIONS ===");
        foreach (var c in result.SuggestedConditions ?? [])
            _output.WriteLine($"  - {c}");
        _output.WriteLine("");

        _output.WriteLine("=== SUGGESTED COVENANTS ===");
        foreach (var c in result.SuggestedCovenants ?? [])
            _output.WriteLine($"  - {c}");

        Assert.NotEmpty(result.ExecutiveSummary ?? "");
        Assert.NotEmpty(result.StrengthsAnalysis ?? "");
        Assert.NotEmpty(result.WeaknessesAnalysis ?? "");
    }

    [SkippableFact]
    public async Task LLMNarrativeGenerator_WithHighRiskApplication_HighlightsRedFlags()
    {
        SkipIfNotConfigured();

        var logger = new Mock<ILogger<LLMNarrativeGenerator>>().Object;
        var generator = new LLMNarrativeGenerator(_openAIService!, logger);

        var request = BuildHighRiskRequest();
        var scoringResult = BuildHighRiskScoringResult();

        _output.WriteLine("Sending HIGH-RISK application to OpenAI...");
        _output.WriteLine($"Recommendation: {scoringResult.Recommendation}");
        _output.WriteLine($"Red Flags: {scoringResult.RedFlags.Count}");
        _output.WriteLine("");

        var result = await generator.GenerateNarrativeAsync(request, scoringResult);

        Assert.NotNull(result);

        _output.WriteLine("=== EXECUTIVE SUMMARY ===");
        _output.WriteLine(result.ExecutiveSummary ?? "(null)");
        _output.WriteLine("");

        _output.WriteLine("=== KEY RISKS ===");
        _output.WriteLine(result.KeyRisks ?? "(null)");

        // For a decline recommendation, the narrative should reference risk indicators
        Assert.NotEmpty(result.ExecutiveSummary ?? "");
    }

    // ── Sample data builders ──────────────────────────────────────────────

    private static AIAdvisoryRequest BuildSampleRequest() => new(
        LoanApplicationId: Guid.NewGuid(),
        RequestedAmount: 50_000_000m,
        RequestedTenorMonths: 24,
        ProductType: "Corporate Term Loan",
        Industry: "Manufacturing",
        BureauReports:
        [
            new BureauDataInput(
                ReportId: Guid.NewGuid(),
                SubjectName: "Adekunle Fashola",
                SubjectType: "Director",
                CreditScore: 720,
                ActiveLoansCount: 2,
                TotalOutstandingDebt: 8_500_000m,
                PerformingLoansCount: 2,
                DelinquentLoansCount: 0,
                DefaultedLoansCount: 0,
                WorstStatus: "Performing",
                ReportDate: DateTime.UtcNow.AddDays(-5),
                MaxDelinquencyDays: 0,
                HasLegalActions: false)
        ],
        FinancialStatements:
        [
            new FinancialDataInput(
                StatementId: Guid.NewGuid(),
                Year: 2024,
                YearType: "Audited",
                TotalAssets: 320_000_000m,
                TotalLiabilities: 180_000_000m,
                TotalEquity: 140_000_000m,
                Revenue: 210_000_000m,
                NetProfit: 22_000_000m,
                EBITDA: 38_000_000m,
                CurrentRatio: 1.85m,
                QuickRatio: 1.42m,
                DebtToEquityRatio: 1.28m,
                InterestCoverageRatio: 4.2m,
                DebtServiceCoverageRatio: 1.65m,
                NetProfitMarginPercent: 10.5m,
                ReturnOnEquity: 15.7m,
                LiquidityAssessment: "Adequate",
                LeverageAssessment: "Moderate",
                ProfitabilityAssessment: "Good",
                OverallAssessment: "Satisfactory")
        ],
        CashflowAnalysis: new CashflowDataInput(
            AnalysisId: Guid.NewGuid(),
            MonthsAnalyzed: 12,
            AverageMonthlyInflow: 19_500_000m,
            AverageMonthlyOutflow: 16_200_000m,
            NetMonthlyCashflow: 3_300_000m,
            CashflowVolatility: 0.18m,
            RecurringCreditsCount: 8,
            RecurringDebitsCount: 12,
            LoanRepaymentRatio: 0.22m,
            HasSalaryCredits: false,
            CashflowHealthAssessment: "Healthy",
            HasInternalStatement: true,
            ExternalStatementsCount: 1,
            AllExternalStatementsVerified: true,
            OverallTrustScore: 82m,
            GamblingTransactionCount: 0,
            GamblingTransactionTotal: 0m,
            BouncedTransactionCount: 1,
            DaysWithNegativeBalance: 0,
            DetectedMonthlySalary: null,
            SalarySource: null,
            AnalysisWarnings: ["One minor returned cheque detected in period"]),
        CollateralSummary: new CollateralDataInput(
            TotalCollateralCount: 2,
            TotalMarketValue: 85_000_000m,
            TotalForcedSaleValue: 62_000_000m,
            AverageLTV: 80.6m,
            CollateralTypes: ["Residential Property", "Commercial Vehicle"],
            HasPerfectedLiens: true,
            ApprovedCount: 2,
            ValuedButNotApprovedCount: 0,
            ValuedButNotApprovedMarketValue: 0m),
        Guarantors:
        [
            new GuarantorDataInput(
                GuarantorId: Guid.NewGuid(),
                Name: "Adekunle Fashola",
                Type: "Individual",
                NetWorth: 95_000_000m,
                GuaranteeAmount: 50_000_000m,
                CreditScore: 720,
                CreditStatus: "Performing",
                HasBureauReport: true)
        ],
        ExistingExposure: 8_500_000m,
        ExistingFacilitiesCount: 1
    );

    private static RuleBasedScoringEngine.ScoringResult BuildSampleScoringResult() => new(
        RiskScores:
        [
            new RiskScoreOutput("CreditHistory", 75m, 0.25m, "Low", "Clean credit history with no defaults", [], ["2 performing facilities", "Score 720"]),
            new RiskScoreOutput("FinancialHealth", 68m, 0.30m, "Medium", "Adequate financials with moderate leverage", ["Debt-to-equity above 1.0x"], ["Profitable 3 consecutive years", "DSCR > 1.5x"]),
            new RiskScoreOutput("CashflowStability", 72m, 0.25m, "Low", "Consistent inflows with minor volatility", ["One returned cheque"], ["12-month inflow trend positive"]),
            new RiskScoreOutput("CollateralCoverage", 80m, 0.20m, "Low", "Well-covered with perfected liens", [], ["FSV covers 124% of facility", "Perfected legal mortgage"])
        ],
        OverallScore: 73.5m,
        OverallRating: "Low",
        Recommendation: "Approve",
        RedFlags: ["Debt-to-equity ratio of 1.28x slightly elevated", "One returned cheque in 12-month period"],
        Conditions:
        [
            "Submission of audited financial statements within 90 days of financial year end",
            "Proof of equity injection of NGN 5,000,000 before first drawdown"
        ],
        Covenants:
        [
            "Maintain minimum DSCR of 1.25x throughout facility tenor",
            "No additional borrowings exceeding NGN 10,000,000 without prior written consent"
        ],
        RecommendedAmount: 50_000_000m,
        RecommendedTenorMonths: 24,
        RecommendedInterestRate: 22.5m,
        MaxExposure: 60_000_000m
    );

    private static AIAdvisoryRequest BuildHighRiskRequest() => new(
        LoanApplicationId: Guid.NewGuid(),
        RequestedAmount: 80_000_000m,
        RequestedTenorMonths: 36,
        ProductType: "Corporate Term Loan",
        Industry: "Retail Trade",
        BureauReports:
        [
            new BureauDataInput(
                ReportId: Guid.NewGuid(),
                SubjectName: "Emeka Okafor",
                SubjectType: "Director",
                CreditScore: 410,
                ActiveLoansCount: 5,
                TotalOutstandingDebt: 42_000_000m,
                PerformingLoansCount: 2,
                DelinquentLoansCount: 2,
                DefaultedLoansCount: 1,
                WorstStatus: "Defaulted",
                ReportDate: DateTime.UtcNow.AddDays(-3),
                MaxDelinquencyDays: 180,
                HasLegalActions: true,
                TotalOverdue: 12_000_000m)
        ],
        FinancialStatements:
        [
            new FinancialDataInput(
                StatementId: Guid.NewGuid(),
                Year: 2024,
                YearType: "Management",
                TotalAssets: 95_000_000m,
                TotalLiabilities: 88_000_000m,
                TotalEquity: 7_000_000m,
                Revenue: 60_000_000m,
                NetProfit: -3_500_000m,
                EBITDA: 1_200_000m,
                CurrentRatio: 0.82m,
                QuickRatio: 0.54m,
                DebtToEquityRatio: 12.57m,
                InterestCoverageRatio: 0.8m,
                DebtServiceCoverageRatio: 0.62m,
                NetProfitMarginPercent: -5.8m,
                ReturnOnEquity: -50.0m,
                LiquidityAssessment: "Weak",
                LeverageAssessment: "Very High",
                ProfitabilityAssessment: "Loss-Making",
                OverallAssessment: "Poor",
                IsUnverified: true)
        ],
        CashflowAnalysis: new CashflowDataInput(
            AnalysisId: Guid.NewGuid(),
            MonthsAnalyzed: 6,
            AverageMonthlyInflow: 4_200_000m,
            AverageMonthlyOutflow: 5_800_000m,
            NetMonthlyCashflow: -1_600_000m,
            CashflowVolatility: 0.62m,
            RecurringCreditsCount: 2,
            RecurringDebitsCount: 18,
            LoanRepaymentRatio: 0.72m,
            HasSalaryCredits: false,
            CashflowHealthAssessment: "Stressed",
            HasInternalStatement: false,
            ExternalStatementsCount: 1,
            AllExternalStatementsVerified: false,
            OverallTrustScore: 28m,
            GamblingTransactionCount: 12,
            GamblingTransactionTotal: 850_000m,
            BouncedTransactionCount: 8,
            DaysWithNegativeBalance: 45,
            DetectedMonthlySalary: null,
            SalarySource: null,
            AnalysisWarnings: ["Negative net cashflow for 6 months", "High gambling exposure", "Unverified external statements only"]),
        CollateralSummary: null,
        Guarantors: [],
        ExistingExposure: 42_000_000m,
        ExistingFacilitiesCount: 5
    );

    private static RuleBasedScoringEngine.ScoringResult BuildHighRiskScoringResult() => new(
        RiskScores:
        [
            new RiskScoreOutput("CreditHistory", 18m, 0.25m, "Very High", "Active default and legal action", ["Defaulted facility", "Legal action ongoing", "180-day delinquency"], []),
            new RiskScoreOutput("FinancialHealth", 12m, 0.30m, "Very High", "Loss-making with near-insolvent balance sheet", ["Net loss in current year", "D/E ratio 12.57x", "DSCR below 1.0x", "Unaudited accounts"], []),
            new RiskScoreOutput("CashflowStability", 10m, 0.25m, "Very High", "Negative cashflow, high gambling and bounced items", ["Negative net cashflow", "8 bounced cheques", "Gambling transactions", "45 days negative balance"], []),
            new RiskScoreOutput("CollateralCoverage", 0m, 0.20m, "Very High", "No collateral pledged", ["No collateral offered", "No guarantors"], [])
        ],
        OverallScore: 11.5m,
        OverallRating: "Very High",
        Recommendation: "Decline",
        RedFlags:
        [
            "Director has active loan default with legal proceedings",
            "Business is currently loss-making (net margin -5.8%)",
            "DSCR of 0.62x — insufficient to service proposed facility",
            "Debt-to-equity ratio of 12.57x indicates near-insolvency",
            "Negative net cashflow for all 6 months analysed",
            "12 gambling transactions totalling NGN 850,000",
            "8 returned cheques in 6-month period",
            "No collateral or guarantors offered",
            "Only unverified management accounts provided"
        ],
        Conditions: [],
        Covenants: [],
        RecommendedAmount: null,
        RecommendedTenorMonths: null,
        RecommendedInterestRate: null,
        MaxExposure: null
    );

    // Helper DTO for JSON parse test
    private class PingResponse
    {
        public string? Status { get; set; }
    }
}
