using CRMS.Application.Advisory.Interfaces;
using CRMS.Domain.Configuration;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.ExternalServices.AIServices;

/// <summary>
/// Hybrid AI Advisory Service that combines:
/// 1. Rule-based scoring engine for deterministic, auditable scores and recommendations
/// 2. LLM-generated narratives for enhanced, contextual explanatory text
/// 
/// The LLM NEVER changes scores or recommendations - it only improves the presentation.
/// If LLM fails, the service gracefully falls back to template-based narratives.
/// </summary>
public class HybridAIAdvisoryService : IAIAdvisoryService
{
    private const string ModelVersion = "hybrid-v1.0-rules+llm";

    private readonly ScoringConfigurationService _configService;
    private readonly RuleBasedScoringEngine _scoringEngine;
    private readonly CustomerCheckScoringEngine _customerEngine;
    private readonly LLMNarrativeGenerator? _narrativeGenerator;
    private readonly AIAdvisorySettings _settings;
    private readonly ILogger<HybridAIAdvisoryService> _logger;

    // Configuration cache
    private ScoringConfiguration? _cachedConfig;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public HybridAIAdvisoryService(
        ScoringConfigurationService configService,
        RuleBasedScoringEngine scoringEngine,
        IOptions<AIAdvisorySettings> settings,
        ILogger<HybridAIAdvisoryService> logger,
        LLMNarrativeGenerator? narrativeGenerator = null,
        CustomerCheckScoringEngine? customerEngine = null)
    {
        _configService = configService;
        _scoringEngine = scoringEngine;
        _settings = settings.Value;
        _logger = logger;
        _narrativeGenerator = narrativeGenerator;
        _customerEngine = customerEngine ?? new CustomerCheckScoringEngine();
    }

    public string GetModelVersion() => _settings.UseLLMNarrative ? ModelVersion : "hybrid-v1.0-rules-only";

    /// <summary>The customer check's advisory: the individual profile scores, then the narrative.
    /// The narrative never changes a number, and when the LLM is off, slow or failing the template
    /// narrative is used; a missing narrative is never a reason to withhold the scores.</summary>
    public async Task<CustomerAdvisoryResponse> GenerateCustomerAdvisoryAsync(CustomerAdvisoryRequest request, CancellationToken ct = default)
    {
        var config = await GetConfigAsync(ct);
        var result = _customerEngine.CalculateScores(request, config);
        if (result.Scores.Count == 0)
            return new CustomerAdvisoryResponse(
                false, "Nothing to assess. " + string.Join(" ", result.NotAssessed.Select(g => g.Reason)),
                [], result.NotAssessed, 0, result.OverallRating, result.Recommendation, null, null, [], [],
                string.Empty, string.Empty, string.Empty, null, null, false, GetModelVersion());

        LLMNarrativeGenerator.NarrativeResult? narrative = null;
        if (_settings.UseLLMNarrative && _narrativeGenerator != null)
        {
            try
            {
                using var llmCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                llmCts.CancelAfter(TimeSpan.FromSeconds(_settings.LLMTimeoutSeconds));
                narrative = await _narrativeGenerator.GenerateCustomerNarrativeAsync(request, result, llmCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Customer check advisory {Reference}: the LLM narrative timed out; using the template.", request.ReferenceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Customer check advisory {Reference}: the LLM narrative failed; using the template.", request.ReferenceId);
            }
        }

        var conditions = result.Conditions.ToList();
        foreach (var c in narrative?.SuggestedConditions ?? [])
            if (!string.IsNullOrWhiteSpace(c) && !conditions.Any(x => x.Contains(c, StringComparison.OrdinalIgnoreCase) || c.Contains(x, StringComparison.OrdinalIgnoreCase)))
                conditions.Add(c);

        var fromLlm = !string.IsNullOrWhiteSpace(narrative?.ExecutiveSummary);
        return new CustomerAdvisoryResponse(
            true, null, result.Scores, result.NotAssessed, result.OverallScore, result.OverallRating, result.Recommendation,
            result.RecommendedAmount, result.RecommendedTenorMonths, conditions, result.RedFlags,
            fromLlm ? narrative!.ExecutiveSummary! : CustomerTemplateSummary(request, result),
            !string.IsNullOrWhiteSpace(narrative?.StrengthsAnalysis) ? narrative!.StrengthsAnalysis! : Sentences(result.Scores.SelectMany(s => s.PositiveIndicators), "No clear strengths in the data available."),
            !string.IsNullOrWhiteSpace(narrative?.WeaknessesAnalysis) ? narrative!.WeaknessesAnalysis! : Sentences(result.Scores.SelectMany(s => s.RedFlags), "No significant weaknesses in the data available."),
            !string.IsNullOrWhiteSpace(narrative?.MitigatingFactors) ? narrative!.MitigatingFactors : CustomerTemplateMitigating(request),
            !string.IsNullOrWhiteSpace(narrative?.KeyRisks) ? narrative!.KeyRisks : result.RedFlags.Count == 0 ? null : Sentences(result.RedFlags, ""),
            fromLlm, GetModelVersion());
    }

