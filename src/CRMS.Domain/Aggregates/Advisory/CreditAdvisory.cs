using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Advisory;

/// <summary>
/// AI-generated credit advisory for a loan application.
/// Aggregates risk scores from multiple sources and provides recommendations.
/// </summary>
public class CreditAdvisory : AggregateRoot
{
    public Guid LoanApplicationId { get; private set; }
    public AdvisoryStatus Status { get; private set; }
    
    // Overall Assessment
    public decimal OverallScore { get; private set; } // 0-100
    public RiskRating OverallRating { get; private set; }
    public AdvisoryRecommendation Recommendation { get; private set; }
    
    // Individual Risk Scores
    private readonly List<RiskScore> _riskScores = new();
    public IReadOnlyList<RiskScore> RiskScores => _riskScores.AsReadOnly();
    
    // Loan Recommendations
    public decimal? RecommendedAmount { get; private set; }
    public int? RecommendedTenorMonths { get; private set; }
    public decimal? RecommendedInterestRate { get; private set; }
    public decimal? MaxExposure { get; private set; }
    
    // Conditions and Covenants
    private readonly List<string> _conditions = new();
    public IReadOnlyList<string> Conditions => _conditions.AsReadOnly();
    
    private readonly List<string> _covenants = new();
    public IReadOnlyList<string> Covenants => _covenants.AsReadOnly();
    
    // AI-Generated Content
    public string? ExecutiveSummary { get; private set; }
    public string? StrengthsAnalysis { get; private set; }
    public string? WeaknessesAnalysis { get; private set; }
    public string? MitigatingFactors { get; private set; }
    public string? KeyRisks { get; private set; }
    
    // Red Flags (aggregated)
    private readonly List<string> _redFlags = new();
    public IReadOnlyList<string> RedFlags => _redFlags.AsReadOnly();
    
    // Audit
    public string ModelVersion { get; private set; } = string.Empty;
    public DateTime GeneratedAt { get; private set; }
    public Guid GeneratedByUserId { get; private set; }
    public string? ErrorMessage { get; private set; }
    
    // Input Data References
    public List<Guid> BureauReportIds { get; private set; } = new();
    public List<Guid> FinancialStatementIds { get; private set; } = new();
    public Guid? CashflowAnalysisId { get; private set; }

    private CreditAdvisory() { }

    public static Result<CreditAdvisory> Create(
        Guid loanApplicationId,
        Guid generatedByUserId,
        string modelVersion)
    {
        if (loanApplicationId == Guid.Empty)
            return Result.Failure<CreditAdvisory>("Loan application ID is required");

        return Result.Success(new CreditAdvisory
        {
            LoanApplicationId = loanApplicationId,
            GeneratedByUserId = generatedByUserId,
            ModelVersion = modelVersion,
            Status = AdvisoryStatus.Pending,
            GeneratedAt = DateTime.UtcNow
        });
    }

    public Result StartProcessing()
    {
        if (Status != AdvisoryStatus.Pending)
            return Result.Failure("Advisory is not in pending status");

        Status = AdvisoryStatus.Processing;
        return Result.Success();
    }

    public Result AddRiskScore(RiskScore score)
    {
        if (Status != AdvisoryStatus.Processing)
            return Result.Failure("Advisory must be in processing status to add scores");

        // Remove existing score for same category
        _riskScores.RemoveAll(s => s.Category == score.Category);
        _riskScores.Add(score);
        
        // Aggregate red flags
        foreach (var flag in score.RedFlags)
        {
            if (!_redFlags.Contains(flag))
                _redFlags.Add(flag);
        }

        return Result.Success();
    }

    public Result SetRecommendation(
        AdvisoryRecommendation recommendation,
        decimal? recommendedAmount = null,
        int? recommendedTenorMonths = null,
        decimal? recommendedInterestRate = null,
        decimal? maxExposure = null)
    {
        if (Status != AdvisoryStatus.Processing)
            return Result.Failure("Advisory must be in processing status");

        Recommendation = recommendation;
        RecommendedAmount = recommendedAmount;
        RecommendedTenorMonths = recommendedTenorMonths;
        RecommendedInterestRate = recommendedInterestRate;
        MaxExposure = maxExposure;

        return Result.Success();
    }

    public Result AddCondition(string condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return Result.Failure("Condition cannot be empty");

        if (!_conditions.Contains(condition))
            _conditions.Add(condition);

        return Result.Success();
    }

    public Result AddCovenant(string covenant)
    {
        if (string.IsNullOrWhiteSpace(covenant))
            return Result.Failure("Covenant cannot be empty");

        if (!_covenants.Contains(covenant))
            _covenants.Add(covenant);

        return Result.Success();
    }

    public Result SetAnalysisContent(
        string executiveSummary,
        string strengthsAnalysis,
        string weaknessesAnalysis,
        string? mitigatingFactors = null,
        string? keyRisks = null)
    {
        if (Status != AdvisoryStatus.Processing)
            return Result.Failure("Advisory must be in processing status");

        ExecutiveSummary = executiveSummary;
        StrengthsAnalysis = strengthsAnalysis;
        WeaknessesAnalysis = weaknessesAnalysis;
        MitigatingFactors = mitigatingFactors;
        KeyRisks = keyRisks;

        return Result.Success();
    }

    public Result SetInputReferences(
        List<Guid> bureauReportIds,
        List<Guid> financialStatementIds,
        Guid? cashflowAnalysisId)
    {
        BureauReportIds = bureauReportIds;
        FinancialStatementIds = financialStatementIds;
        CashflowAnalysisId = cashflowAnalysisId;
        return Result.Success();
    }

    public Result Complete()
    {
        if (Status != AdvisoryStatus.Processing)
            return Result.Failure("Advisory must be in processing status");

        if (_riskScores.Count == 0)
            return Result.Failure("At least one risk score is required");

        // Calculate overall score
        var totalWeight = _riskScores.Sum(s => s.Weight);
        if (totalWeight > 0)
        {
            OverallScore = Math.Round(_riskScores.Sum(s => s.WeightedScore) / totalWeight, 2);
        }

        OverallRating = DetermineOverallRating(OverallScore);
        Status = AdvisoryStatus.Completed;
        GeneratedAt = DateTime.UtcNow;

        return Result.Success();
    }

    public Result MarkFailed(string errorMessage)
    {
        Status = AdvisoryStatus.Failed;
        ErrorMessage = errorMessage;
        return Result.Success();
    }

    private static RiskRating DetermineOverallRating(decimal score) => score switch
    {
        >= 80 => RiskRating.VeryLow,
        >= 65 => RiskRating.Low,
        >= 50 => RiskRating.Medium,
        >= 35 => RiskRating.High,
        _ => RiskRating.VeryHigh
    };

    // Computed properties
    public bool HasCriticalRedFlags => _redFlags.Count >= 3 || 
        _riskScores.Any(s => s.Rating == RiskRating.VeryHigh);
    
    public decimal CreditHistoryScore => _riskScores
        .FirstOrDefault(s => s.Category == RiskCategory.CreditHistory)?.Score ?? 0;
    
    public decimal FinancialHealthScore => _riskScores
        .FirstOrDefault(s => s.Category == RiskCategory.FinancialHealth)?.Score ?? 0;
    
    public decimal CashflowScore => _riskScores
        .FirstOrDefault(s => s.Category == RiskCategory.CashflowStability)?.Score ?? 0;
    
    public decimal DSCRScore => _riskScores
        .FirstOrDefault(s => s.Category == RiskCategory.DebtServiceCapacity)?.Score ?? 0;
}
