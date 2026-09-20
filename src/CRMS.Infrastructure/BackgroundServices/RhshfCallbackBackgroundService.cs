using CRMS.Infrastructure.ExternalServices.Rhshf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.BackgroundServices;

/// <summary>
/// Polls the persistent RhshfCallbackAttempt outbox and delivers due webhooks (design doc §4.4,
/// Phase 10) — mirrors CreditCheckBackgroundService's polling-outbox shape rather than an
/// in-process Polly retry policy (like NAMP's own callback uses), because the brief's ~30-minute
/// retry window is too long to hold open across a single HTTP call/request lifecycle and needs to
/// survive an app restart mid-window. All actual send/retry-scheduling logic lives in
/// RhshfCallbackDispatcher (directly unit-testable); this class is just the poll loop.
/// </summary>
public class RhshfCallbackBackgroundService : BackgroundService
{
    private const int PollIntervalSeconds = 30;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RhshfCallbackBackgroundService> _logger;

    public RhshfCallbackBackgroundService(IServiceScopeFactory scopeFactory, ILogger<RhshfCallbackBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RH-SHF Callback Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<RhshfCallbackDispatcher>();
                var processed = await dispatcher.ProcessDueAttemptsAsync(stoppingToken);
                if (processed > 0)
                    _logger.LogInformation("RH-SHF callback outbox: processed {Count} due attempt(s)", processed);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Unexpected error in RH-SHF callback polling cycle");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("RH-SHF Callback Background Service stopped");
    }
}
