using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CRMS.API.PartyExposure;
using CRMS.Application.PartyExposure;
using CRMS.Domain.Aggregates.PartyExposure;
using CRMS.Infrastructure.ExternalServices.Rhshf;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CRMS.API.Controllers;

/// <summary>
/// Party-exposure lookup for the RH-SHF portal's Farmer 360 (CRMSQA#4): every CRMS facility a person
/// is involved in, found by BVN, NIN or account number. Read-only — it never changes a case.
/// <para>
/// Same caller and key as the RH-SHF submission API (X-Api-Key), plus a signed request
/// (<see cref="PortalRequestSignature"/>). Identifiers arrive only in POST bodies, never in a URL,
/// and never appear in a log line, error or reply — the reply carries the last four digits at most.
/// </para>
/// <para>
/// Exempt from the host's per-IP limiter, which would cap every portal user together at 100 a
/// minute; this endpoint limits per portal user and overall instead, and the batch endpoint is the
/// only bulk path.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/party-exposure")]
[DisableRateLimiting]
public class PartyExposureController : ControllerBase
{
    private readonly IPartyExposureService _service;
    private readonly PartyExposureRateLimiter _limiter;
    private readonly PartyExposureBatchQueue _queue;
    private readonly PartyExposureSettings _settings;
    private readonly RhshfSettings _rhshf;
    private readonly ILogger<PartyExposureController> _logger;

    public PartyExposureController(
        IPartyExposureService service,
        PartyExposureRateLimiter limiter,
        PartyExposureBatchQueue queue,
        IOptions<PartyExposureSettings> settings,
        IOptions<RhshfSettings> rhshf,
        ILogger<PartyExposureController> logger)
    {
        _service = service;
        _limiter = limiter;
        _queue = queue;
        _settings = settings.Value;
        _rhshf = rhshf.Value;
        _logger = logger;
    }

    public static string BatchPath(Guid batchId) => $"/api/v1/party-exposure/lookup-batch/{batchId}";

