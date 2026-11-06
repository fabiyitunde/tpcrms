using CRMS.Application.CustomerCheck.Commands;
using CRMS.Application.Notification.Interfaces;
using CRMS.Domain.Enums;
using CRMS.Domain.Services;

namespace CRMS.Infrastructure.Services;

/// <summary>
/// Emails a setup link through the notification service, with the seeded template
/// <see cref="TemplateCode"/>. Sent at high priority so it leaves at once from whichever host the
/// administrator is working in, instead of waiting for the background sender.
/// </summary>
public class ApiConsumerSetupMailer : IApiConsumerSetupMailer
{
    public const string TemplateCode = "API_CONSUMER_SETUP_LINK";

    private readonly INotificationService _notifications;
    private readonly Domain.Interfaces.INotificationRepository _sent;

    public ApiConsumerSetupMailer(INotificationService notifications, Domain.Interfaces.INotificationRepository sent)
    {
        _notifications = notifications;
        _sent = sent;
    }

    public async Task SendAsync(ApiConsumerSetupMail mail, CancellationToken ct = default)
    {
        var id = await _notifications.SendEmailAsync(
            NotificationType.Custom,
            mail.To,
            mail.ConsumerName,
            TemplateCode,
            new Dictionary<string, string>
            {
                ["RecipientName"] = mail.ConsumerName,
                ["Issues"] = mail.Issues,
                ["SetupLink"] = mail.Link,
                ["ExpiryHours"] = mail.ValidForHours.ToString(),
                ["ExpiresAt"] = mail.ExpiresAt.ToString("dd MMM yyyy HH:mm"),
            },
            priority: NotificationPriority.High,
            ct: ct);

        // The notification service records a refused email instead of throwing. One it will try
        // again is as good as sent; one it has given up on is not, and the caller must know, so
        // that a link nobody will receive is cancelled rather than left open.
        var notification = await _sent.GetByIdAsync(id, ct);
        if (notification is { Status: NotificationStatus.Failed })
            throw new InvalidOperationException($"The email was refused: {notification.FailureReason}");
    }
}

/// <summary>Writes credential events of API consumers to the audit trail (Audit Trail page,
/// category Security). Descriptions name the consumer and the address; never a key or a secret.</summary>
public class ApiConsumerAudit : IApiConsumerAudit
{
    private readonly AuditService _audit;

    public ApiConsumerAudit(AuditService audit) => _audit = audit;

    public Task RecordAsync(ApiConsumerAuditEntry entry, CancellationToken ct = default) =>
        _audit.LogAsync(
            entry.Event == ApiConsumerAuditEntry.SecretViewed ? AuditAction.Read : AuditAction.ConfigChange,
            AuditCategory.Security,
            entry.Description,
            "ApiConsumer",
            entityId: entry.ConsumerId,
            entityReference: entry.ConsumerName,
            userId: entry.UserId,
            userName: entry.UserName,
            ipAddress: entry.IpAddress,
            additionalData: new { Event = entry.Event },
            ct: ct);
}
