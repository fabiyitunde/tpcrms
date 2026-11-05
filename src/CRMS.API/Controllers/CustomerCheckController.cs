using System.Text;
using System.Text.Json;
using CRMS.API.CustomerCheck;
using CRMS.Application.CustomerCheck;
using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using CRMS.API.OpenApi;

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
[ApiKeyAuth("Your consumer key, issued by a bank administrator under Administration > API Consumers.")]
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
    /// <remarks>The check does not run while you wait. This call validates the request, stores a job and answers <c>202</c> with a <c>jobId</c> and a <c>statusUrl</c>; a background worker then runs the sections you asked for. <para>Only <c>requestId</c> and <c>bvn</c> are required. <c>sections</c> defaults to all seven; ask only for what you need, because bureau, fraud and advisory calls are billed. <c>callbackUrl</c> is optional and must be on a host the bank has allowed for you; without it, your registered callback URL is used, and without either you fetch the result from <c>statusUrl</c>.</para> <para>Sending the same <c>requestId</c> again within 24 hours returns the existing job rather than a second, billed check.</para> <para>A section you are not entitled to is not an error: it comes back with status <c>NOT_ALLOWED</c>.</para></remarks>
    [HttpPost]
    [RequestBodyDoc(typeof(CustomerCheckRequest), Example = """{"requestId":"c7f1e2a0-9b1d-4e4e-8d0a-7b9f3c2d1a55","bvn":"22212344321","sections":["identity","creditBureau","fraudCheck","bankAccounts","bankLoans","crmsFacilities","aiAdvisory"],"options":{"activityWindowMonths":12,"includeScore":true,"runAmlCheck":false,"loanAmount":500000,"tenorMonths":12}}""")]
    [ProducesResponseType(typeof(CustomerCheckAccepted), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
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
    /// <remarks>Poll this until <c>status</c> is <c>COMPLETED</c> or <c>FAILED</c>. <c>result</c> is present only when completed and is the same object your callback receives. <c>callback</c> tells you whether the callback was delivered, how many attempts were made, and when the next one is due. Fetching a stored result is free and can be repeated at any time. You can only read your own jobs.</remarks>
    [HttpGet("{jobId}")]
    [ProducesResponseType(typeof(CustomerCheckJobView), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
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
    /// <remarks>Every field of the body is optional; an empty body lists your most recent checks. <c>bvn</c> is matched on its keyed hash and is never stored or logged in plain form. Items carry no results; fetch any one with its <c>jobId</c>.</remarks>
    [HttpPost("history")]
    [RequestBodyDoc(typeof(CustomerCheckHistoryRequest), Example = """{"from":"2026-11-01","to":"2026-11-30","status":"COMPLETED","page":1,"pageSize":50}""")]
    [ProducesResponseType(typeof(CustomerCheckHistoryPage), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
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
