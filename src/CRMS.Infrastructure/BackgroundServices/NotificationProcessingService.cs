using CRMS.Application.Notification.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that processes pending, scheduled, and retry notifications.
/// </summary>
public class NotificationProcessingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationProcessingService> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(30);

    public NotificationProcessingService(
        IServiceProvider serviceProvider,
        ILogger<NotificationProcessingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification Processing Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNotificationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing notifications");
            }

            await Task.Delay(_processingInterval, stoppingToken);
        }

        _logger.LogInformation("Notification Processing Service stopped");
    }

    private async Task ProcessNotificationsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        // Process scheduled notifications that are due
        await notificationService.ProcessScheduledAsync(ct);

        // Process pending notifications
        await notificationService.ProcessPendingAsync(ct);

        // Process retries
        await notificationService.ProcessRetriesAsync(ct);
    }
}
