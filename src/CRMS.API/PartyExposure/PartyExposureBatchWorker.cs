using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using CRMS.API.Controllers;
using CRMS.Application.PartyExposure;
using CRMS.Domain.Aggregates.PartyExposure;
using CRMS.Infrastructure.ExternalServices.Rhshf;
using Microsoft.Extensions.Options;

namespace CRMS.API.PartyExposure;

/// <summary>One subject of a batch: the portal's reference and its validated identifiers, or why
/// it could not be looked up.</summary>
public record BatchSubjectWork(string? SubjectRef, ExposureQuery? Query, string? Error);

/// <summary>A batch waiting to be worked. The identifiers live only here, in memory.</summary>
public record BatchWork(Guid BatchId, IReadOnlyList<BatchSubjectWork> Subjects);

public class PartyExposureBatchQueue
{
    private readonly Channel<BatchWork> _channel = Channel.CreateUnbounded<BatchWork>(
        new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(BatchWork work, CancellationToken ct = default) => _channel.Writer.WriteAsync(work, ct);

    public IAsyncEnumerable<BatchWork> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

/// <summary>
/// Works batches one at a time, in small chunks with a pause between, each chunk in its own scope
/// (its own database context). A batch therefore never holds a connection for long or competes with
/// live CRMS work, and does not read balances from core banking — batch results are summaries for
/// the register, not live positions.
/// </summary>
public class PartyExposureBatchWorker : BackgroundService
{
    public const string BatchReadyEvent = "PARTY_EXPOSURE_BATCH_READY";

    private readonly PartyExposureBatchQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly PartyExposureSettings _settings;
    private readonly ILogger<PartyExposureBatchWorker> _logger;

    public PartyExposureBatchWorker(
        PartyExposureBatchQueue queue, IServiceScopeFactory scopes,
        IOptions<PartyExposureSettings> settings, ILogger<PartyExposureBatchWorker> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var failed = await scope.ServiceProvider.GetRequiredService<IPartyExposureService>()
                .FailInterruptedBatchesAsync(stoppingToken);
            if (failed > 0)
                _logger.LogWarning("Party exposure: {Count} batch(es) left unfinished by a restart were marked failed.", failed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Party exposure: could not mark interrupted batches as failed.");
        }

        await foreach (var work in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(work, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Party exposure: batch {BatchId} failed.", work.BatchId);
                await MarkFailedAsync(work.BatchId, "The CRMS could not finish this batch. Please send it again.");
            }
        }
    }

    private async Task ProcessAsync(BatchWork work, CancellationToken ct)
    {
        PartyExposureBatch? batch;
        using (var scope = _scopes.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IPartyExposureService>();
            batch = await service.GetBatchAsync(work.BatchId, ct);
            if (batch is null || batch.Status != PartyExposureBatch.Queued)
                return;
            batch.Start();
            await service.SaveBatchAsync(batch, ct);
        }

        var results = new List<ExposureBatchResult>(work.Subjects.Count);
        foreach (var chunk in work.Subjects.Chunk(Math.Max(1, _settings.BatchChunkSize)))
        {
            using (var scope = _scopes.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IPartyExposureService>();
                foreach (var subject in chunk)
                    results.Add(await LookupOneAsync(service, batch, subject, ct));

                var current = await service.GetBatchAsync(work.BatchId, ct);
                current?.Progress(results.Count);
                if (current is not null)
                    await service.SaveBatchAsync(current, ct);
            }

            if (_settings.BatchChunkPauseMs > 0)
                await Task.Delay(_settings.BatchChunkPauseMs, ct);
        }

        using (var scope = _scopes.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IPartyExposureService>();
            var done = await service.GetBatchAsync(work.BatchId, ct);
            if (done is null) return;
            done.Complete(JsonSerializer.Serialize(results, JsonSerializerOptions.Web));
            await service.SaveBatchAsync(done, ct);

            if (!string.IsNullOrWhiteSpace(done.CallbackUrl))
            {
                var callbacks = scope.ServiceProvider.GetRequiredService<IRhshfCallbackService>();
                var sent = await callbacks.SendAsync(done.CallbackUrl, new
                {
                    eventType = BatchReadyEvent,
                    eventId = $"{done.Id:N}-ready",
                    batchId = done.Id.ToString(),
                    requestId = done.RequestId,
                    status = done.Status,
                    subjectCount = done.SubjectCount,
                    pollUrl = PartyExposureController.BatchPath(done.Id),
                    occurredAt = done.CompletedAt,
                }, ct);
                done.RecordCallback(sent.StatusCode);
                await service.SaveBatchAsync(done, ct);
                if (!sent.Succeeded)
                    _logger.LogWarning("Party exposure: batch {BatchId} ready, but the callback returned {Status}. Polling still works.",
                        done.Id, sent.StatusCode?.ToString() ?? sent.ErrorMessage);
            }
        }
    }

    private async Task<ExposureBatchResult> LookupOneAsync(
        IPartyExposureService service, PartyExposureBatch batch, BatchSubjectWork subject, CancellationToken ct)
    {
        if (subject.Query is null)
            return new ExposureBatchResult(subject.SubjectRef, MatchStatuses.Error, [], true, null, subject.Error);

        var q = subject.Query;
        var timer = Stopwatch.StartNew();
        try
        {
            var reply = await service.LookupAsync(batch.RequestId, q, ct);
            await service.RecordLookupAsync(PartyExposureLookupLog.Record(
                batch.RequestId, batch.Purpose, batch.RequestedByUserId, batch.RequestedByRole, subject.SubjectRef,
                batch.Id, Hash(service, q.Bvn), Hash(service, q.Nin), q.Nubans.Select(service.HashIdentifier).ToList(),
                reply.MatchStatus, reply.Relationships.Count, reply.Summary.DirectFacilities, reply.Summary.IndirectFacilities,
                reply.Truncated, !reply.IdentityConsistency.Consistent, (int)timer.ElapsedMilliseconds, null), ct);
            return new ExposureBatchResult(
                subject.SubjectRef, reply.MatchStatus, reply.IdentifierResults, reply.IdentityConsistency.Consistent,
                reply.Summary, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Party exposure: one subject of batch {BatchId} failed.", batch.Id);
            return new ExposureBatchResult(subject.SubjectRef, MatchStatuses.Error, [], true, null,
                "The CRMS could not complete this lookup.");
        }
    }

    private static string? Hash(IPartyExposureService service, string? value)
        => value is null ? null : service.HashIdentifier(value);

    private async Task MarkFailedAsync(Guid batchId, string reason)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPartyExposureService>();
            var batch = await service.GetBatchAsync(batchId);
            if (batch is null) return;
            batch.Fail(reason);
            await service.SaveBatchAsync(batch);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Party exposure: could not mark batch {BatchId} failed.", batchId);
        }
    }
}
