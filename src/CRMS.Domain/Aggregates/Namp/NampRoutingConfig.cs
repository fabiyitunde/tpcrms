using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Namp;

/// <summary>
/// Admin-configurable routing threshold row.
/// Determines which committee tier handles a NAMP application based on
/// applicant category and equipment value range.
///
/// Row example:
///   Category=YouthAgripreneur, Tier=Branch,  Min=0,       Max=5_000_000
///   Category=YouthAgripreneur, Tier=Zonal,   Min=5_000_001, Max=20_000_000
///   ...
/// </summary>
public class NampRoutingConfig : AggregateRoot
{
    public NampApplicantCategory ApplicantCategory { get; private set; }
    public NampCommitteeTier CommitteeTier { get; private set; }
    public decimal MinEquipmentValue { get; private set; }
    public decimal MaxEquipmentValue { get; private set; }

    /// <summary>
    /// Lower priority value = higher precedence when ranges overlap.
    /// </summary>
    public int Priority { get; private set; }
    public bool IsActive { get; private set; }

    protected NampRoutingConfig() { }

    public static NampRoutingConfig Create(
        NampApplicantCategory applicantCategory,
        NampCommitteeTier committeeTier,
        decimal minEquipmentValue,
        decimal maxEquipmentValue,
        int priority = 0)
    {
        return new NampRoutingConfig
        {
            ApplicantCategory = applicantCategory,
            CommitteeTier = committeeTier,
            MinEquipmentValue = minEquipmentValue,
            MaxEquipmentValue = maxEquipmentValue,
            Priority = priority,
            IsActive = true,
        };
    }

    public void Update(decimal minEquipmentValue, decimal maxEquipmentValue, int priority)
    {
        MinEquipmentValue = minEquipmentValue;
        MaxEquipmentValue = maxEquipmentValue;
        Priority = priority;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
