using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// Admin-configurable routing threshold row — determines which committee tier a RH-SHF case is
/// circulated to, based on TotalEopValue. Mirrors NampRoutingConfig's value-band shape, minus the
/// applicant-category dimension (RH-SHF has no applicant categories — every case is a FAC). Reuses
/// the existing generic CommitteeType enum directly rather than introducing a parallel RH-SHF-only
/// tier enum, so a resolved tier maps straight onto the shared StandingCommittee/CommitteeReview
/// module without a translation step.
/// </summary>
public class RhshfRoutingConfig : AggregateRoot
{
    public CommitteeType Tier { get; private set; }
    public decimal MinEopValue { get; private set; }
    public decimal MaxEopValue { get; private set; }

    /// <summary>Lower priority value = higher precedence when ranges overlap.</summary>
    public int Priority { get; private set; }
    public bool IsActive { get; private set; }

    protected RhshfRoutingConfig() { }

    public static Result<RhshfRoutingConfig> Create(CommitteeType tier, decimal minEopValue, decimal maxEopValue, int priority = 0)
    {
        if (maxEopValue < minEopValue)
            return Result.Failure<RhshfRoutingConfig>("Max EOP value must not be less than min EOP value.");

        return Result.Success(new RhshfRoutingConfig
        {
            Tier = tier,
            MinEopValue = minEopValue,
            MaxEopValue = maxEopValue,
            Priority = priority,
            IsActive = true,
        });
    }

    public Result Update(decimal minEopValue, decimal maxEopValue, int priority)
    {
        if (maxEopValue < minEopValue)
            return Result.Failure("Max EOP value must not be less than min EOP value.");

        MinEopValue = minEopValue;
        MaxEopValue = maxEopValue;
        Priority = priority;
        return Result.Success();
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
