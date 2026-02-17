using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Advisory;

/// <summary>
/// Individual risk score for a specific category.
/// </summary>
public class RiskScore : ValueObject
{
    public RiskCategory Category { get; private set; }
    public decimal Score { get; private set; } // 0-100
    public decimal Weight { get; private set; } // Weight in overall score (0-1)
    public RiskRating Rating { get; private set; }
    public string Rationale { get; private set; } = string.Empty;
    public List<string> RedFlags { get; private set; } = new();
    public List<string> PositiveIndicators { get; private set; } = new();

    private RiskScore() { }

    public static RiskScore Create(
        RiskCategory category,
        decimal score,
        decimal weight,
        string rationale,
        List<string>? redFlags = null,
        List<string>? positiveIndicators = null)
    {
        var clampedScore = Math.Clamp(score, 0, 100);
        
        return new RiskScore
        {
            Category = category,
            Score = clampedScore,
            Weight = Math.Clamp(weight, 0, 1),
            Rating = DetermineRating(clampedScore),
            Rationale = rationale,
            RedFlags = redFlags ?? new List<string>(),
            PositiveIndicators = positiveIndicators ?? new List<string>()
        };
    }

    public decimal WeightedScore => Score * Weight;

    private static RiskRating DetermineRating(decimal score) => score switch
    {
        >= 80 => RiskRating.VeryLow,
        >= 65 => RiskRating.Low,
        >= 50 => RiskRating.Medium,
        >= 35 => RiskRating.High,
        _ => RiskRating.VeryHigh
    };

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Category;
        yield return Score;
        yield return Weight;
    }
}
