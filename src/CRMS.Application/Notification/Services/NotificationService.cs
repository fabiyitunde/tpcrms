using CRMS.Application.Notification.Interfaces;
using CRMS.Domain.Aggregates.Notification;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Notification.Services;

public class NotificationOrchestrator : INotificationService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationTemplateRepository _templateRepository;
    private readonly IEnumerable<INotificationSender> _senders;
    private readonly IUnitOfWork _unitOfWork;

    public NotificationOrchestrator(
        INotificationRepository notificationRepository,
        INotificationTemplateRepository templateRepository,
        IEnumerable<INotificationSender> senders,
        IUnitOfWork unitOfWork)
    {
        _notificationRepository = notificationRepository;
        _templateRepository = templateRepository;
        _senders = senders;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> SendAsync(
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
        CancellationToken ct = default)
    {
        // Get template
        var template = await _templateRepository.GetByCodeAsync(templateCode, channel, "en", ct);
        if (template == null)
            throw new InvalidOperationException($"Template not found: {templateCode}");

        // Render template
        var (subject, body, bodyHtml) = template.Render(variables);

        // Create notification
        var notificationResult = Domain.Aggregates.Notification.Notification.Create(
            type,
            channel,
            priority,
            recipientName,
            recipientAddress,
            templateCode,
            subject,
            body,
            bodyHtml,
            recipientUserId,
            loanApplicationId,
            loanApplicationNumber,
            System.Text.Json.JsonSerializer.Serialize(variables),
            scheduledAt);

        if (!notificationResult.IsSuccess)
            throw new InvalidOperationException(notificationResult.Error);

        var notification = notificationResult.Value;

        await _notificationRepository.AddAsync(notification, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // If not scheduled, send immediately for high priority
        if (!scheduledAt.HasValue && priority >= NotificationPriority.High)
        {
            await SendNotificationAsync(notification, ct);
        }

        return notification.Id;
    }

    public Task<Guid> SendEmailAsync(
        NotificationType type,
        string recipientEmail,
        string recipientName,
        string templateCode,
        Dictionary<string, string> variables,
        Guid? recipientUserId = null,
        Guid? loanApplicationId = null,
        string? loanApplicationNumber = null,
        NotificationPriority priority = NotificationPriority.Normal,
        CancellationToken ct = default)
    {
        return SendAsync(type, NotificationChannel.Email, recipientEmail, recipientName,
            templateCode, variables, recipientUserId, loanApplicationId, loanApplicationNumber, priority, null, ct);
    }

    public Task<Guid> SendSmsAsync(
        NotificationType type,
        string recipientPhone,
        string recipientName,
        string templateCode,
        Dictionary<string, string> variables,
        Guid? recipientUserId = null,
        Guid? loanApplicationId = null,
        string? loanApplicationNumber = null,
        NotificationPriority priority = NotificationPriority.Normal,
        CancellationToken ct = default)
    {
        return SendAsync(type, NotificationChannel.SMS, recipientPhone, recipientName,
            templateCode, variables, recipientUserId, loanApplicationId, loanApplicationNumber, priority, null, ct);
    }

    public async Task ProcessPendingAsync(CancellationToken ct = default)
    {
        var pending = await _notificationRepository.GetPendingAsync(100, ct);
        
        foreach (var notification in pending)
        {
            await SendNotificationAsync(notification, ct);
        }
    }

    public async Task ProcessRetriesAsync(CancellationToken ct = default)
    {
        var retries = await _notificationRepository.GetForRetryAsync(50, ct);
        
        foreach (var notification in retries)
        {
            await SendNotificationAsync(notification, ct);
        }
    }

    public async Task ProcessScheduledAsync(CancellationToken ct = default)
    {
        var scheduled = await _notificationRepository.GetScheduledDueAsync(DateTime.UtcNow, 100, ct);
        
        foreach (var notification in scheduled)
        {
            await SendNotificationAsync(notification, ct);
        }
    }

    private async Task SendNotificationAsync(Domain.Aggregates.Notification.Notification notification, CancellationToken ct)
    {
        var sender = _senders.FirstOrDefault(s => s.Channel == notification.Channel);
        if (sender == null)
        {
            notification.MarkAsFailed($"No sender configured for channel: {notification.Channel}", canRetry: false);
            _notificationRepository.Update(notification);
            await _unitOfWork.SaveChangesAsync(ct);
            return;
        }

        notification.MarkAsSending();
        _notificationRepository.Update(notification);
        await _unitOfWork.SaveChangesAsync(ct);

        try
        {
            var message = new NotificationMessage(
                notification.RecipientAddress,
                notification.Subject,
                notification.Body,
                notification.BodyHtml,
                notification.Priority);

            var result = await sender.SendAsync(message, ct);

            if (result.IsSuccess)
            {
                notification.MarkAsSent(result.ExternalMessageId, result.ProviderName, result.ProviderResponse);
            }
            else
            {
                notification.MarkAsFailed(result.ErrorMessage ?? "Unknown error", result.CanRetry);
            }
        }
        catch (Exception ex)
        {
            notification.MarkAsFailed(ex.Message, canRetry: true);
        }

        _notificationRepository.Update(notification);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
