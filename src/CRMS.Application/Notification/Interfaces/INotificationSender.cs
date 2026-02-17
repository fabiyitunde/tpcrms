using CRMS.Domain.Enums;

namespace CRMS.Application.Notification.Interfaces;

/// <summary>
/// Interface for notification channel providers.
/// </summary>
public interface INotificationSender
{
    NotificationChannel Channel { get; }
    Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken ct = default);
}

public record NotificationMessage(
    string RecipientAddress,
    string Subject,
    string Body,
    string? BodyHtml = null,
    NotificationPriority Priority = NotificationPriority.Normal,
    Dictionary<string, string>? Metadata = null
);

public record NotificationSendResult(
    bool IsSuccess,
    string? ExternalMessageId = null,
    string? ProviderName = null,
    string? ProviderResponse = null,
    string? ErrorMessage = null,
    bool CanRetry = true
);

/// <summary>
/// Interface for the notification orchestration service.
/// </summary>
public interface INotificationService
{
    Task<Guid> SendAsync(
        NotificationType type,
        NotificationChannel channel,
        string recipientAddress,
        string recipientName,
        string templateCode,
        Dictionary<string, string> variables,
        Guid? recipientUserId = null,
        Guid? loanApplicationId = null,
        string? loanApplicationNumber = null,
        NotificationPriority priority = NotificationPriority.Normal,
        DateTime? scheduledAt = null,
        CancellationToken ct = default);

    Task<Guid> SendEmailAsync(
        NotificationType type,
        string recipientEmail,
        string recipientName,
        string templateCode,
        Dictionary<string, string> variables,
        Guid? recipientUserId = null,
        Guid? loanApplicationId = null,
        string? loanApplicationNumber = null,
        NotificationPriority priority = NotificationPriority.Normal,
        CancellationToken ct = default);

    Task<Guid> SendSmsAsync(
        NotificationType type,
        string recipientPhone,
        string recipientName,
        string templateCode,
        Dictionary<string, string> variables,
        Guid? recipientUserId = null,
        Guid? loanApplicationId = null,
        string? loanApplicationNumber = null,
        NotificationPriority priority = NotificationPriority.Normal,
        CancellationToken ct = default);

    Task ProcessPendingAsync(CancellationToken ct = default);
    Task ProcessRetriesAsync(CancellationToken ct = default);
    Task ProcessScheduledAsync(CancellationToken ct = default);
}
