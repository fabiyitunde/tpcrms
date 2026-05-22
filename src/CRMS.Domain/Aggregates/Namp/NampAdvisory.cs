using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Namp;

/// <summary>
/// AI-generated credit advisory for a NAMP application.
/// Separate from CreditAdvisory (corporate loans) — NAMP scoring includes
/// a 5th category (Technical Viability) derived from the Agricultural Engineer's report.
/// Risk scores are stored as JSON (no child entity table).
/// </summary>
public class NampAdvisory : AggregateRoot
{
    public Guid NampApplicationId { get; private set; }
    public AdvisoryStatus Status { get; private set; }

    // ── Overall Assessment ────────────────────────────────────────────────
    public decimal OverallScore { get; private set; }        // 0–100
    public RiskRating OverallRating { get; private set; }
    public AdvisoryRecommendation Recommendation { get; private set; }

    // ── Loan Recommendations ──────────────────────────────────────────────
    public decimal? RecommendedAmount { get; private set; }
    public int? RecommendedTenorMonths { get; private set; }
    public decimal? RecommendedInterestRate { get; private set; }

    // ── AI-Generated Narrative ────────────────────────────────────────────
    public string? ExecutiveSummary { get; private set; }
    public string? StrengthsAnalysis { get; private set; }
    public string? WeaknessesAnalysis { get; private set; }
    public string? MitigatingFactors { get; private set; }
    public string? KeyRisks { get; private set; }

    // ── Technical Viability Snapshot ──────────────────────────────────────
    /// <summary>Engineer's qualitative rating (Viable / Marginal / NotViable) at advisory generation time.</summary>
    public string? TechnicalViabilityRating { get; private set; }
    public decimal? TechnicalViabilityScore { get; private set; }
    public decimal? TechnicalViabilityCategoryWeight { get; private set; }

    // ── JSON-Persisted Collections ────────────────────────────────────────
    /// <summary>Serialized list of all 5 risk score categories.</summary>
    public string? RiskScoresJson { get; private set; }
    public string? RedFlagsJson { get; private set; }
    public string? ConditionsJson { get; private set; }
    public string? CovenantsJson { get; private set; }

    // ── Audit ─────────────────────────────────────────────────────────────
    public string ModelVersion { get; private set; } = string.Empty;
    public DateTime GeneratedAt { get; private set; }
    public Guid GeneratedByUserId { get; private set; }
    public string? ErrorMessage { get; private set; }

    // ── Computed ──────────────────────────────────────────────────────────
    public bool HasCriticalRedFlags { get; private set; }

    private NampAdvisory() { }

    public static Result<NampAdvisory> Create(
        Guid nampApplicationId,
        Guid generatedByUserId,
        string modelVersion)
    {
        if (nampApplicationId == Guid.Empty)
            return Result.Failure<NampAdvisory>("NAMP application ID is required");

        return Result.Success(new NampAdvisory
        {
            NampApplicationId = nampApplicationId,
            GeneratedByUserId = generatedByUserId,
            ModelVersion = modelVersion,
            Status = AdvisoryStatus.Pending,
            GeneratedAt = DateTime.UtcNow,
        });
    }

    public void StartProcessing()
    {
        Status = AdvisoryStatus.Processing;
    }

    public void SetRecommendation(
        AdvisoryRecommendation recommendation,
        decimal? recommendedAmount,
        int? recommendedTenorMonths,
        decimal? recommendedInterestRate)
    {
        Recommendation = recommendation;
        RecommendedAmount = recommendedAmount;
        RecommendedTenorMonths = recommendedTenorMonths;
        RecommendedInterestRate = recommendedInterestRate;
    }

    public void SetTechnicalViability(
        string viabilityRating,
        decimal score,
        decimal categoryWeight)
    {
        TechnicalViabilityRating = viabilityRating;
        TechnicalViabilityScore = score;
        TechnicalViabilityCategoryWeight = categoryWeight;
    }

    public void SetAnalysisContent(
        string? executiveSummary,
        string? strengthsAnalysis,
        string? weaknessesAnalysis,
        string? mitigatingFactors,
        string? keyRisks)
    {
        ExecutiveSummary = executiveSummary;
        StrengthsAnalysis = strengthsAnalysis;
        WeaknessesAnalysis = weaknessesAnalysis;
        MitigatingFactors = mitigatingFactors;
        KeyRisks = keyRisks;
    }

    public void SetPersistedData(
        decimal overallScore,
        RiskRating overallRating,
        bool hasCriticalRedFlags,
        string? riskScoresJson,
        string? redFlagsJson,
        string? conditionsJson,
        string? covenantsJson)
    {
        OverallScore = overallScore;
        OverallRating = overallRating;
        HasCriticalRedFlags = hasCriticalRedFlags;
        RiskScoresJson = riskScoresJson;
        RedFlagsJson = redFlagsJson;
        ConditionsJson = conditionsJson;
        CovenantsJson = covenantsJson;
        Status = AdvisoryStatus.Completed;
        GeneratedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = AdvisoryStatus.Failed;
        ErrorMessage = errorMessage;
    }

    public static RiskRating DetermineRating(decimal score) => score switch
    {
        >= 80 => RiskRating.VeryLow,
        >= 65 => RiskRating.Low,
        >= 50 => RiskRating.Medium,
        >= 35 => RiskRating.High,
        _ => RiskRating.VeryHigh,
    };
}
