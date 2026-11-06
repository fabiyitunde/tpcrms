using System.Diagnostics;
using System.Text.Json;
using CRMS.Application.CustomerCheck;
using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.BackgroundServices;

/// <summary>Wakes the worker when a job is queued. One pending wake-up is enough; extra nudges
/// while one is pending are dropped, since the worker drains the whole queue when it wakes.</summary>
public class CustomerCheckJobSignal : ICustomerCheckJobSignal
{
    private readonly SemaphoreSlim _gate = new(0, 1);

    public void Nudge()
    {
        try { _gate.Release(); }
        catch (SemaphoreFullException) { }
    }

    /// <summary>Returns when nudged, when the timeout passes, or when stopping.</summary>
    public async Task WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        try { await _gate.WaitAsync(timeout, ct); }
        catch (OperationCanceledException) { }
    }
}

/// <summary>
/// Runs one claimed job: decrypts the request, runs the check, writes the audit row, stores the
/// encrypted result and schedules the callback. The job, its audit row and the consumer's
/// last-used stamp are saved together: no result without its audit entry. If that save fails the
/// job stays PROCESSING and is claimed again once stale, up to the configured number of runs.
/// </summary>
public class CustomerCheckJobRunner
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ICustomerCheckJobRepository _jobs;
    private readonly IApiConsumerRepository _consumers;
    private readonly ICustomerCheckService _service;
    private readonly ICustomerCheckCipher _cipher;
    private readonly CustomerCheckSettings _settings;
    private readonly ILogger<CustomerCheckJobRunner> _logger;

    public CustomerCheckJobRunner(
        ICustomerCheckJobRepository jobs, IApiConsumerRepository consumers, ICustomerCheckService service,
        ICustomerCheckCipher cipher, IOptions<CustomerCheckSettings> settings, ILogger<CustomerCheckJobRunner> logger)
    {
        _jobs = jobs;
        _consumers = consumers;
        _service = service;
        _cipher = cipher;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task RunAsync(Guid jobId, CancellationToken stopping)
    {
        var job = await _jobs.GetByIdAsync(jobId, stopping);
        if (job is null || job.Status != CustomerCheckJob.Processing)
            return;

        var now = DateTime.UtcNow;
        if (job.RunAttempts > _settings.MaxRunAttempts)
        {
            _logger.LogWarning("Customer credit check job {JobId} was interrupted {Attempts} times; failing it.", job.Id, job.RunAttempts - 1);
            job.Fail(CustomerCheckErrors.Interrupted, "The check was interrupted by a restart more than once. Send it again.", 0, now);
            await SaveWithoutAuditAsync(job, stopping);
            return;
        }

        var consumer = await _consumers.GetByIdAsync(job.ConsumerId, stopping);
        if (consumer is null || !consumer.IsActive)
        {
            job.Fail(CustomerCheckErrors.ConsumerInactive, "The consumer was switched off before the check ran.", 0, now);
            await SaveWithoutAuditAsync(job, stopping);
            return;
        }

        CustomerCheckQuery? query = null;
        try { query = JsonSerializer.Deserialize<CustomerCheckQuery>(_cipher.Unprotect(job.RequestCiphertext), Json); }
        catch (Exception ex) { _logger.LogError(ex, "Customer credit check job {JobId}: the stored request could not be read.", job.Id); }
        if (query is null)
        {
            job.Fail(CustomerCheckErrors.CheckFailed, "The stored request could not be read. Send it again.", 0, now);
            await SaveWithoutAuditAsync(job, stopping);
            return;
        }

        var watch = Stopwatch.StartNew();
        CustomerCheckOutcome? outcome = null;
        string code = CustomerCheckErrors.CheckFailed;
        string? failure = null;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stopping);
            cts.CancelAfter(_settings.JobTimeoutMs);
            outcome = await _service.CheckAsync(consumer, query, cts.Token);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // Shutting down mid-check: leave the row PROCESSING so it is claimed again after the restart.
            throw;
        }
        catch (OperationCanceledException)
        {
            code = CustomerCheckErrors.TimedOut;
            failure = "The check did not finish within the time allowed.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Customer credit check job {JobId} failed for consumer {Consumer}.", job.Id, consumer.Name);
            failure = "The check could not be completed.";
        }
        watch.Stop();
        var elapsed = (int)Math.Min(int.MaxValue, watch.ElapsedMilliseconds);
        now = DateTime.UtcNow;

        var outcomes = outcome is null
            ? query.Sections.Select(s => new KeyValuePair<string, string>(s, "ERROR")).ToList()
            : Outcomes(outcome.Response);
        var log = CustomerCheckLog.Record(
            job.RequestId, consumer.Id, consumer.Name, job.BvnHash, query.Nubans.Select(_service.HashIdentifier).ToList(),
            query.Sections, outcomes, outcome?.BureauUsed, outcome?.BureauReused ?? false,
            outcome?.Billed ?? BilledCallCounts.None, elapsed, failure is null ? null : code, job.Id);

        if (outcome is null)
            job.Fail(code, failure!, elapsed, now);
        else
        {
            var advisory = outcome.Response.AiAdvisory;
            var scored = advisory.Status == SectionStatuses.Ok;
            job.Complete(
                _cipher.Protect(JsonSerializer.Serialize(outcome.Response, Json)), outcomes,
                outcome.BureauUsed, outcome.BureauReused, outcome.Billed.Total, elapsed, now,
                scored ? advisory.OverallScore : null, scored ? advisory.OverallRating : null, scored ? advisory.Recommendation : null);
        }

        consumer.Touch();
        _consumers.Update(consumer);
        _jobs.Update(job);
        try
        {
            // RecordAsync adds the audit row and saves the scope, so the job, the row and the
            // consumer stamp land together or not at all.
            await _service.RecordAsync(log, stopping);
            _logger.LogInformation("Customer credit check job {JobId} {Status} for {Consumer} in {Ms} ms.", job.Id, job.Status, consumer.Name, elapsed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Customer credit check job {JobId}: the result could not be saved; it will be run again.", job.Id);
        }
    }

    private async Task SaveWithoutAuditAsync(CustomerCheckJob job, CancellationToken ct)
    {
        _jobs.Update(job);
        try { await _service.RecordAsync(CustomerCheckLog.Record(
                job.RequestId, job.ConsumerId, job.ConsumerName, job.BvnHash, [], job.SectionList,
                job.SectionList.Select(s => new KeyValuePair<string, string>(s, "ERROR")), null, false,
                BilledCallCounts.None, 0, job.ErrorCode, job.Id), ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Customer credit check job {JobId}: failure could not be saved.", job.Id);
        }
    }

    public static List<KeyValuePair<string, string>> Outcomes(CustomerCheckResponse r) =>
    [
        new(CustomerCheckSections.Identity, r.Identity.Status),
        new(CustomerCheckSections.CreditBureau, r.CreditBureau.Status),
        new(CustomerCheckSections.FraudCheck, r.FraudCheck.Status),
        new(CustomerCheckSections.BankAccounts, r.BankAccounts.Status),
        new(CustomerCheckSections.BankLoans, r.BankLoans.Status),
        new(CustomerCheckSections.CrmsFacilities, r.CrmsFacilities.Status),
        new(CustomerCheckSections.AiAdvisory, r.AiAdvisory.Status),
    ];
}

