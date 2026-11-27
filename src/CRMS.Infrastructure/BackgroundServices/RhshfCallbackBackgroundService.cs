using CRMS.Infrastructure.ExternalServices.Rhshf;
using Microsoft.Extensions.Configuration;
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
/// <para>
/// Sends only on hosts running as Production (TEST and production both do). Every copy of the CRMS that
/// shares a database also shares this outbox, and a local run points at the shared non-prod database
/// while signing with appsettings.json's placeholder secret: on 23 Nov a disbursement made locally sent
/// its final decision from the laptop, and the portal refused all five attempts (CRMSQA#2). Off, a local
/// run still queues its updates, and TEST sends them with the right secret. Rhshf:CallbackSenderEnabled
/// overrides either way.
/// </para>
/// </summary>
public class RhshfCallbackBackgroundService : BackgroundService
{
    private const int PollIntervalSeconds = 30;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RhshfCallbackBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public RhshfCallbackBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<RhshfCallbackBackgroundService> logger,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
    }

    /// <summary>Whether this host sends queued portal updates: Production hosts do unless told not to.</summary>
    public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment) =>
        configuration.GetValue<bool?>("Rhshf:CallbackSenderEnabled") ?? environment.IsProduction();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled(_configuration, _environment))
        {
            _logger.LogInformation(
                "RH-SHF Callback Background Service is off in {Environment}: portal updates queued here are sent by TEST or production",
                _environment.EnvironmentName);
            return;
        }

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
