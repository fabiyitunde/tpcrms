using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Workflow;

/// <summary>
/// Immutable audit record of a workflow transition or action.
/// </summary>
public class WorkflowTransitionLog : Entity
{
    public Guid WorkflowInstanceId { get; private set; }
    public LoanApplicationStatus? FromStatus { get; private set; }
    public LoanApplicationStatus ToStatus { get; private set; }
    public WorkflowAction Action { get; private set; }
    public Guid PerformedByUserId { get; private set; }
    public DateTime PerformedAt { get; private set; }
    public string? Comment { get; private set; }
    public TimeSpan? DurationInPreviousStage { get; private set; }

    private WorkflowTransitionLog() { }

    internal static WorkflowTransitionLog Create(
        Guid workflowInstanceId,
        LoanApplicationStatus? fromStatus,
        LoanApplicationStatus toStatus,
        WorkflowAction action,
        Guid performedByUserId,
        string? comment)
    {
        return new WorkflowTransitionLog
        {
            WorkflowInstanceId = workflowInstanceId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Action = action,
            PerformedByUserId = performedByUserId,
            PerformedAt = DateTime.UtcNow,
            Comment = comment
        };
    }
}