    private static string CustomerTemplateSummary(CustomerAdvisoryRequest r, CustomerCheckScoringEngine.Result s)
    {
        var basis = new List<string>();
        if (r.Bureau is not null) basis.Add("a bureau report");
        if (r.Cashflow is { AccountsRead: > 0 } c) basis.Add($"{c.WindowMonths} months of statements on {c.AccountsRead} account{(c.AccountsRead == 1 ? "" : "s")}");
        if (r.Exposure.BankDataAvailable) basis.Add("the customer's loans with the bank");
        if (r.Identity.BvnVerified is not null || r.Identity.FraudRiskScore is not null) basis.Add("the identity and fraud checks");
        var gaps = s.NotAssessed.Count == 0 ? "" : $" Not assessed: {string.Join(", ", s.NotAssessed.Select(g => g.Category))}.";
        return $"Assessment of the customer for NGN {r.ProposedAmount:N0} over {r.ProposedTenorMonths} months" +
            $"{(r.ProposalSupplied ? "" : " (assumed)")}. Overall score {s.OverallScore:N1}/100 ({s.OverallRating} risk); recommendation {s.Recommendation}. " +
            $"Based on {(basis.Count == 0 ? "limited data" : string.Join(", ", basis))}.{gaps}";
    }

    private static string? CustomerTemplateMitigating(CustomerAdvisoryRequest r)
    {
        var f = new List<string>();
        if (r.Exposure.BankPaidLoans > 0) f.Add($"{r.Exposure.BankPaidLoans} loan{(r.Exposure.BankPaidLoans == 1 ? "" : "s")} with the bank repaid in full");
        if (r.Identity.MonthsAsCustomer is >= 24) f.Add($"a customer of the bank for {r.Identity.MonthsAsCustomer} months");
        if (r.Cashflow is { TotalBalance: > 0 } c && c.TotalBalance >= r.ProposedAmount * 0.25m) f.Add($"balances of NGN {c.TotalBalance:N0} held with the bank");
        return f.Count == 0 ? null : char.ToUpperInvariant(f[0][0]) + string.Join("; ", f)[1..] + ".";
    }

    private static string Sentences(IEnumerable<string> items, string fallback)
    {
        var list = items.Take(5).Select(i => i.TrimEnd('.')).ToList();
        return list.Count == 0 ? fallback : string.Join(". ", list) + ".";
    }

    public async Task<AIAdvisoryResponse> GenerateAdvisoryAsync(AIAdvisoryRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Generating advisory for loan application {LoanApplicationId}, LLM enabled: {LLMEnabled}",
            request.LoanApplicationId, _settings.UseLLMNarrative);

        // STEP 1: Rule-based scoring (always runs, deterministic)
        var config = await GetConfigAsync(ct);
        var scoringResult = _scoringEngine.CalculateScores(request, config);

        _logger.LogDebug(
            "Rule-based scoring complete for {LoanApplicationId}: Score={Score}, Recommendation={Recommendation}",
            request.LoanApplicationId, scoringResult.OverallScore, scoringResult.Recommendation);

        // STEP 2: LLM narrative enhancement (optional)
        LLMNarrativeGenerator.NarrativeResult? narrative = null;

