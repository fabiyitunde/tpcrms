using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Namp;

/// <summary>
/// Admin-configurable mapping from an Agricultural Engineer's qualitative viability rating
/// to a numeric score (0–100) and the category weight applied in the overall NAMP advisory score.
///
/// There are exactly 3 rows (one per NampViabilityRating value).
/// Changing Score or CategoryWeight here immediately affects future advisory generation.
///
/// Default seeded values:
///   Viable     → Score = 85, CategoryWeight = 20
///   Marginal   → Score = 50, CategoryWeight = 20
///   NotViable  → Score = 20, CategoryWeight = 20
/// </summary>
public class NampViabilityScoreConfig : AggregateRoot
{
    public NampViabilityRating ViabilityRating { get; private set; }
    public decimal Score { get; private set; }              // 0–100
    public decimal CategoryWeight { get; private set; }     // absolute weight units (e.g. 20)
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }

    protected NampViabilityScoreConfig() { }

    public static NampViabilityScoreConfig Create(
        NampViabilityRating viabilityRating,
        decimal score,
        decimal categoryWeight,
        string? description = null)
    {
        return new NampViabilityScoreConfig
        {
            ViabilityRating = viabilityRating,
            Score = score,
            CategoryWeight = categoryWeight,
            Description = description,
            IsActive = true,
        };
    }

    public void Update(decimal score, decimal categoryWeight, string? description)
    {
        Score = score;
        CategoryWeight = categoryWeight;
        Description = description;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