/// <summary>
/// Delivers due callbacks and schedules retries: immediately, then after 1, 3, 7, 15 and 15
/// minutes, six attempts in all. The envelope carries the stored result verbatim.
/// </summary>
public class CustomerCheckCallbackDispatcher
{
    public static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(3),
        TimeSpan.FromMinutes(7),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(15),
    ];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ICustomerCheckJobRepository _jobs;
    private readonly IApiConsumerRepository _consumers;
    private readonly ICustomerCheckCallbackSender _sender;
    private readonly ICustomerCheckCipher _cipher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CustomerCheckCallbackDispatcher> _logger;

    public CustomerCheckCallbackDispatcher(
        ICustomerCheckJobRepository jobs, IApiConsumerRepository consumers, ICustomerCheckCallbackSender sender,
        ICustomerCheckCipher cipher, IUnitOfWork unitOfWork, ILogger<CustomerCheckCallbackDispatcher> logger)
    {
        _jobs = jobs;
        _consumers = consumers;
        _sender = sender;
        _cipher = cipher;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<int> ProcessDueAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var due = await _jobs.GetDueCallbackIdsAsync(now, 20, ct);
        var processed = 0;
        foreach (var id in due)
        {
            ct.ThrowIfCancellationRequested();
            var job = await _jobs.GetByIdAsync(id, ct);
            if (job is null || job.CallbackStatus != CustomerCheckJob.CallbackPending || job.CallbackUrl is null || job.NextCallbackAt > now)
                continue;
            await DeliverAsync(job, ct);
            processed++;
        }
        return processed;
    }

    public async Task DeliverAsync(CustomerCheckJob job, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var consumer = await _consumers.GetByIdAsync(job.ConsumerId, ct);
        string? secret = null;
        if (consumer?.CallbackSecretCiphertext is { } ciphertext)
        {
            try { secret = _cipher.Unprotect(ciphertext); }
            catch (Exception ex) { _logger.LogError(ex, "Customer credit check: the callback secret for consumer {Consumer} could not be read.", job.ConsumerName); }
        }

        CallbackSendResult sent;
        if (secret is null)
        {
            sent = new CallbackSendResult(false, null, "The consumer has no usable callback signing secret.");
        }
        else
        {
            var envelope = BuildEnvelope(job);
            var body = JsonSerializer.Serialize(envelope, Json);
            sent = await _sender.SendAsync(job.CallbackUrl!, secret, job.EventId, envelope.EventType, body, ct);
        }

        DateTime? next = sent.Succeeded ? null
            : job.CallbackAttempts < RetryDelays.Length ? now.Add(RetryDelays[job.CallbackAttempts])
            : null;
        job.RecordCallbackAttempt(sent.Succeeded, sent.StatusCode, sent.Error, next, now);
        _jobs.Update(job);
        await _unitOfWork.SaveChangesAsync(ct);

        if (sent.Succeeded)
            _logger.LogInformation("Customer credit check callback {EventId} delivered on attempt {Attempt}.", job.EventId, job.CallbackAttempts);
        else if (next is not null)
            _logger.LogWarning("Customer credit check callback {EventId} attempt {Attempt} failed ({Error}); next at {Next:u}.", job.EventId, job.CallbackAttempts, sent.Error, next);
        else
            _logger.LogWarning("Customer credit check callback {EventId} gave up after {Attempt} attempts ({Error}). The result waits to be fetched.", job.EventId, job.CallbackAttempts, sent.Error);
    }

    public CustomerCheckCallbackEnvelope BuildEnvelope(CustomerCheckJob job)
    {
        var completed = job.Status == CustomerCheckJob.Completed;
        JsonElement? result = null;
        if (completed && job.ResultCiphertext is not null)
            result = JsonSerializer.Deserialize<JsonElement>(_cipher.Unprotect(job.ResultCiphertext), Json);
        return new CustomerCheckCallbackEnvelope(
            completed ? CustomerCheckEvents.Completed : CustomerCheckEvents.Failed,
            job.EventId, CustomerCheckJobIds.Format(job.Id), job.RequestId, job.Status,
            job.CompletedAt ?? DateTime.UtcNow, CustomerCheckJobIds.StatusPath(job.Id),
            completed ? null : new CustomerCheckJobError(job.ErrorCode ?? CustomerCheckErrors.CheckFailed, job.ErrorMessage ?? "The check could not be completed."),
            result);
    }
}

