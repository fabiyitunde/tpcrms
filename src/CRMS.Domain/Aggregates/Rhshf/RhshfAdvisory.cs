using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// AI-generated credit advisory for a RH-SHF case — mirrors NampAdvisory's shape minus the
/// Technical Viability snapshot (no equivalent in RH-SHF; that section is specific to NAMP's
/// equipment-loan financial appraisal report). Own aggregate root, own table, same isolation
/// principle as every other RH-SHF entity. Risk scores/red flags/conditions/covenants are
/// stored as JSON, no child tables — same as NampAdvisory.
/// </summary>
public class RhshfAdvisory : AggregateRoot
{
    public Guid RhshfCreditProfileId { get; private set; }
    public AdvisoryStatus Status { get; private set; }

    public decimal OverallScore { get; private set; }
    public RiskRating OverallRating { get; private set; }
    public AdvisoryRecommendation Recommendation { get; private set; }

    public decimal? RecommendedAmount { get; private set; }

    public string? ExecutiveSummary { get; private set; }
    public string? StrengthsAnalysis { get; private set; }
    public string? WeaknessesAnalysis { get; private set; }
    public string? MitigatingFactors { get; private set; }
    public string? KeyRisks { get; private set; }

    public string? RiskScoresJson { get; private set; }
    public string? RedFlagsJson { get; private set; }
    public string? ConditionsJson { get; private set; }
    public string? CovenantsJson { get; private set; }

    public string ModelVersion { get; private set; } = string.Empty;
    public DateTime GeneratedAt { get; private set; }
    public Guid GeneratedByUserId { get; private set; }
    public string? ErrorMessage { get; private set; }

    public bool HasCriticalRedFlags { get; private set; }

    private RhshfAdvisory() { }

    public static Result<RhshfAdvisory> Create(Guid rhshfCreditProfileId, Guid generatedByUserId, string modelVersion)
    {
        if (rhshfCreditProfileId == Guid.Empty)
            return Result.Failure<RhshfAdvisory>("RH-SHF credit profile ID is required.");

        return Result.Success(new RhshfAdvisory
        {
            RhshfCreditProfileId = rhshfCreditProfileId,
            GeneratedByUserId = generatedByUserId,
            ModelVersion = modelVersion,
            Status = AdvisoryStatus.Pending,
            GeneratedAt = DateTime.UtcNow,
        });
    }

    public void StartProcessing() => Status = AdvisoryStatus.Processing;

    public void SetRecommendation(AdvisoryRecommendation recommendation, decimal? recommendedAmount)
    {
        Recommendation = recommendation;
        RecommendedAmount = recommendedAmount;
    }

    public void SetAnalysisContent(
        string? executiveSummary, string? strengthsAnalysis, string? weaknessesAnalysis, string? mitigatingFactors, string? keyRisks)
    {
        ExecutiveSummary = executiveSummary;
        StrengthsAnalysis = strengthsAnalysis;
        WeaknessesAnalysis = weaknessesAnalysis;
        MitigatingFactors = mitigatingFactors;
        KeyRisks = keyRisks;
    }

    public void SetPersistedData(
        decimal overallScore, RiskRating overallRating, bool hasCriticalRedFlags,
        string? riskScoresJson, string? redFlagsJson, string? conditionsJson, string? covenantsJson)
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
