using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Workflow;

/// <summary>
/// Represents a stage in the workflow with SLA and role assignment.
/// </summary>
public class WorkflowStage : Entity
{
    public Guid WorkflowDefinitionId { get; private set; }
    public LoanApplicationStatus Status { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string AssignedRole { get; private set; } = string.Empty;
    public int SLAHours { get; private set; }
    public int SortOrder { get; private set; }
    public bool RequiresComment { get; private set; }
    public bool IsTerminal { get; private set; }

    private WorkflowStage() { }

    internal static Result<WorkflowStage> Create(
        Guid workflowDefinitionId,
        LoanApplicationStatus status,
        string displayName,
        string description,
        string assignedRole,
        int slaHours,
        int sortOrder,
        bool requiresComment,
        bool isTerminal)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return Result.Failure<WorkflowStage>("Display name is required");

        if (string.IsNullOrWhiteSpace(assignedRole))
            return Result.Failure<WorkflowStage>("Assigned role is required");

        if (slaHours < 0)
            return Result.Failure<WorkflowStage>("SLA hours cannot be negative");

        return Result.Success(new WorkflowStage
        {
            WorkflowDefinitionId = workflowDefinitionId,
            Status = status,
            DisplayName = displayName,
            Description = description,
            AssignedRole = assignedRole,
            SLAHours = slaHours,
            SortOrder = sortOrder,
            RequiresComment = requiresComment,
            IsTerminal = isTerminal
        });
    }

    public TimeSpan GetSLA() => TimeSpan.FromHours(SLAHours);
}
