namespace CRMS.Application.Audit.DTOs;

public record AuditLogDto(
    Guid Id,
    string Action,
    string Category,
    string Description,
    Guid? UserId,
    string? UserName,
    string? UserRole,
    string? IpAddress,
    string EntityType,
    Guid? EntityId,
    string? EntityReference,
    Guid? LoanApplicationId,
    string? LoanApplicationNumber,
    string? OldValues,
    string? NewValues,
    string? AdditionalData,
    bool IsSuccess,
    string? ErrorMessage,
    DateTime Timestamp
);

public record AuditLogSummaryDto(
    Guid Id,
    string Action,
    string Category,
    string Description,
    string? UserName,
    string EntityType,
    string? EntityReference,
    bool IsSuccess,
    DateTime Timestamp
);

public record DataAccessLogDto(
    Guid Id,
    Guid UserId,
    string UserName,
    string UserRole,
    string DataType,
    string EntityType,
    Guid EntityId,
    string? EntityReference,
    Guid? LoanApplicationId,
    string? LoanApplicationNumber,
    string AccessType,
    string? AccessReason,
    string? IpAddress,
    DateTime AccessedAt
);

public record AuditStatisticsDto(
    int TotalActions,
    int SuccessfulActions,
    int FailedActions,
    int UniqueUsers,
    Dictionary<string, int> ActionsByCategory,
    Dictionary<string, int> ActionsByType,
    DateTime? FromDate,
    DateTime? ToDate
);

public record AuditSearchRequest(
    string? Category = null,
    string? Action = null,
    Guid? UserId = null,
    Guid? LoanApplicationId = null,
    string? EntityType = null,
    DateTime? From = null,
    DateTime? To = null,
    bool? IsSuccess = null,
    int PageNumber = 1,
    int PageSize = 50
);

public record AuditSearchResultDto(
    List<AuditLogSummaryDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages
);
