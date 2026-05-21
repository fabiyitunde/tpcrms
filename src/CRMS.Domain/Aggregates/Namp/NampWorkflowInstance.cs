using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Namp;

/// <summary>
/// Per-application SLA and queue tracker for the NAMP workflow.
/// One record per NampApplication; updated by NampStatusChangedWorkflowHandler.
/// </summary>
public class NampWorkflowInstance : AggregateRoot
{
    public Guid NampApplicationId { get; private set; }

    // Current stage state
    public NampApplicationStatus CurrentStatus { get; private set; }
    public string CurrentStageName { get; private set; } = string.Empty;
    public string AssignedRole { get; private set; } = string.Empty;
    public Guid? AssignedToUserId { get; private set; }
    public DateTime? AssignedAt { get; private set; }

    // SLA tracking
    public DateTime EnteredCurrentStageAt { get; private set; }
    public DateTime? SLADueAt { get; private set; }
    public bool IsSLABreached { get; private set; }
    public int EscalationLevel { get; private set; }

    // Completion
    public bool IsCompleted { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public NampApplicationStatus? FinalStatus { get; private set; }

    private NampWorkflowInstance() { }

    public static NampWorkflowInstance Create(
        Guid nampApplicationId,
        NampApplicationStatus initialStatus,
        string stageName,
        string assignedRole,
        int slaHours)
    {
        return new NampWorkflowInstance
        {
            NampApplicationId = nampApplicationId,
            CurrentStatus = initialStatus,
            CurrentStageName = stageName,
            AssignedRole = assignedRole,
            EnteredCurrentStageAt = DateTime.UtcNow,
            SLADueAt = slaHours > 0 ? DateTime.UtcNow.AddHours(slaHours) : null,
        };
    }

    public void Transition(
        NampApplicationStatus newStatus,
        string newStageName,
        string newAssignedRole,
        int newSlaHours,
        bool isTerminal = false)
    {
        CurrentStatus = newStatus;
        CurrentStageName = newStageName;
        AssignedRole = newAssignedRole;
        AssignedToUserId = null;
        AssignedAt = null;
        EnteredCurrentStageAt = DateTime.UtcNow;
        SLADueAt = newSlaHours > 0 ? DateTime.UtcNow.AddHours(newSlaHours) : null;
        IsSLABreached = false;
        EscalationLevel = 0;

        if (isTerminal)
        {
            IsCompleted = true;
            CompletedAt = DateTime.UtcNow;
            FinalStatus = newStatus;
        }
    }

    public void AssignToUser(Guid userId)
    {
        AssignedToUserId = userId;
        AssignedAt = DateTime.UtcNow;
    }

    public void Unassign()
    {
        AssignedToUserId = null;
        AssignedAt = null;
    }

    public void MarkSLABreached()
    {
        IsSLABreached = true;
    }

    public bool IsSLADue()
        => !IsCompleted && SLADueAt.HasValue && DateTime.UtcNow > SLADueAt.Value;
}
