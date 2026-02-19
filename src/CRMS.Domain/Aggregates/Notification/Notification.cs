using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Notification;

/// <summary>
/// Represents a notification to be sent to a recipient.
/// Tracks delivery status across multiple channels.
/// </summary>
public class Notification : AggregateRoot
{
    public NotificationType Type { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public NotificationPriority Priority { get; private set; }
    public NotificationStatus Status { get; private set; }
    
    // Recipient
    public Guid? RecipientUserId { get; private set; }
    public string RecipientName { get; private set; } = string.Empty;
    public string RecipientAddress { get; private set; } = string.Empty; // Email, phone, etc.
    
    // Content
    public string TemplateCode { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? BodyHtml { get; private set; }
    
    // Context
    public Guid? LoanApplicationId { get; private set; }
    public string? LoanApplicationNumber { get; private set; }
    public string? ContextData { get; private set; } // JSON for template variables
    
    // Delivery tracking
    public DateTime? ScheduledAt { get; private set; }
    public DateTime? SentAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public DateTime? FailedAt { get; private set; }
    public string? FailureReason { get; private set; }
    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; } = 3;
    public DateTime? NextRetryAt { get; private set; }
    
    // External reference
    public string? ExternalMessageId { get; private set; }
    public string? ProviderName { get; private set; }
    public string? ProviderResponse { get; private set; }

    private Notification() { }

    public static Result<Notification> Create(
        NotificationType type,
        NotificationChannel channel,
        NotificationPriority priority,
        string recipientName,
        string recipientAddress,
        string templateCode,
        string subject,
        string body,
        string? bodyHtml = null,
        Guid? recipientUserId = null,
        Guid? loanApplicationId = null,
        string? loanApplicationNumber = null,
        string? contextData = null,
        DateTime? scheduledAt = null)
    {
        if (string.IsNullOrWhiteSpace(recipientAddress))
            return Result.Failure<Notification>("Recipient address is required");

        if (string.IsNullOrWhiteSpace(subject) && channel == NotificationChannel.Email)
            return Result.Failure<Notification>("Subject is required for email notifications");

        if (string.IsNullOrWhiteSpace(body))
            return Result.Failure<Notification>("Body is required");

        var notification = new Notification
        {
            Type = type,
            Channel = channel,
            Priority = priority,
            Status = scheduledAt.HasValue ? NotificationStatus.Scheduled : NotificationStatus.Pending,
            RecipientUserId = recipientUserId,
            RecipientName = recipientName,
            RecipientAddress = recipientAddress,
            TemplateCode = templateCode,
            Subject = subject,
            Body = body,
            BodyHtml = bodyHtml,
            LoanApplicationId = loanApplicationId,
            LoanApplicationNumber = loanApplicationNumber,
            ContextData = contextData,
            ScheduledAt = scheduledAt
        };

        notification.AddDomainEvent(new NotificationCreatedEvent(
            notification.Id, type, channel, recipientAddress, loanApplicationId));

        return Result.Success(notification);
    }

    public Result MarkAsSending()
    {
        if (Status != NotificationStatus.Pending && Status != NotificationStatus.Scheduled && Status != NotificationStatus.Retry)
            return Result.Failure("Notification is not in a sendable state");

        Status = NotificationStatus.Sending;
        return Result.Success();
    }

    public Result MarkAsSent(string? externalMessageId = null, string? providerName = null, string? providerResponse = null)
    {
        if (Status != NotificationStatus.Sending)
            return Result.Failure("Notification is not being sent");

        Status = NotificationStatus.Sent;
        SentAt = DateTime.UtcNow;
        ExternalMessageId = externalMessageId;
        ProviderName = providerName;
        ProviderResponse = providerResponse;

        AddDomainEvent(new NotificationSentEvent(Id, Channel, RecipientAddress, ExternalMessageId));

        return Result.Success();
    }

    public Result MarkAsDelivered(DateTime? deliveredAt = null)
    {
        if (Status != NotificationStatus.Sent)
            return Result.Failure("Notification has not been sent");

        Status = NotificationStatus.Delivered;
        DeliveredAt = deliveredAt ?? DateTime.UtcNow;

        AddDomainEvent(new NotificationDeliveredEvent(Id, Channel, RecipientAddress));

        return Result.Success();
    }

    public Result MarkAsRead(DateTime? readAt = null)
    {
        if (Status != NotificationStatus.Delivered && Status != NotificationStatus.Sent)
            return Result.Failure("Notification has not been delivered");

        Status = NotificationStatus.Read;
        ReadAt = readAt ?? DateTime.UtcNow;

        return Result.Success();
    }

    public Result MarkAsFailed(string reason, bool canRetry = true)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Failure reason is required");

        FailedAt = DateTime.UtcNow;
        FailureReason = reason;

        if (canRetry && RetryCount < MaxRetries)
        {
            Status = NotificationStatus.Retry;
            RetryCount++;
            // Exponential backoff: 1min, 5min, 25min (base 5^retryCount minutes)
            var backoffMinutes = Math.Pow(5, RetryCount);
            NextRetryAt = DateTime.UtcNow.AddMinutes(backoffMinutes);
        }
        else
        {
            Status = NotificationStatus.Failed;
            NextRetryAt = null;
            AddDomainEvent(new NotificationFailedEvent(Id, Channel, RecipientAddress, reason));
        }

        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (Status == NotificationStatus.Sent || Status == NotificationStatus.Delivered || Status == NotificationStatus.Read)
            return Result.Failure("Cannot cancel a notification that has already been sent");

        Status = NotificationStatus.Cancelled;
        FailureReason = $"Cancelled: {reason}";

        return Result.Success();
    }

    public bool CanRetry => Status == NotificationStatus.Retry && RetryCount < MaxRetries;
    public bool IsPending => Status == NotificationStatus.Pending || Status == NotificationStatus.Scheduled || Status == NotificationStatus.Retry;
}

// Domain Events
public record NotificationCreatedEvent(
    Guid NotificationId, NotificationType Type, NotificationChannel Channel, 
    string RecipientAddress, Guid? LoanApplicationId) : DomainEvent;

public record NotificationSentEvent(
    Guid NotificationId, NotificationChannel Channel, string RecipientAddress, 
    string? ExternalMessageId) : DomainEvent;

public record NotificationDeliveredEvent(
    Guid NotificationId, NotificationChannel Channel, string RecipientAddress) : DomainEvent;

public record NotificationFailedEvent(
    Guid NotificationId, NotificationChannel Channel, string RecipientAddress, 
    string FailureReason) : DomainEvent;
