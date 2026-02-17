using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Workflow;

/// <summary>
/// Represents a valid transition between workflow stages.
/// </summary>
public class WorkflowTransition : Entity
{
    public Guid WorkflowDefinitionId { get; private set; }
    public LoanApplicationStatus FromStatus { get; private set; }
    public LoanApplicationStatus ToStatus { get; private set; }
    public WorkflowAction Action { get; private set; }
    public string RequiredRole { get; private set; } = string.Empty;
    public bool RequiresComment { get; private set; }
    public string? ConditionExpression { get; private set; }

    private WorkflowTransition() { }

    internal static Result<WorkflowTransition> Create(
        Guid workflowDefinitionId,
        LoanApplicationStatus fromStatus,
        LoanApplicationStatus toStatus,
        WorkflowAction action,
        string requiredRole,
        bool requiresComment,
        string? conditionExpression)
    {
        if (string.IsNullOrWhiteSpace(requiredRole))
            return Result.Failure<WorkflowTransition>("Required role is required");

        return Result.Success(new WorkflowTransition
        {
            WorkflowDefinitionId = workflowDefinitionId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Action = action,
            RequiredRole = requiredRole,
            RequiresComment = requiresComment,
            ConditionExpression = conditionExpression
        });
    }
}