        if (_settings.UseLLMNarrative && _narrativeGenerator != null)
        {
            try
            {
                using var llmCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                llmCts.CancelAfter(TimeSpan.FromSeconds(_settings.LLMTimeoutSeconds));

                narrative = await _narrativeGenerator.GenerateNarrativeAsync(request, scoringResult, llmCts.Token);

                if (narrative != null)
                {
                    _logger.LogInformation(
                        "LLM narrative generated successfully for {LoanApplicationId}",
                        request.LoanApplicationId);
                }
                else
                {
                    _logger.LogWarning(
                        "LLM returned null narrative for {LoanApplicationId}, using template fallback",
                        request.LoanApplicationId);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "LLM narrative generation timed out for {LoanApplicationId} after {Timeout}s, using template fallback",
                    request.LoanApplicationId, _settings.LLMTimeoutSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "LLM narrative generation failed for {LoanApplicationId}, using template fallback",
                    request.LoanApplicationId);

                if (!_settings.FallbackToTemplateOnFailure)
                {
                    return new AIAdvisoryResponse(
                        Success: false,
                        ErrorMessage: $"LLM narrative generation failed: {ex.Message}",
                        RiskScores: scoringResult.RiskScores,
                        OverallScore: scoringResult.OverallScore,
                        OverallRating: scoringResult.OverallRating,
                        Recommendation: scoringResult.Recommendation,
                        RecommendedAmount: null,
                        RecommendedTenorMonths: null,
                        RecommendedInterestRate: null,
                        MaxExposure: null,
                        Conditions: new List<string>(),
                        Covenants: new List<string>(),
                        ExecutiveSummary: string.Empty,
                        StrengthsAnalysis: string.Empty,
                        WeaknessesAnalysis: string.Empty,
                        MitigatingFactors: null,
                        KeyRisks: null,
                        RedFlags: scoringResult.RedFlags
                    );
                }
            }
        }

