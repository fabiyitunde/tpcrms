using CRMS.Domain.Enums;

namespace CRMS.Application.Workflow.DTOs;

public record WorkflowDefinitionDto(
    Guid Id,
    string Name,
    string Description,
    string ApplicationType,
    bool IsActive,
    int Version,
    List<WorkflowStageDto> Stages,
    List<WorkflowTransitionDto> Transitions
);

public record WorkflowStageDto(
    Guid Id,
    string Status,
    string DisplayName,
    string Description,
    string AssignedRole,
    int SLAHours,
    int SortOrder,
    bool RequiresComment,
    bool IsTerminal
);

public record WorkflowTransitionDto(
    Guid Id,
    string FromStatus,
    string ToStatus,
    string Action,
    string RequiredRole,
    bool RequiresComment
);

public record WorkflowInstanceDto(
    Guid Id,
    Guid LoanApplicationId,
    Guid WorkflowDefinitionId,
    string CurrentStatus,
    string CurrentStageDisplayName,
    string AssignedRole,
    Guid? AssignedToUserId,
    DateTime? AssignedAt,
    DateTime EnteredCurrentStageAt,
    DateTime? SLADueAt,
    bool IsSLABreached,
    int EscalationLevel,
    bool IsCompleted,
    DateTime? CompletedAt,
    string? FinalStatus,
    TimeSpan? TimeInCurrentStage,
    TimeSpan? RemainingTime,
    List<WorkflowTransitionLogDto> RecentHistory
);

public record WorkflowInstanceSummaryDto(
    Guid Id,
    Guid LoanApplicationId,
    string ApplicationNumber,
    string CustomerName,
    string CurrentStatus,
    string CurrentStageDisplayName,
    string AssignedRole,
    Guid? AssignedToUserId,
    DateTime? SLADueAt,
    bool IsSLABreached,
    bool IsOverdue
);

public record WorkflowTransitionLogDto(
    Guid Id,
    string? FromStatus,
    string ToStatus,
    string Action,
    Guid PerformedByUserId,
    DateTime PerformedAt,
    string? Comment
);

public record AvailableActionDto(
    string Action,
    string ToStatus,
    bool RequiresComment,
    string DisplayName
);

public record WorkflowQueueSummaryDto(
    string Role,
    int TotalCount,
    int OverdueCount,
    int AssignedCount,
    int UnassignedCount
);

public record WorkflowStatusSummaryDto(
    string Status,
    string DisplayName,
    int Count,
    int OverdueCount
);

// Input DTOs
public record TransitionWorkflowRequest(
    string ToStatus,
    string Action,
    string? Comment
);

public record AssignWorkflowRequest(
    Guid AssignToUserId
);
