using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Namp;

/// <summary>
/// Static per-stage configuration for the NAMP workflow.
/// Seeded once; defines SLA, responsible role, and display name for each NampApplicationStatus.
/// </summary>
public class NampWorkflowConfig : Entity
{
    public NampApplicationStatus Status { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string AssignedRole { get; private set; } = string.Empty;
    public int SlaHours { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsTerminal { get; private set; }

    private NampWorkflowConfig() { }

    public static NampWorkflowConfig Create(
        NampApplicationStatus status,
        string displayName,
        string description,
        string assignedRole,
        int slaHours,
        int sortOrder,
        bool isTerminal = false)
    {
        return new NampWorkflowConfig
        {
            Status = status,
            DisplayName = displayName,
            Description = description,
            AssignedRole = assignedRole,
            SlaHours = slaHours,
            SortOrder = sortOrder,
            IsTerminal = isTerminal,
        };
    }
}