        // STEP 3: Build final response (merge rule-based + LLM results)
        return BuildResponse(request, scoringResult, narrative);
    }

    private async Task<ScoringConfiguration> GetConfigAsync(CancellationToken ct)
    {
        if (_cachedConfig != null && DateTime.UtcNow < _cacheExpiry)
            return _cachedConfig;

        _cachedConfig = await _configService.LoadConfigurationAsync(ct);
        _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
        return _cachedConfig;
    }

    private AIAdvisoryResponse BuildResponse(
        AIAdvisoryRequest request,
        RuleBasedScoringEngine.ScoringResult scoringResult,
        LLMNarrativeGenerator.NarrativeResult? narrative)
    {
        // Use LLM narratives if available, otherwise fall back to templates
        var executiveSummary = !string.IsNullOrWhiteSpace(narrative?.ExecutiveSummary)
            ? narrative.ExecutiveSummary
            : GenerateTemplateSummary(request, scoringResult);

        var strengthsAnalysis = !string.IsNullOrWhiteSpace(narrative?.StrengthsAnalysis)
            ? narrative.StrengthsAnalysis
            : GenerateTemplateStrengths(scoringResult);

        var weaknessesAnalysis = !string.IsNullOrWhiteSpace(narrative?.WeaknessesAnalysis)
            ? narrative.WeaknessesAnalysis
            : GenerateTemplateWeaknesses(scoringResult);

        var mitigatingFactors = !string.IsNullOrWhiteSpace(narrative?.MitigatingFactors)
            ? narrative.MitigatingFactors
            : GenerateTemplateMitigatingFactors(request);

        var keyRisks = !string.IsNullOrWhiteSpace(narrative?.KeyRisks)
            ? narrative.KeyRisks
            : GenerateTemplateKeyRisks(scoringResult);

        // Merge conditions: rule-based + LLM suggestions (deduplicated)
        var conditions = scoringResult.Conditions.ToList();
        if (narrative?.SuggestedConditions != null)
        {
            foreach (var c in narrative.SuggestedConditions)
            {
                if (!string.IsNullOrWhiteSpace(c) && !conditions.Any(existing =>
                    existing.Contains(c, StringComparison.OrdinalIgnoreCase) ||
                    c.Contains(existing, StringComparison.OrdinalIgnoreCase)))
                {
                    conditions.Add(c);
                }
            }
        }

        // Merge covenants: rule-based + LLM suggestions (deduplicated)
        var covenants = scoringResult.Covenants.ToList();
        if (narrative?.SuggestedCovenants != null)
        {
            foreach (var c in narrative.SuggestedCovenants)
            {
                if (!string.IsNullOrWhiteSpace(c) && !covenants.Any(existing =>
                    existing.Contains(c, StringComparison.OrdinalIgnoreCase) ||
                    c.Contains(existing, StringComparison.OrdinalIgnoreCase)))
                {
                    covenants.Add(c);
                }
            }
        }

        return new AIAdvisoryResponse(
            Success: true,
            ErrorMessage: null,
            RiskScores: scoringResult.RiskScores,
            OverallScore: scoringResult.OverallScore,
            OverallRating: scoringResult.OverallRating,
            Recommendation: scoringResult.Recommendation,
            RecommendedAmount: scoringResult.RecommendedAmount,
            RecommendedTenorMonths: scoringResult.RecommendedTenorMonths,
            RecommendedInterestRate: scoringResult.RecommendedInterestRate,
            MaxExposure: scoringResult.MaxExposure,
            Conditions: conditions,
            Covenants: covenants,
            ExecutiveSummary: executiveSummary,
            StrengthsAnalysis: strengthsAnalysis,
            WeaknessesAnalysis: weaknessesAnalysis,
            MitigatingFactors: mitigatingFactors,
            KeyRisks: keyRisks,
            RedFlags: scoringResult.RedFlags
        );
    }

    #region Template-Based Fallback Narratives

    private static string GenerateTemplateSummary(AIAdvisoryRequest request, RuleBasedScoringEngine.ScoringResult scoringResult)
    {
        return $"Credit assessment for {request.Industry} sector loan application of NGN {request.RequestedAmount:N0} " +
               $"over {request.RequestedTenorMonths} months. Overall risk score: {scoringResult.OverallScore:N1}/100 " +
               $"({scoringResult.OverallRating} risk). Recommendation: {scoringResult.Recommendation}. " +
               $"Assessment based on {request.BureauReports.Count} bureau reports, " +
               $"{request.FinancialStatements.Count} financial statements, and " +
               $"{(request.CollateralSummary != null ? $"{request.CollateralSummary.TotalCollateralCount} collateral items" : "no collateral pledged")}.";
    }

    private static string GenerateTemplateStrengths(RuleBasedScoringEngine.ScoringResult scoringResult)
    {
        var strengths = scoringResult.RiskScores
            .SelectMany(s => s.PositiveIndicators)
            .Take(5)
            .ToList();

        return strengths.Any()
            ? string.Join(". ", strengths) + "."
            : "Limited positive indicators identified in the available data.";
    }

    private static string GenerateTemplateWeaknesses(RuleBasedScoringEngine.ScoringResult scoringResult)
    {
        var weaknesses = scoringResult.RiskScores
            .SelectMany(s => s.RedFlags)
            .Take(5)
            .ToList();

        return weaknesses.Any()
            ? string.Join(". ", weaknesses) + "."
            : "No significant weaknesses identified in the available data.";
    }

    private static string? GenerateTemplateMitigatingFactors(AIAdvisoryRequest request)
    {
        var factors = new List<string>();

        if (request.Guarantors.Any())
        {
            var totalNetWorth = request.Guarantors.Sum(g => g.NetWorth);
            factors.Add($"Facility supported by {request.Guarantors.Count} guarantor(s) with combined net worth of NGN {totalNetWorth:N0}");
        }

        if (request.CollateralSummary != null && request.CollateralSummary.TotalForcedSaleValue > 0)
        {
            factors.Add($"Collateral coverage of NGN {request.CollateralSummary.TotalForcedSaleValue:N0} (FSV) provides downside protection");
        }

        return factors.Any() ? string.Join(". ", factors) + "." : null;
    }

    private static string? GenerateTemplateKeyRisks(RuleBasedScoringEngine.ScoringResult scoringResult)
    {
        return scoringResult.Recommendation switch
        {
            "Decline" => "Multiple critical risk factors identified. Application does not meet minimum underwriting standards.",
            "Refer" => "Borderline case requiring senior credit committee review. Key concerns include marginal financials and/or credit history issues.",
            "ApproveWithConditions" => "Acceptable risk profile with conditions. Ongoing monitoring of financial covenants recommended.",
            _ => null
        };
    }

    #endregion
}