    /// <summary>POST /api/v1/party-exposure/lookup — one person, full detail.</summary>
    [HttpPost("lookup")]
    public async Task<IActionResult> Lookup(CancellationToken ct)
    {
        var (raw, refusal) = await AuthenticateAsync(ct);
        if (refusal is not null) return refusal;

        if (!TryParse<PartyExposureLookupRequest>(raw, out var request))
            return BadRequest(new { error = "invalid_payload", message = "The request body is not valid JSON." });

        var idempotencyRefusal = CheckIdempotencyKey(request!.RequestId);
        if (idempotencyRefusal is not null) return idempotencyRefusal;

        var (query, errors) = PartyExposureRules.Validate(
            request.RequestId, request.Purpose, request.Identifiers, request.Include, fetchLiveBalances: true);
        if (query is null)
            return BadRequest(new { error = "validation_failed", messages = errors });

        var userKey = request.RequestedBy?.PortalUserId ?? "unknown";
        var retryAfter = _limiter.TryAcquire(userKey, _settings.PerUserLookupsPerMinute, _settings.OverallLookupsPerMinute);
        if (retryAfter is not null)
        {
            Response.Headers.RetryAfter = retryAfter.Value.ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "rate_limited", message = "Too many lookups. Try again shortly.", retryAfterSeconds = retryAfter });
        }

        var timer = Stopwatch.StartNew();
        PartyExposureResponse reply;
        try
        {
            reply = await _service.LookupAsync(request.RequestId!, query, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Party exposure lookup {RequestId} failed.", request.RequestId);
            await TryAuditAsync(request, query, MatchStatuses.Error, null, timer, "The lookup failed inside the CRMS.", ct);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { requestId = request.RequestId, matchStatus = MatchStatuses.Error, message = "The CRMS could not complete this lookup." });
        }

        // No audit row, no answer: a lookup that cannot be accounted for is not given out.
        if (!await TryAuditAsync(request, query, reply.MatchStatus, reply, timer, null, ct))
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { requestId = request.RequestId, matchStatus = MatchStatuses.Error, message = "The CRMS could not record this lookup, so it was not answered." });

        _logger.LogInformation(
            "Party exposure lookup {RequestId} for {Purpose} by {User}: {MatchStatus}, {Count} relationship(s), {Ms}ms.",
            request.RequestId, request.Purpose, userKey, reply.MatchStatus, reply.Relationships.Count, timer.ElapsedMilliseconds);
        return Ok(reply);
    }

    /// <summary>POST /api/v1/party-exposure/lookup-batch — up to 500 people, summaries only, worked in
    /// the background. Answers 202 with a batch id to poll.</summary>
    [HttpPost("lookup-batch")]
    public async Task<IActionResult> LookupBatch(CancellationToken ct)
    {
        var (raw, refusal) = await AuthenticateAsync(ct);
        if (refusal is not null) return refusal;

        if (!TryParse<PartyExposureBatchRequest>(raw, out var request))
            return BadRequest(new { error = "invalid_payload", message = "The request body is not valid JSON." });

        var idempotencyRefusal = CheckIdempotencyKey(request!.RequestId);
        if (idempotencyRefusal is not null) return idempotencyRefusal;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Length > 100)
            errors.Add("requestId is required and must be at most 100 characters.");
        if (string.IsNullOrWhiteSpace(request.Purpose) || !ExposurePurposes.All.Contains(request.Purpose))
            errors.Add($"purpose must be one of {string.Join(", ", ExposurePurposes.All)}.");
        var subjects = request.Subjects ?? [];
        if (subjects.Count == 0 || subjects.Count > _settings.BatchMaxSubjects)
            errors.Add($"subjects must hold between 1 and {_settings.BatchMaxSubjects} entries.");
        var months = request.Include?.HistoryMonths ?? PartyExposureRules.DefaultHistoryMonths;
        if (months < 1 || months > PartyExposureRules.MaxHistoryMonths)
            errors.Add($"include.historyMonths must be between 1 and {PartyExposureRules.MaxHistoryMonths}.");
        string? callbackUrl = null;
        if (!string.IsNullOrWhiteSpace(request.CallbackUrl))
        {
            if (!IsAllowedCallback(request.CallbackUrl))
                errors.Add("callbackUrl is not on the CRMS's list of allowed portal hosts.");
            else
                callbackUrl = request.CallbackUrl;
        }
        if (errors.Count > 0)
            return BadRequest(new { error = "validation_failed", messages = errors });

        // The same requestId sent again is the same batch, not a second one.
        var existing = await _service.FindBatchByRequestIdAsync(request.RequestId!, ct);
        if (existing is not null)
            return Accepted(BatchPath(existing.Id),
                new PartyExposureBatchAccepted(existing.Id.ToString(), existing.Status, existing.SubjectCount, BatchPath(existing.Id)));

        var work = subjects.Select(s =>
        {
            var (q, subjectErrors) = PartyExposureRules.Validate(
                null, null, s?.Identifiers, request.Include, fetchLiveBalances: false, requireRequestEnvelope: false);
            return new BatchSubjectWork(s?.SubjectRef, q, q is null ? string.Join(" ", subjectErrors) : null);
        }).ToList();

        var batch = PartyExposureBatch.Create(
            request.RequestId!, request.Purpose!, request.RequestedBy?.PortalUserId, request.RequestedBy?.Role,
            callbackUrl, work.Count);
        await _service.AddBatchAsync(batch, ct);
        await _queue.EnqueueAsync(new BatchWork(batch.Id, work), ct);

        _logger.LogInformation("Party exposure batch {BatchId} ({RequestId}) queued with {Count} subject(s).",
            batch.Id, batch.RequestId, work.Count);
        return Accepted(BatchPath(batch.Id),
            new PartyExposureBatchAccepted(batch.Id.ToString(), batch.Status, batch.SubjectCount, BatchPath(batch.Id)));
    }

    /// <summary>GET /api/v1/party-exposure/lookup-batch/{batchId} — progress, then results.</summary>
    [HttpGet("lookup-batch/{batchId:guid}")]
    public async Task<IActionResult> GetBatch(Guid batchId, CancellationToken ct)
    {
        var (_, refusal) = await AuthenticateAsync(ct);
        if (refusal is not null) return refusal;

        var batch = await _service.GetBatchAsync(batchId, ct);
        if (batch is null)
            return NotFound(new { error = "not_found", message = "No batch with that id." });

        var results = batch.Status == PartyExposureBatch.Completed && batch.ResultsJson is not null
            ? JsonSerializer.Deserialize<List<ExposureBatchResult>>(batch.ResultsJson, JsonSerializerOptions.Web)
            : null;
        return Ok(new PartyExposureBatchStatus(
            batch.Id.ToString(), batch.RequestId, batch.Status, batch.SubjectCount, batch.ProcessedCount,
            batch.CreatedAt, batch.CompletedAt, batch.Failure, results));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>API key, then signature. Returns the raw body that was signed.</summary>
    private async Task<(string Raw, IActionResult? Refusal)> AuthenticateAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_rhshf.ApiKey))
        {
            Request.Headers.TryGetValue("X-Api-Key", out var key);
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(_rhshf.ApiKey), Encoding.UTF8.GetBytes(key.ToString())))
            {
                _logger.LogWarning("Party exposure: invalid API key from {RemoteIp}.", HttpContext.Connection.RemoteIpAddress);
                return ("", Unauthorized(new { error = "unauthorized", message = "Invalid or missing API key." }));
            }
        }

        var raw = await ReadBodyAsync(ct);
        if (_settings.RequireSignature)
        {
            var problem = PortalRequestSignature.Verify(
                Request.Headers[PortalRequestSignature.TimestampHeader].ToString(),
                Request.Headers[PortalRequestSignature.SignatureHeader].ToString(),
                raw, _rhshf.CallbackSigningSecret, _settings.SignatureToleranceSeconds, DateTimeOffset.UtcNow);
            if (problem is not null)
            {
                _logger.LogWarning("Party exposure: signature refused from {RemoteIp}: {Reason}",
                    HttpContext.Connection.RemoteIpAddress, problem);
                return (raw, Unauthorized(new { error = "invalid_signature", message = problem }));
            }
        }
        return (raw, null);
    }

    private async Task<string> ReadBodyAsync(CancellationToken ct)
    {
        if (Request.ContentLength is 0 || HttpMethods.IsGet(Request.Method))
            return string.Empty;
        if (Request.Body.CanSeek) Request.Body.Position = 0;
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(ct);
        if (Request.Body.CanSeek) Request.Body.Position = 0;
        return body;
    }

    private static bool TryParse<T>(string raw, out T? value) where T : class
    {
        try
        {
            value = JsonSerializer.Deserialize<T>(raw, JsonSerializerOptions.Web);
            return value is not null;
        }
        catch (JsonException)
        {
            value = null;
            return false;
        }
    }

    /// <summary>Idempotency-Key is the requestId (contract §4). When both are given they must agree.</summary>
    private IActionResult? CheckIdempotencyKey(string? requestId)
    {
        var key = Request.Headers["Idempotency-Key"].ToString();
        return string.IsNullOrEmpty(key) || key == requestId
            ? null
            : BadRequest(new { error = "validation_failed", messages = new[] { "Idempotency-Key must equal requestId." } });
    }

    private bool IsAllowedCallback(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return false;
        var allowed = _settings.AllowedCallbackHosts.Count > 0
            ? _settings.AllowedCallbackHosts
            : Uri.TryCreate(_rhshf.PortalUrl, UriKind.Absolute, out var portal) ? [portal.Host] : [];
        return allowed.Any(h => string.Equals(h, uri.Host, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> TryAuditAsync(
        PartyExposureLookupRequest request, ExposureQuery query, string matchStatus, PartyExposureResponse? reply,
        Stopwatch timer, string? error, CancellationToken ct)
    {
        try
        {
            await _service.RecordLookupAsync(PartyExposureLookupLog.Record(
                request.RequestId!, request.Purpose!, request.RequestedBy?.PortalUserId, request.RequestedBy?.Role,
                request.SubjectRef, null,
                query.Bvn is null ? null : _service.HashIdentifier(query.Bvn),
                query.Nin is null ? null : _service.HashIdentifier(query.Nin),
                query.Nubans.Select(_service.HashIdentifier).ToList(),
                matchStatus, reply?.Relationships.Count ?? 0, reply?.Summary.DirectFacilities ?? 0,
                reply?.Summary.IndirectFacilities ?? 0, reply?.Truncated ?? false,
                reply is not null && !reply.IdentityConsistency.Consistent, (int)timer.ElapsedMilliseconds, error), ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Party exposure: could not record lookup {RequestId}.", request.RequestId);
            return false;
        }
    }
}
