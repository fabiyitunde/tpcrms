using CRMS.Domain.Aggregates.Workflow;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Domain.Services;

/// <summary>
/// Domain service for workflow operations.
/// Validates transitions and executes workflow state changes.
/// </summary>
public class WorkflowService
{
    private readonly IWorkflowDefinitionRepository _definitionRepository;
    private readonly IWorkflowInstanceRepository _instanceRepository;

    public WorkflowService(
        IWorkflowDefinitionRepository definitionRepository,
        IWorkflowInstanceRepository instanceRepository)
    {
        _definitionRepository = definitionRepository;
        _instanceRepository = instanceRepository;
    }

    /// <summary>
    /// Initialize a workflow for a loan application.
    /// </summary>
    public async Task<Result<WorkflowInstance>> InitializeWorkflowAsync(
        Guid loanApplicationId,
        LoanApplicationType applicationType,
        LoanApplicationStatus initialStatus,
        Guid initiatedByUserId,
        CancellationToken ct = default)
    {
        // Check if workflow already exists
        var existing = await _instanceRepository.GetByLoanApplicationIdAsync(loanApplicationId, ct);
        if (existing != null)
            return Result.Failure<WorkflowInstance>("Workflow already exists for this loan application");

        // Get active workflow definition
        var definition = await _definitionRepository.GetActiveByTypeAsync(applicationType, ct);
        if (definition == null)
            return Result.Failure<WorkflowInstance>($"No active workflow definition for {applicationType} loans");

        // Get initial stage
        var stage = definition.GetStage(initialStatus);
        if (stage == null)
            return Result.Failure<WorkflowInstance>($"Stage {initialStatus} not found in workflow definition");

        var instance = WorkflowInstance.Create(
            loanApplicationId,
            definition.Id,
            initialStatus,
            stage.DisplayName,
            stage.AssignedRole,
            stage.SLAHours,
            initiatedByUserId);

        if (instance.IsFailure)
            return instance;

        await _instanceRepository.AddAsync(instance.Value, ct);
        return instance;
    }

    /// <summary>
    /// Execute a workflow transition.
    /// </summary>
    public async Task<Result> TransitionAsync(
        Guid workflowInstanceId,
        LoanApplicationStatus toStatus,
        WorkflowAction action,
        Guid performedByUserId,
        string userRole,
        string? comment = null,
        CancellationToken ct = default)
    {
        var instance = await _instanceRepository.GetByIdAsync(workflowInstanceId, ct);
        if (instance == null)
            return Result.Failure("Workflow instance not found");

        var definition = await _definitionRepository.GetByIdAsync(instance.WorkflowDefinitionId, ct);
        if (definition == null)
            return Result.Failure("Workflow definition not found");

        // Validate transition is allowed
        var transition = definition.GetTransition(instance.CurrentStatus, toStatus, action);
        if (transition == null)
            return Result.Failure($"Transition from {instance.CurrentStatus} to {toStatus} via {action} is not allowed");

        // Check role authorization
        if (!definition.CanTransition(instance.CurrentStatus, toStatus, action, userRole))
            return Result.Failure($"Role {userRole} is not authorized for this transition. Required: {transition.RequiredRole}");

        // Check comment requirement
        if (transition.RequiresComment && string.IsNullOrWhiteSpace(comment))
            return Result.Failure("Comment is required for this transition");

        // Get target stage
        var targetStage = definition.GetStage(toStatus);
        if (targetStage == null)
            return Result.Failure($"Target stage {toStatus} not found");

        // Execute transition
        var result = instance.Transition(
            toStatus,
            action,
            targetStage.DisplayName,
            targetStage.AssignedRole,
            targetStage.SLAHours,
            performedByUserId,
            comment,
            targetStage.IsTerminal);

        if (result.IsFailure)
            return result;

        _instanceRepository.Update(instance);
        return Result.Success();
    }

    /// <summary>
    /// Get available actions for current workflow state.
    /// </summary>
    public async Task<Result<List<AvailableAction>>> GetAvailableActionsAsync(
        Guid workflowInstanceId,
        string userRole,
        CancellationToken ct = default)
    {
        var instance = await _instanceRepository.GetByIdAsync(workflowInstanceId, ct);
        if (instance == null)
            return Result.Failure<List<AvailableAction>>("Workflow instance not found");

        if (instance.IsCompleted)
            return Result.Success(new List<AvailableAction>());

        var definition = await _definitionRepository.GetByIdAsync(instance.WorkflowDefinitionId, ct);
        if (definition == null)
            return Result.Failure<List<AvailableAction>>("Workflow definition not found");

        var transitions = definition.GetAvailableTransitions(instance.CurrentStatus);
        var actions = transitions
            .Where(t => t.RequiredRole == userRole || userRole == "SystemAdministrator")
            .Select(t => new AvailableAction
            {
                Action = t.Action,
                ToStatus = t.ToStatus,
                RequiresComment = t.RequiresComment,
                DisplayName = GetActionDisplayName(t.Action)
            })
            .ToList();

        return Result.Success(actions);
    }

    /// <summary>
    /// Assign workflow to a specific user.
    /// </summary>
    public async Task<Result> AssignAsync(
        Guid workflowInstanceId,
        Guid assignToUserId,
        Guid assignedByUserId,
        CancellationToken ct = default)
    {
        var instance = await _instanceRepository.GetByIdAsync(workflowInstanceId, ct);
        if (instance == null)
            return Result.Failure("Workflow instance not found");

        var result = instance.AssignToUser(assignToUserId, assignedByUserId);
        if (result.IsFailure)
            return result;

        _instanceRepository.Update(instance);
        return Result.Success();
    }

    /// <summary>
    /// Check and mark SLA breaches.
    /// </summary>
    public async Task<int> CheckAndMarkSLABreachesAsync(CancellationToken ct = default)
    {
        var overdueInstances = await _instanceRepository.GetOverdueSLAAsync(ct);
        var breachedCount = 0;

        foreach (var instance in overdueInstances)
        {
            if (!instance.IsSLABreached && instance.IsSLADue())
            {
                instance.MarkSLABreached();
                _instanceRepository.Update(instance);
                breachedCount++;
            }
        }

        return breachedCount;
    }

    private static string GetActionDisplayName(WorkflowAction action) => action switch
    {
        WorkflowAction.Submit => "Submit",
        WorkflowAction.Approve => "Approve",
        WorkflowAction.Reject => "Reject",
        WorkflowAction.Return => "Return for Correction",
        WorkflowAction.Escalate => "Escalate",
        WorkflowAction.MoveToNextStage => "Move to Next Stage",
        WorkflowAction.RequestInfo => "Request Information",
        WorkflowAction.Override => "Override Decision",
        _ => action.ToString()
    };
}

public class AvailableAction
{
    public WorkflowAction Action { get; set; }
    public LoanApplicationStatus ToStatus { get; set; }
    public bool RequiresComment { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
