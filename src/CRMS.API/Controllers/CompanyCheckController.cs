using System.Text;
using System.Text.Json;
using CRMS.API.CustomerCheck;
using CRMS.API.OpenApi;
using CRMS.Application.CustomerCheck;
using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CRMS.API.Controllers;

/// <summary>
/// Company credit check for third-party systems (docs/Customer_Credit_Check.md, "Company credit
/// check"): CAC registration, business bureau, business fraud, bank accounts and loans, CRMS cases
/// and an advisory for one registration number. Read-only.
/// <para>
/// The same pipeline as the customer (individual) check: the same key, per-consumer and overall
/// limits, queue cap, idempotency, worker, callbacks and signing. Its jobs sit in the same table
/// marked COMPANY, and each endpoint only ever sees its own kind. A consumer may call it only once
/// an administrator has turned company checks on for it.
/// </para>
/// </summary>
[ApiController]
[ApiKeyAuth("Your consumer key, issued by a bank administrator under Administration > API Consumers.")]
[Route("api/v1/company-credit-check")]
[DisableRateLimiting]
public class CompanyCheckController : ControllerBase
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
    private readonly ILogger<CompanyCheckController> _logger;

    public CompanyCheckController(
        ICustomerCheckService service,
        IApiConsumerRepository consumers,
        ICustomerCheckJobRepository jobs,
        ICustomerCheckCipher cipher,
        ICustomerCheckJobSignal signal,
        IUnitOfWork unitOfWork,
        CustomerCheckRateLimiter limiter,
        IOptions<CustomerCheckSettings> settings,
        ILogger<CompanyCheckController> logger)
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

    /// <summary>POST /api/v1/company-credit-check: accept one company check and queue it. Answers 202.</summary>
    /// <remarks>Works like the customer credit check, for a company. Only <c>requestId</c>, <c>rcNumber</c> and <c>companyName</c> are required; the registration number keeps its prefix (<c>RC</c>, <c>BN</c> or <c>IT</c>), and a number with none is read as <c>RC</c>. <para>Core banking finds a company only by one of its account numbers, so give at least one in <c>nubans</c> if you want the <c>bankAccounts</c> and <c>bankLoans</c> sections; without one they come back <c>NOT_POSSIBLE</c>. An account registered to a different company is left out and named in the reason.</para> <para><c>sections</c> defaults to all seven: <c>companyRegistration</c>, <c>creditBureau</c>, <c>fraudCheck</c>, <c>bankAccounts</c>, <c>bankLoans</c>, <c>crmsFacilities</c>, <c>aiAdvisory</c>. A section you are not entitled to comes back <c>NOT_ALLOWED</c>.</para> <para>Answers <c>403</c> with <c>company_checks_not_enabled</c> until the bank switches company checks on for your consumer.</para></remarks>
    [HttpPost]
    [RequestBodyDoc(typeof(CompanyCheckRequest),
        Example = """{"requestId":"b2c4e6f8-1a3c-4e5a-9b7d-2f4e6a8c0e1d","rcNumber":"RC123456","companyName":"Gote Agricultural Holdings Limited"}""",
        FullExample = """{"requestId":"b2c4e6f8-1a3c-4e5a-9b7d-2f4e6a8c0e1d","rcNumber":"RC123456","companyName":"Gote Agricultural Holdings Limited","nubans":["0123455678"],"sections":["companyRegistration","creditBureau","fraudCheck","bankAccounts","bankLoans","crmsFacilities","aiAdvisory"],"callbackUrl":"https://partner.example.com/crms/company-credit-check","options":{"activityWindowMonths":12,"runAmlCheck":false,"loanAmount":5000000,"tenorMonths":12,"annualRevenue":40000000,"loanPurpose":"Dry-season inputs"}}""")]
    [ProducesResponseType(typeof(CustomerCheckAccepted), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Submit(CancellationToken ct)
    {
        var consumer = await AuthenticateAsync(ct);
        if (consumer is null)
            return Unauthorized(new { error = "unauthorized", message = "Invalid, missing or inactive API key." });
        if (!consumer.AllowCompanyChecks)
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = CustomerCheckErrors.CompanyChecksNotEnabled,
                message = "Company checks are not switched on for this consumer. Ask the bank to enable them.",
            });

        if (!TryParse<CompanyCheckRequest>(await ReadBodyAsync(ct), out var request))
            return BadRequest(new { error = "invalid_payload", message = "The request body is not valid JSON." });

        var (query, errors) = CompanyCheckRules.Validate(request, _settings);
        var (callbackUrl, callbackError) = CustomerCheckRules.ResolveCallback(request!.CallbackUrl, consumer, _settings.RequireHttpsCallbacks);
        if (callbackError is not null) errors.Add(callbackError);
        if (query is null || errors.Count > 0)
            return BadRequest(new { error = "validation_failed", messages = errors });
        query = query with { CallbackUrl = callbackUrl };

        var since = DateTime.UtcNow.AddHours(-Math.Max(1, _settings.IdempotencyWindowHours));
        var existing = await _jobs.FindByRequestIdAsync(consumer.Id, query.RequestId, since, CustomerCheckJob.Company, ct);
        if (existing is not null)
            return Accepted(CustomerCheckJobIds.StatusPath(existing), CustomerCheckJobMapping.Accepted(existing));

        var retryAfter = _limiter.TryAcquire(consumer.Id.ToString(), consumer.PerMinuteLimit, _settings.OverallLookupsPerMinute);
        if (retryAfter is not null)
        {
            Response.Headers.RetryAfter = retryAfter.Value.ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "rate_limited", message = $"Try again in {retryAfter} seconds." });
        }

        // One queue cap across both kinds of check: it protects the same worker.
        var open = await _jobs.CountOpenAsync(consumer.Id, ct);
        if (open >= consumer.MaxQueuedJobs)
        {
            Response.Headers.RetryAfter = "30";
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "queue_full", message = $"This consumer already has {open} checks waiting or running. Try again shortly." });
        }

        var job = CustomerCheckJob.Create(
            consumer.Id, consumer.Name, query.RequestId, _service.HashIdentifier(CompanyCheckRules.HashInput(query.RcNumber)),
            CustomerCheckRules.Last4(query.RcDigits), query.Sections, _cipher.Protect(JsonSerializer.Serialize(query, Json)),
            callbackUrl, CustomerCheckJob.Company);
        job.SetAuditInfo(consumer.Id.ToString(), isNew: true);
        try
        {
            await _jobs.AddAsync(job, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Company credit check: job for consumer {Consumer}, request {RequestId} could not be stored.", consumer.Name, query.RequestId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "unavailable", message = "The check could not be queued. Send it again." });
        }

        _signal.Nudge();
        _logger.LogInformation("Company credit check job {JobId} queued for {Consumer}, request {RequestId}, callback {Callback}.",
            job.Id, consumer.Name, query.RequestId, callbackUrl is null ? "none" : "registered");
        return Accepted(CustomerCheckJobIds.StatusPath(job), CustomerCheckJobMapping.Accepted(job));
    }

    /// <summary>GET /api/v1/company-credit-check/{jobId}: status and, once finished, the result.</summary>
    /// <remarks>Poll this until <c>status</c> is <c>COMPLETED</c> or <c>FAILED</c>. <c>result</c> is present only when completed and is the object described as CompanyCheckResponse, exactly as your callback receives it. Fetching a stored result is free and can be repeated. You can only read your own company checks.</remarks>
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
        if (job is null || job.ConsumerId != consumer.Id || !job.IsCompany)
            return NotFound(new { error = "not_found", message = "No company check with that id belongs to this consumer." });

        JsonElement? result = null;
        if (job.Status == CustomerCheckJob.Completed && job.ResultCiphertext is not null)
        {
            try { result = JsonSerializer.Deserialize<JsonElement>(_cipher.Unprotect(job.ResultCiphertext), Json); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Company credit check: the stored result of job {JobId} could not be read.", job.Id);
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { error = "unavailable", message = "The stored result could not be read." });
            }
        }
        return Ok(CustomerCheckJobMapping.View(job, result));
    }

    /// <summary>POST /api/v1/company-credit-check/history: the consumer's past company checks, newest first.</summary>
    /// <remarks>Every field of the body is optional; an empty body lists your most recent company checks. <c>rcNumber</c> is matched on its keyed hash. Items carry no results; fetch any one with its <c>jobId</c>.</remarks>
    [HttpPost("history")]
    [RequestBodyDoc(typeof(CompanyCheckHistoryRequest), Example = """{"from":"2026-11-01","to":"2026-11-30","status":"COMPLETED","rcNumber":"RC123456","page":1,"pageSize":50}""")]
    [ProducesResponseType(typeof(CompanyCheckHistoryPage), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> History(CancellationToken ct)
    {
        var consumer = await AuthenticateAsync(ct);
        if (consumer is null)
            return Unauthorized(new { error = "unauthorized", message = "Invalid, missing or inactive API key." });

        var raw = await ReadBodyAsync(ct);
        CompanyCheckHistoryRequest? request = new(null, null, null, null, null, null, null);
        if (!string.IsNullOrWhiteSpace(raw) && !TryParse(raw, out request))
            return BadRequest(new { error = "invalid_payload", message = "The request body is not valid JSON." });

        var (filter, errors) = CompanyCheckRules.ValidateHistory(request!, consumer.Id, _service.HashIdentifier, _settings);
        if (filter is null)
            return BadRequest(new { error = "validation_failed", messages = errors });

        var (items, total) = await _jobs.SearchAsync(filter, ct);
        return Ok(new CompanyCheckHistoryPage(filter.Page, filter.PageSize, total, items.Select(CustomerCheckJobMapping.CompanyHistoryItem).ToList()));
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
            _logger.LogWarning("Company credit check: {Outcome} API key from {RemoteIp}.",
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