/// <summary>
/// The poll loop: claims queued jobs up to the configured concurrency, runs each in its own scope,
/// and delivers due callbacks. Sleeps between passes unless a new job nudges it. Only the API host
/// runs this; the intranet never does.
/// </summary>
public class CustomerCheckJobWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly CustomerCheckJobSignal _signal;
    private readonly CustomerCheckSettings _settings;
    private readonly ILogger<CustomerCheckJobWorker> _logger;

    public CustomerCheckJobWorker(
        IServiceScopeFactory scopes, CustomerCheckJobSignal signal,
        IOptions<CustomerCheckSettings> settings, ILogger<CustomerCheckJobWorker> logger)
    {
        _scopes = scopes;
        _signal = signal;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Customer credit check worker started (concurrency {Concurrency}).", _settings.WorkerConcurrency);
        var running = new List<Task>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                running.RemoveAll(t => t.IsCompleted);
                while (running.Count < Math.Max(1, _settings.WorkerConcurrency))
                {
                    var id = await ClaimAsync(stoppingToken);
                    if (id is null) break;
                    running.Add(RunOneAsync(id.Value, stoppingToken));
                }

                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<CustomerCheckCallbackDispatcher>().ProcessDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Customer credit check worker: error in a polling pass.");
            }

            await _signal.WaitAsync(TimeSpan.FromSeconds(Math.Max(1, _settings.WorkerPollSeconds)), stoppingToken);
        }

        try { await Task.WhenAll(running); }
        catch (Exception) { }
        _logger.LogInformation("Customer credit check worker stopped.");
    }

    private async Task<Guid?> ClaimAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<ICustomerCheckJobRepository>();
        var now = DateTime.UtcNow;
        return await jobs.ClaimNextAsync(now, now.AddMinutes(-Math.Max(1, _settings.StaleProcessingMinutes)), ct);
    }

    private async Task RunOneAsync(Guid jobId, CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<CustomerCheckJobRunner>().RunAsync(jobId, ct);
            _signal.Nudge();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Customer credit check worker: job {JobId} threw.", jobId);
        }
    }
}
