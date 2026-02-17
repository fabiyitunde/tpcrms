using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Workflow;

/// <summary>
/// Defines a workflow configuration for a loan application type.
/// Contains all valid states, transitions, and SLA requirements.
/// </summary>
public class WorkflowDefinition : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public LoanApplicationType ApplicationType { get; private set; }
    public bool IsActive { get; private set; }
    public int Version { get; private set; }

    private readonly List<WorkflowStage> _stages = [];
    private readonly List<WorkflowTransition> _transitions = [];

    public IReadOnlyCollection<WorkflowStage> Stages => _stages.AsReadOnly();
    public IReadOnlyCollection<WorkflowTransition> Transitions => _transitions.AsReadOnly();

    private WorkflowDefinition() { }

    public static Result<WorkflowDefinition> Create(
        string name,
        string description,
        LoanApplicationType applicationType)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<WorkflowDefinition>("Workflow name is required");

        return Result.Success(new WorkflowDefinition
        {
            Name = name,
            Description = description,
            ApplicationType = applicationType,
            IsActive = true,
            Version = 1
        });
    }

    public Result AddStage(
        LoanApplicationStatus status,
        string displayName,
        string description,
        string assignedRole,
        int slaHours,
        int sortOrder,
        bool requiresComment = false,
        bool isTerminal = false)
    {
        if (_stages.Any(s => s.Status == status))
            return Result.Failure($"Stage for status {status} already exists");

        var stage = WorkflowStage.Create(
            Id, status, displayName, description, assignedRole, slaHours, sortOrder, requiresComment, isTerminal);

        if (stage.IsFailure)
            return Result.Failure(stage.Error);

        _stages.Add(stage.Value);
        return Result.Success();
    }

    public Result AddTransition(
        LoanApplicationStatus fromStatus,
        LoanApplicationStatus toStatus,
        WorkflowAction action,
        string requiredRole,
        bool requiresComment = false,
        string? conditionExpression = null)
    {
        var fromStage = _stages.FirstOrDefault(s => s.Status == fromStatus);
        var toStage = _stages.FirstOrDefault(s => s.Status == toStatus);

        if (fromStage == null)
            return Result.Failure($"From stage {fromStatus} not found");

        if (toStage == null)
            return Result.Failure($"To stage {toStatus} not found");

        if (_transitions.Any(t => t.FromStatus == fromStatus && t.ToStatus == toStatus && t.Action == action))
            return Result.Failure($"Transition from {fromStatus} to {toStatus} via {action} already exists");

        var transition = WorkflowTransition.Create(
            Id, fromStatus, toStatus, action, requiredRole, requiresComment, conditionExpression);

        if (transition.IsFailure)
            return Result.Failure(transition.Error);

        _transitions.Add(transition.Value);
        return Result.Success();
    }

    public WorkflowStage? GetStage(LoanApplicationStatus status)
    {
        return _stages.FirstOrDefault(s => s.Status == status);
    }

    public IEnumerable<WorkflowTransition> GetAvailableTransitions(LoanApplicationStatus currentStatus)
    {
        return _transitions.Where(t => t.FromStatus == currentStatus);
    }

    public WorkflowTransition? GetTransition(LoanApplicationStatus from, LoanApplicationStatus to, WorkflowAction action)
    {
        return _transitions.FirstOrDefault(t => 
            t.FromStatus == from && t.ToStatus == to && t.Action == action);
    }

    public bool CanTransition(LoanApplicationStatus from, LoanApplicationStatus to, WorkflowAction action, string userRole)
    {
        var transition = GetTransition(from, to, action);
        if (transition == null) return false;
        
        return transition.RequiredRole == userRole || userRole == "SystemAdministrator";
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
    
    public void IncrementVersion()
    {
        Version++;
    }
}
