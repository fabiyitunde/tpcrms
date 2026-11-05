using System.Text;
using System.Text.Json;
using CRMS.API.CustomerCheck;
using CRMS.Application.CustomerCheck;
using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CRMS.API.Controllers;

/// <summary>
/// Customer check for third-party systems (docs/Customer_Check.md): identity, bureau, fraud, bank
/// accounts and loans, CRMS facilities and an advisory for one BVN. Read-only.
/// <para>
/// A check calls several outside providers and takes a minute or two, so it does not run on the
/// request thread. POST stores a job and answers 202; the worker runs it and posts the result to
/// the consumer's callback; GET and the history search let the consumer fetch it at any time.
/// </para>
/// <para>
/// Auth: each consumer has its own key (X-Api-Key), issued from Admin > API Consumers and stored
/// hashed. Identifiers arrive only in POST bodies, never in a URL, and never appear in a log line,
/// error or reply beyond their last four digits. A consumer sees only its own jobs.
/// </para>
/// <para>
/// Exempt from the host's per-IP limiter; limited per consumer and overall instead, because a
/// partner's traffic may all arrive from one address.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/customer-check")]
[DisableRateLimiting]
public class CustomerCheckController : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ICustomerCheckService _service;
    private readonly IApiConsumerRepository _consumers;
    private readonly ICustomerCheckJobRepository _jobs;
    private readonly ICustomerCheckCipher _cipher;
    private readonly ICustomerCheckJobSignal _signal;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CustomerCheckRateLimiter _limiter;
    private readonly CustomerCheckSettings _settings;
    private readonly ILogger<CustomerCheckController> _logger;

    public CustomerCheckController(
        ICustomerCheckService service,
        IApiConsumerRepository consumers,
        ICustomerCheckJobRepository jobs,
        ICustomerCheckCipher cipher,
        ICustomerCheckJobSignal signal,
        IUnitOfWork unitOfWork,
        CustomerCheckRateLimiter limiter,
        IOptions<CustomerCheckSettings> settings,
        ILogger<CustomerCheckController> logger)
    {
        _service = service;
        _consumers = consumers;
        _jobs = jobs;
        _cipher = cipher;
        _signal = signal;
        _unitOfWork = unitOfWork;
        _limiter = limiter;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>POST /api/v1/customer-check: accept one check and queue it. Answers 202.</summary>
    [HttpPost]
    public async Task<IActionResult> Submit(CancellationToken ct)
    {
        var consumer = await AuthenticateAsync(ct);
        if (consumer is null)
            return Unauthorized(new { error = "unauthorized", message = "Invalid, missing or inactive API key." });

        if (!TryParse<CustomerCheckRequest>(await ReadBodyAsync(ct), out var request))
            return BadRequest(new { error = "invalid_payload", message = "The request body is not valid JSON." });

        var (query, errors) = CustomerCheckRules.Validate(request, _settings);
        var (callbackUrl, callbackError) = CustomerCheckRules.ResolveCallback(request!.CallbackUrl, consumer, _settings.RequireHttpsCallbacks);
        if (callbackError is not null) errors.Add(callbackError);
        if (query is null || errors.Count > 0)
            return BadRequest(new { error = "validation_failed", messages = errors });
        query = query with { CallbackUrl = callbackUrl };

        // The same request id again is the same job, not a second billed check. Checked before the
        // limits because a repeat costs nothing.
        var since = DateTime.UtcNow.AddHours(-Math.Max(1, _settings.IdempotencyWindowHours));
        var existing = await _jobs.FindByRequestIdAsync(consumer.Id, query.RequestId, since, ct);
        if (existing is not null)
            return Accepted(CustomerCheckJobIds.StatusPath(existing.Id), CustomerCheckJobMapping.Accepted(existing));

        var retryAfter = _limiter.TryAcquire(consumer.Id.ToString(), consumer.PerMinuteLimit, _settings.OverallLookupsPerMinute);
        if (retryAfter is not null)
        {
            Response.Headers.RetryAfter = retryAfter.Value.ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "rate_limited", message = $"Try again in {retryAfter} seconds." });
        }

        var open = await _jobs.CountOpenAsync(consumer.Id, ct);
        if (open >= consumer.MaxQueuedJobs)
        {
            Response.Headers.RetryAfter = "30";
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "queue_full", message = $"This consumer already has {open} checks waiting or running. Try again shortly." });
        }

        var job = CustomerCheckJob.Create(
            consumer.Id, consumer.Name, query.RequestId, _service.HashIdentifier(query.Bvn), CustomerCheckRules.Last4(query.Bvn),
            query.Sections, _cipher.Protect(JsonSerializer.Serialize(query, Json)), callbackUrl);
        job.SetAuditInfo(consumer.Id.ToString(), isNew: true);
        try
        {
            await _jobs.AddAsync(job, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Customer check: job for consumer {Consumer}, request {RequestId} could not be stored.", consumer.Name, query.RequestId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "unavailable", message = "The check could not be queued. Send it again." });
        }

        _signal.Nudge();
        _logger.LogInformation("Customer check job {JobId} queued for {Consumer}, request {RequestId}, callback {Callback}.",
            job.Id, consumer.Name, query.RequestId, callbackUrl is null ? "none" : "registered");
        return Accepted(CustomerCheckJobIds.StatusPath(job.Id), CustomerCheckJobMapping.Accepted(job));
    }

    /// <summary>GET /api/v1/customer-check/{jobId}: status and, once finished, the result.</summary>
    [HttpGet("{jobId}")]
    public async Task<IActionResult> Get(string jobId, CancellationToken ct)
    {
        var consumer = await AuthenticateAsync(ct);
        if (consumer is null)
            return Unauthorized(new { error = "unauthorized", message = "Invalid, missing or inactive API key." });

        var job = CustomerCheckJobIds.TryParse(jobId, out var id) ? await _jobs.GetByIdAsync(id, ct) : null;
        if (job is null || job.ConsumerId != consumer.Id)
            return NotFound(new { error = "not_found", message = "No check with that id belongs to this consumer." });

        JsonElement? result = null;
        if (job.Status == CustomerCheckJob.Completed && job.ResultCiphertext is not null)
        {
            try { result = JsonSerializer.Deserialize<JsonElement>(_cipher.Unprotect(job.ResultCiphertext), Json); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Customer check: the stored result of job {JobId} could not be read.", job.Id);
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { error = "unavailable", message = "The stored result could not be read." });
            }
        }
        return Ok(CustomerCheckJobMapping.View(job, result));
    }

    /// <summary>POST /api/v1/customer-check/history: the consumer's past checks, newest first. A
    /// POST because a BVN filter must not travel in a URL.</summary>
    [HttpPost("history")]
    public async Task<IActionResult> History(CancellationToken ct)
    {
        var consumer = await AuthenticateAsync(ct);
        if (consumer is null)
            return Unauthorized(new { error = "unauthorized", message = "Invalid, missing or inactive API key." });

        var raw = await ReadBodyAsync(ct);
        CustomerCheckHistoryRequest? request = new(null, null, null, null, null, null, null);
        if (!string.IsNullOrWhiteSpace(raw) && !TryParse(raw, out request))
            return BadRequest(new { error = "invalid_payload", message = "The request body is not valid JSON." });

        var (filter, errors) = CustomerCheckRules.ValidateHistory(request!, consumer.Id, _service.HashIdentifier, _settings);
        if (filter is null)
            return BadRequest(new { error = "validation_failed", messages = errors });

        var (items, total) = await _jobs.SearchAsync(filter, ct);
        return Ok(new CustomerCheckHistoryPage(filter.Page, filter.PageSize, total, items.Select(CustomerCheckJobMapping.HistoryItem).ToList()));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    private async Task<ApiConsumer?> AuthenticateAsync(CancellationToken ct)
    {
        Request.Headers.TryGetValue("X-Api-Key", out var provided);
        var key = provided.ToString();
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var consumer = await _consumers.GetByKeyHashAsync(ApiKeys.Hash(key), ct);
        if (consumer is null || !consumer.IsActive)
        {
            _logger.LogWarning("Customer check: {Outcome} API key from {RemoteIp}.",
                consumer is null ? "unknown" : "inactive", HttpContext.Connection.RemoteIpAddress);
            return null;
        }
        return consumer;
    }

    private async Task<string> ReadBodyAsync(CancellationToken ct)
    {
        if (Request.ContentLength is 0)
            return string.Empty;
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync(ct);
    }

    private static bool TryParse<T>(string raw, out T? value) where T : class
    {
        try
        {
            value = JsonSerializer.Deserialize<T>(raw, Json);
            return value is not null;
        }
        catch (JsonException)
        {
            value = null;
            return false;
        }
    }
}
