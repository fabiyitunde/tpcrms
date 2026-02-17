namespace CRMS.Application.Configuration.DTOs;

public record ScoringParameterDto(
    Guid Id,
    string Category,
    string ParameterKey,
    string DisplayName,
    string Description,
    string DataType,
    decimal CurrentValue,
    decimal? MinValue,
    decimal? MaxValue,
    // Pending change info
    decimal? PendingValue,
    Guid? PendingChangeByUserId,
    DateTime? PendingChangeAt,
    string? PendingChangeReason,
    string ChangeStatus,
    // Audit
    DateTime LastModifiedAt,
    int Version,
    bool IsActive,
    int SortOrder
);

public record ScoringParameterSummaryDto(
    Guid Id,
    string Category,
    string ParameterKey,
    string DisplayName,
    decimal CurrentValue,
    string ChangeStatus,
    bool HasPendingChange
);

public record PendingChangeDto(
    Guid ParameterId,
    string Category,
    string ParameterKey,
    string DisplayName,
    decimal CurrentValue,
    decimal PendingValue,
    Guid RequestedByUserId,
    string? RequestedByUserName,
    DateTime RequestedAt,
    string ChangeReason
);

public record ScoringParameterHistoryDto(
    Guid Id,
    Guid ScoringParameterId,
    string Category,
    string ParameterKey,
    decimal PreviousValue,
    decimal NewValue,
    string ChangeType,
    string? ChangeReason,
    Guid RequestedByUserId,
    DateTime RequestedAt,
    Guid? ApprovedByUserId,
    DateTime? ApprovedAt,
    string? ApprovalNotes,
    int VersionNumber
);

public record CategorySummaryDto(
    string Category,
    string DisplayName,
    int ParameterCount,
    int PendingChangesCount
);

// Input DTOs
public record CreateScoringParameterRequest(
    string Category,
    string ParameterKey,
    string DisplayName,
    string Description,
    string DataType,
    decimal InitialValue,
    decimal? MinValue,
    decimal? MaxValue,
    int SortOrder = 0
);

public record RequestParameterChangeRequest(
    decimal NewValue,
    string Reason
);

public record ApproveChangeRequest(
    string? Notes
);

public record RejectChangeRequest(
    string Reason
);
