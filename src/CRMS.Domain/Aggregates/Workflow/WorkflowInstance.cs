using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Workflow;

/// <summary>
/// Tracks the workflow state for a specific loan application.
/// Maintains queue assignment, SLA tracking, and transition history.
/// </summary>
public class WorkflowInstance : AggregateRoot
{
    public Guid LoanApplicationId { get; private set; }
    public Guid WorkflowDefinitionId { get; private set; }
    public LoanApplicationStatus CurrentStatus { get; private set; }
    public string CurrentStageDisplayName { get; private set; } = string.Empty;
    
    // Queue Assignment
    public string AssignedRole { get; private set; } = string.Empty;
    public Guid? AssignedToUserId { get; private set; }
    public DateTime? AssignedAt { get; private set; }
    
    // SLA Tracking
    public DateTime EnteredCurrentStageAt { get; private set; }
    public DateTime? SLADueAt { get; private set; }
    public bool IsSLABreached { get; private set; }
    public int EscalationLevel { get; private set; }
    
    // Status
    public bool IsCompleted { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public LoanApplicationStatus? FinalStatus { get; private set; }
    
    // Concurrency control
    public byte[] RowVersion { get; private set; } = [];

    private readonly List<WorkflowTransitionLog> _transitionHistory = [];
    public IReadOnlyCollection<WorkflowTransitionLog> TransitionHistory => _transitionHistory.AsReadOnly();

    private WorkflowInstance() { }

    public static Result<WorkflowInstance> Create(
        Guid loanApplicationId,
        Guid workflowDefinitionId,
        LoanApplicationStatus initialStatus,
        string stageDisplayName,
        string assignedRole,
        int slaHours,
        Guid initiatedByUserId)
    {
        var instance = new WorkflowInstance
        {
            LoanApplicationId = loanApplicationId,
            WorkflowDefinitionId = workflowDefinitionId,
            CurrentStatus = initialStatus,
            CurrentStageDisplayName = stageDisplayName,
            AssignedRole = assignedRole,
            EnteredCurrentStageAt = DateTime.UtcNow,
            SLADueAt = slaHours > 0 ? DateTime.UtcNow.AddHours(slaHours) : null,
            IsSLABreached = false,
            EscalationLevel = 0,
            IsCompleted = false
        };

        instance._transitionHistory.Add(WorkflowTransitionLog.Create(
            instance.Id,
            null,
            initialStatus,
            WorkflowAction.Create,
            initiatedByUserId,
            "Workflow initiated"
        ));

        instance.AddDomainEvent(new WorkflowInstanceCreatedEvent(
            instance.Id, loanApplicationId, initialStatus));

        return Result.Success(instance);
    }

    public Result Transition(
        LoanApplicationStatus toStatus,
        WorkflowAction action,
        string newStageDisplayName,
        string newAssignedRole,
        int newSlaHours,
        Guid performedByUserId,
        string? comment = null,
        bool isTerminal = false)
    {
        if (IsCompleted)
            return Result.Failure("Workflow is already completed");

        var fromStatus = CurrentStatus;

        // Log the transition
        _transitionHistory.Add(WorkflowTransitionLog.Create(
            Id, fromStatus, toStatus, action, performedByUserId, comment));

        // Update state
        CurrentStatus = toStatus;
        CurrentStageDisplayName = newStageDisplayName;
        AssignedRole = newAssignedRole;
        AssignedToUserId = null; // Reset assignment on transition
        AssignedAt = null;
        EnteredCurrentStageAt = DateTime.UtcNow;
        SLADueAt = newSlaHours > 0 ? DateTime.UtcNow.AddHours(newSlaHours) : null;
        IsSLABreached = false;
        EscalationLevel = 0;

        if (isTerminal)
        {
            IsCompleted = true;
            CompletedAt = DateTime.UtcNow;
            FinalStatus = toStatus;
            AddDomainEvent(new WorkflowInstanceCompletedEvent(Id, LoanApplicationId, toStatus));
        }
        else
        {
            AddDomainEvent(new WorkflowTransitionedEvent(
                Id, LoanApplicationId, fromStatus, toStatus, action, performedByUserId));
        }

        return Result.Success();
    }

    public Result AssignToUser(Guid userId, Guid assignedByUserId)
    {
        if (IsCompleted)
            return Result.Failure("Cannot assign completed workflow");

        AssignedToUserId = userId;
        AssignedAt = DateTime.UtcNow;

        _transitionHistory.Add(WorkflowTransitionLog.Create(
            Id, CurrentStatus, CurrentStatus, WorkflowAction.Assign, assignedByUserId, 
            $"Assigned to user {userId}"));

        AddDomainEvent(new WorkflowAssignedEvent(Id, LoanApplicationId, userId, assignedByUserId));

        return Result.Success();
    }

    public Result Unassign(Guid unassignedByUserId)
    {
        if (IsCompleted)
            return Result.Failure("Cannot unassign completed workflow");

        if (!AssignedToUserId.HasValue)
            return Result.Failure("Workflow is not assigned to anyone");

        var previousUserId = AssignedToUserId.Value;
        AssignedToUserId = null;
        AssignedAt = null;

        _transitionHistory.Add(WorkflowTransitionLog.Create(
            Id, CurrentStatus, CurrentStatus, WorkflowAction.Unassign, unassignedByUserId,
            $"Unassigned from user {previousUserId}"));

        return Result.Success();
    }

    public Result MarkSLABreached()
    {
        if (IsCompleted)
            return Result.Failure("Cannot breach SLA on completed workflow");

        if (IsSLABreached)
            return Result.Success(); // Already breached

        IsSLABreached = true;
        AddDomainEvent(new WorkflowSLABreachedEvent(Id, LoanApplicationId, CurrentStatus, SLADueAt));

        return Result.Success();
    }

    public Result Escalate(Guid escalatedByUserId, string reason)
    {
        if (IsCompleted)
            return Result.Failure("Cannot escalate completed workflow");

        EscalationLevel++;

        _transitionHistory.Add(WorkflowTransitionLog.Create(
            Id, CurrentStatus, CurrentStatus, WorkflowAction.Escalate, escalatedByUserId, reason));

        AddDomainEvent(new WorkflowEscalatedEvent(
            Id, LoanApplicationId, CurrentStatus, EscalationLevel, reason));

        return Result.Success();
    }

    public bool IsSLADue()
    {
        if (IsCompleted || !SLADueAt.HasValue) return false;
        return DateTime.UtcNow > SLADueAt.Value;
    }

    public TimeSpan? GetTimeInCurrentStage()
    {
        return DateTime.UtcNow - EnteredCurrentStageAt;
    }

    public TimeSpan? GetRemainingTime()
    {
        if (!SLADueAt.HasValue) return null;
        var remaining = SLADueAt.Value - DateTime.UtcNow;
        return remaining.TotalSeconds > 0 ? remaining : TimeSpan.Zero;
    }
}

// Domain Events
public record WorkflowInstanceCreatedEvent(
    Guid WorkflowInstanceId, Guid LoanApplicationId, LoanApplicationStatus InitialStatus) : DomainEvent;

public record WorkflowTransitionedEvent(
    Guid WorkflowInstanceId, Guid LoanApplicationId, 
    LoanApplicationStatus FromStatus, LoanApplicationStatus ToStatus,
    WorkflowAction Action, Guid PerformedByUserId) : DomainEvent;

public record WorkflowInstanceCompletedEvent(
    Guid WorkflowInstanceId, Guid LoanApplicationId, LoanApplicationStatus FinalStatus) : DomainEvent;

public record WorkflowAssignedEvent(
    Guid WorkflowInstanceId, Guid LoanApplicationId, Guid AssignedToUserId, Guid AssignedByUserId) : DomainEvent;

public record WorkflowSLABreachedEvent(
    Guid WorkflowInstanceId, Guid LoanApplicationId, LoanApplicationStatus Status, DateTime? DueAt) : DomainEvent;

public record WorkflowEscalatedEvent(
    Guid WorkflowInstanceId, Guid LoanApplicationId, LoanApplicationStatus Status, 
    int EscalationLevel, string Reason) : DomainEvent;
