namespace CRMS.Application.Notification.DTOs;

public record NotificationDto(
    Guid Id,
    string Type,
    string Channel,
    string Priority,
    string Status,
    Guid? RecipientUserId,
    string RecipientName,
    string RecipientAddress,
    string Subject,
    string Body,
    Guid? LoanApplicationId,
    string? LoanApplicationNumber,
    DateTime? ScheduledAt,
    DateTime? SentAt,
    DateTime? DeliveredAt,
    DateTime? FailedAt,
    string? FailureReason,
    int RetryCount,
    DateTime CreatedAt
);

public record NotificationSummaryDto(
    Guid Id,
    string Type,
    string Channel,
    string Status,
    string RecipientName,
    string Subject,
    DateTime? SentAt,
    DateTime CreatedAt
);

public record NotificationTemplateDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string Type,
    string Channel,
    string Language,
    string? Subject,
    string BodyTemplate,
    string? BodyHtmlTemplate,
    bool IsActive,
    int Version
);

public record CreateNotificationTemplateRequest(
    string Code,
    string Name,
    string Description,
    string Type,
    string Channel,
    string BodyTemplate,
    string? Subject = null,
    string? BodyHtmlTemplate = null,
    string? AvailableVariables = null,
    string Language = "en"
);

public record UpdateNotificationTemplateRequest(
    string Name,
    string Description,
    string BodyTemplate,
    string? Subject = null,
    string? BodyHtmlTemplate = null,
    string? AvailableVariables = null
);

public record SendNotificationRequest(
    string Type,
    string Channel,
    string RecipientAddress,
    string RecipientName,
    string TemplateCode,
    Dictionary<string, string> Variables,
    Guid? RecipientUserId = null,
    Guid? LoanApplicationId = null,
    string? LoanApplicationNumber = null,
    string Priority = "Normal",
    DateTime? ScheduledAt = null
);

public record UnreadCountDto(
    int UnreadCount
);
