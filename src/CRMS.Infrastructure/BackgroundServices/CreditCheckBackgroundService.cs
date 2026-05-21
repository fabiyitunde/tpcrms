using CRMS.Application.Common;
using CRMS.Application.CreditBureau.Commands;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that processes credit check requests using a persistent outbox table.
/// Polls the database every 30 seconds for pending entries.
/// Survives app restarts and crashes — no items are lost because everything is in the DB.
/// </summary>
public class CreditCheckBackgroundService : BackgroundService
{
    private const int PollIntervalSeconds = 30;
    private const int MaxAttempts = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CreditCheckBackgroundService> _logger;

    public CreditCheckBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<CreditCheckBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Credit Check Background Service started (persistent outbox mode)");

        // Crash recovery: any entry left in Processing from a previous run is orphaned.
        // Reset them to Pending so they are picked up by the first poll cycle.
        await RecoverOrphanedEntriesAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingOutboxEntriesAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Unexpected error in credit check polling cycle");
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

        _logger.LogInformation("Credit Check Background Service stopped");
    }

    private async Task RecoverOrphanedEntriesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CRMSDbContext>();

        var orphaned = await db.CreditCheckOutbox
            .Where(e => e.Status == CreditCheckOutboxStatus.Processing)
            .ToListAsync(ct);

        if (orphaned.Count == 0) return;

        foreach (var entry in orphaned)
            entry.Status = CreditCheckOutboxStatus.Pending;

        await db.SaveChangesAsync(ct);
        _logger.LogWarning(
            "Recovered {Count} orphaned Processing outbox entries on startup — they will be retried this cycle",
            orphaned.Count);
    }

    private async Task ProcessPendingOutboxEntriesAsync(CancellationToken ct)
    {
        using var claimScope = _scopeFactory.CreateScope();
        var dbContext = claimScope.ServiceProvider.GetRequiredService<CRMSDbContext>();

        // Claim all pending entries that have not exceeded the retry limit
        var entries = await dbContext.CreditCheckOutbox
            .Where(e => e.Status == CreditCheckOutboxStatus.Pending && e.AttemptCount < MaxAttempts)
            .ToListAsync(ct);

        if (entries.Count == 0) return;

        _logger.LogInformation("Credit check outbox: found {Count} pending entries", entries.Count);

        foreach (var entry in entries)
        {
            entry.Status = CreditCheckOutboxStatus.Processing;
            entry.AttemptCount++;
        }

        // Persist the claimed status before processing — prevents duplicate processing
        // if the app crashes mid-batch (claimed entries won't be re-picked up as Pending)
        await dbContext.SaveChangesAsync(ct);

        // Process each entry in its own scope so failures are isolated
        foreach (var entry in entries)
        {
            await ProcessEntryAsync(entry.Id, entry.LoanApplicationId, entry.NampApplicationId, entry.SystemUserId, entry.AttemptCount, ct);
        }
    }

    private async Task ProcessEntryAsync(
        Guid entryId,
        Guid? loanApplicationId,
        Guid? nampApplicationId,
        Guid systemUserId,
        int attemptCount,
        CancellationToken ct)
    {
        using var processScope = _scopeFactory.CreateScope();
        var dbContext = processScope.ServiceProvider.GetRequiredService<CRMSDbContext>();

        var entry = await dbContext.CreditCheckOutbox.FindAsync([entryId], ct);
        if (entry == null) return;

        var applicationId = nampApplicationId.HasValue
            ? nampApplicationId.Value
            : loanApplicationId!.Value;

        try
        {
            _logger.LogInformation(
                "Processing credit checks for application {ApplicationId} (attempt {Attempt}/{Max})",
                applicationId, attemptCount, MaxAttempts);

            if (nampApplicationId.HasValue)
            {
                var nampHandler = processScope.ServiceProvider
                    .GetRequiredService<Application.CreditBureau.Commands.ProcessNampCreditChecksHandler>();
                var nampResult = await nampHandler.Handle(
                    new Application.CreditBureau.Commands.ProcessNampCreditChecksCommand(nampApplicationId.Value, systemUserId), ct);

                if (nampResult.IsSuccess)
                {
                    entry.Status = CreditCheckOutboxStatus.Completed;
                    entry.ProcessedAt = DateTime.UtcNow;
                    entry.ErrorMessage = null;

                    _logger.LogInformation(
                        "Credit check batch completed for NAMP application {NampApplicationId}: " +
                        "{Total} total, {Success} successful, {Failed} failed",
                        nampApplicationId.Value,
                        nampResult.Data!.TotalChecks,
                        nampResult.Data.Successful,
                        nampResult.Data.Failed);
                }
                else
                {
                    var isFinalAttempt = attemptCount >= MaxAttempts;
                    entry.Status = isFinalAttempt ? CreditCheckOutboxStatus.Failed : CreditCheckOutboxStatus.Pending;
                    entry.ErrorMessage = nampResult.Error;
                    if (isFinalAttempt) entry.ProcessedAt = DateTime.UtcNow;

                    _logger.LogWarning(
                        "Credit check batch failed for NAMP application {NampApplicationId} (attempt {Attempt}/{Max}): {Error}. {NextAction}",
                        nampApplicationId.Value, attemptCount, MaxAttempts, nampResult.Error,
                        isFinalAttempt ? "No more retries — marked as Failed." : "Will retry on next poll.");
                }
            }
            else
            {
                var handler = processScope.ServiceProvider
                    .GetRequiredService<IRequestHandler<ProcessLoanCreditChecksCommand, ApplicationResult<CreditCheckBatchResultDto>>>();
                var result = await handler.Handle(
                    new ProcessLoanCreditChecksCommand(loanApplicationId!.Value, systemUserId), ct);

                if (result.IsSuccess)
                {
                    entry.Status = CreditCheckOutboxStatus.Completed;
                    entry.ProcessedAt = DateTime.UtcNow;
                    entry.ErrorMessage = null;

                    _logger.LogInformation(
                        "Credit check batch completed for loan {LoanApplicationId}: " +
                        "{Total} total, {Success} successful, {Failed} failed, {NotFound} not found",
                        loanApplicationId.Value,
                        result.Data!.TotalChecks,
                        result.Data.Successful,
                        result.Data.Failed,
                        result.Data.NotFound);
                }
                else
                {
                    var isFinalAttempt = attemptCount >= MaxAttempts;
                    entry.Status = isFinalAttempt ? CreditCheckOutboxStatus.Failed : CreditCheckOutboxStatus.Pending;
                    entry.ErrorMessage = result.Error;
                    if (isFinalAttempt) entry.ProcessedAt = DateTime.UtcNow;

                    _logger.LogWarning(
                        "Credit check batch failed for loan {LoanApplicationId} (attempt {Attempt}/{Max}): {Error}. {NextAction}",
                        loanApplicationId.Value, attemptCount, MaxAttempts, result.Error,
                        isFinalAttempt ? "No more retries — marked as Failed." : "Will retry on next poll.");
                }
            }
        }
        catch (Exception ex)
        {
            var isFinalAttempt = attemptCount >= MaxAttempts;
            entry.Status = isFinalAttempt ? CreditCheckOutboxStatus.Failed : CreditCheckOutboxStatus.Pending;
            entry.ErrorMessage = ex.Message;
            if (isFinalAttempt) entry.ProcessedAt = DateTime.UtcNow;

            _logger.LogError(ex,
                "Exception processing credit checks for application {ApplicationId} (attempt {Attempt}/{Max}). {NextAction}",
                applicationId, attemptCount, MaxAttempts,
                isFinalAttempt ? "No more retries — marked as Failed." : "Will retry on next poll.");
        }

        await dbContext.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Adds a credit check outbox entry to the ambient DbContext WITHOUT saving.
/// The caller commits it atomically with the triggering state change in a single SaveChangesAsync.
/// </summary>
public class CreditCheckOutboxWriter : Application.CreditBureau.Interfaces.ICreditCheckOutbox
{
    private readonly CRMSDbContext _dbContext;

    public CreditCheckOutboxWriter(CRMSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnqueueAsync(Guid loanApplicationId, Guid systemUserId, CancellationToken ct = default)
    {
        var entry = new CreditCheckOutboxEntry
        {
            Id = Guid.NewGuid(),
            LoanApplicationId = loanApplicationId,
            SystemUserId = systemUserId,
            CreatedAt = DateTime.UtcNow,
            Status = CreditCheckOutboxStatus.Pending,
            AttemptCount = 0
        };

        await _dbContext.CreditCheckOutbox.AddAsync(entry, ct);
        // No SaveChangesAsync — the caller's SaveChangesAsync commits both this entry
        // and the triggering state change in one atomic transaction.
    }

    public async Task EnqueueForNampAsync(Guid nampApplicationId, Guid systemUserId, CancellationToken ct = default)
    {
        var entry = new CreditCheckOutboxEntry
        {
            Id = Guid.NewGuid(),
            NampApplicationId = nampApplicationId,
            SystemUserId = systemUserId,
            CreatedAt = DateTime.UtcNow,
            Status = CreditCheckOutboxStatus.Pending,
            AttemptCount = 0
        };
        await _dbContext.CreditCheckOutbox.AddAsync(entry, ct);
    }
}

