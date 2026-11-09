using CRMS.Application.Billing.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.BackgroundServices;

/// <summary>
/// Keeps the platform-fee rows in step with the loan books: shortly after start-up and then every
/// hour, every application is read and any milestone it has reached without a billing row gets one.
/// Both hosts run it; the work is idempotent, so that only costs a duplicate read.
/// </summary>
public class BillingLoanSyncService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BillingLoanSyncService> _logger;

    public BillingLoanSyncService(IServiceScopeFactory scopeFactory, ILogger<BillingLoanSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(InitialDelay, stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<SyncBillingLoanEventsHandler>();
                var result = await handler.Handle(new SyncBillingLoanEventsCommand(), stoppingToken);
                if (result.IsSuccess && result.Data!.RowsAdded > 0)
                    _logger.LogInformation("Billing loan sync: {Added} platform-fee rows added across {Loans} loans", result.Data.RowsAdded, result.Data.LoansSeen);
                else if (!result.IsSuccess)
                    _logger.LogWarning("Billing loan sync did not run: {Error}", result.Error);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Billing loan sync failed");
            }

            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
