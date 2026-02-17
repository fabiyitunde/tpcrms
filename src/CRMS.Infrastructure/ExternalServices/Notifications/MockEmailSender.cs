using CRMS.Application.Notification.Interfaces;
using CRMS.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.ExternalServices.Notifications;

/// <summary>
/// Mock email sender for development and testing.
/// Logs emails instead of actually sending them.
/// </summary>
public class MockEmailSender : INotificationSender
{
    private readonly ILogger<MockEmailSender> _logger;

    public MockEmailSender(ILogger<MockEmailSender> logger)
    {
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.Email;

    public Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[MOCK EMAIL] To: {Recipient}, Subject: {Subject}, Body: {Body}",
            message.RecipientAddress,
            message.Subject,
            message.Body.Length > 100 ? message.Body[..100] + "..." : message.Body);

        return Task.FromResult(new NotificationSendResult(
            IsSuccess: true,
            ExternalMessageId: Guid.NewGuid().ToString(),
            ProviderName: "MockEmailSender",
            ProviderResponse: "Email logged successfully"));
    }
}

/// <summary>
/// Mock SMS sender for development and testing.
/// Logs SMS messages instead of actually sending them.
/// </summary>
public class MockSmsSender : INotificationSender
{
    private readonly ILogger<MockSmsSender> _logger;

    public MockSmsSender(ILogger<MockSmsSender> logger)
    {
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.SMS;

    public Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[MOCK SMS] To: {Recipient}, Message: {Body}",
            message.RecipientAddress,
            message.Body.Length > 160 ? message.Body[..160] + "..." : message.Body);

        return Task.FromResult(new NotificationSendResult(
            IsSuccess: true,
            ExternalMessageId: Guid.NewGuid().ToString(),
            ProviderName: "MockSmsSender",
            ProviderResponse: "SMS logged successfully"));
    }
}

/// <summary>
/// Mock WhatsApp sender for development and testing.
/// </summary>
public class MockWhatsAppSender : INotificationSender
{
    private readonly ILogger<MockWhatsAppSender> _logger;

    public MockWhatsAppSender(ILogger<MockWhatsAppSender> logger)
    {
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.WhatsApp;

    public Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[MOCK WHATSAPP] To: {Recipient}, Message: {Body}",
            message.RecipientAddress,
            message.Body.Length > 200 ? message.Body[..200] + "..." : message.Body);

        return Task.FromResult(new NotificationSendResult(
            IsSuccess: true,
            ExternalMessageId: Guid.NewGuid().ToString(),
            ProviderName: "MockWhatsAppSender",
            ProviderResponse: "WhatsApp message logged successfully"));
    }
}
