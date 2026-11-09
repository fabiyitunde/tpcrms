using CRMS.Application.Billing;
using CRMS.Application.Billing.Alerts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.BackgroundServices;

/// <summary>
/// Runs the billing checks a few minutes after start-up and then every few hours (Billing:Alerts:
/// CheckEveryHours). New findings become alerts and are emailed to the Billing Managers; findings
/// that have gone away resolve their alerts. Both hosts run it; alerts are keyed by condition, so
/// the second run of a cycle only refreshes what the first recorded.
/// </summary>
public class BillingAlertService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BillingSettings _settings;
    private readonly ILogger<BillingAlertService> _logger;

    public BillingAlertService(IServiceScopeFactory scopeFactory, BillingSettings settings, ILogger<BillingAlertService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Alerts.Enabled)
        {
            _logger.LogInformation("Billing alert checks are disabled (Billing:Alerts:Enabled)");
            return;
        }
        try { await Task.Delay(InitialDelay, stoppingToken); } catch (OperationCanceledException) { return; }

        var interval = TimeSpan.FromHours(Math.Clamp(_settings.Alerts.CheckEveryHours, 1, 168));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<RunBillingAlertChecksHandler>();
                var result = await handler.Handle(new RunBillingAlertChecksCommand(null), stoppingToken);
                if (result.IsSuccess && result.Data is { } r && (r.Added > 0 || r.Resolved > 0))
                    _logger.LogInformation("Billing checks: {Added} new alert(s), {Resolved} resolved, {Emailed} manager(s) emailed", r.Added, r.Resolved, r.Emailed);
                else if (!result.IsSuccess)
                    _logger.LogWarning("Billing checks did not run: {Error}", result.Error);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Billing checks failed");
            }
            try { await Task.Delay(interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
