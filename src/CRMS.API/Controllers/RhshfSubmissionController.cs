using System.Text.Json;
using CRMS.Application.Rhshf.Commands;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Application.Rhshf.Queries;
using CRMS.Infrastructure.ExternalServices.Rhshf;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CRMS.API.Controllers;

/// <summary>
/// CRMS side of the RH-SHF (Renewed Hope Dry Season) input-financing integration —
/// see docs/rhshf resources/. Auth: X-Api-Key header (constant-time comparison), same pattern
/// as NampWebhookController, but its own independent implementation — RH-SHF is a separate loan
/// track from NAMP.
/// </summary>
[ApiController]
[Route("v1/credit-profiles")]
public class RhshfSubmissionController : ControllerBase
{
    private readonly SubmitConsolidatedEopHandler _submitHandler;
    private readonly RefreshRhshfTokenHandler _refreshHandler;
    private readonly GetRhshfCaseStatusHandler _statusHandler;
    private readonly RhshfSettings _settings;
    private readonly ILogger<RhshfSubmissionController> _logger;

    public RhshfSubmissionController(
        SubmitConsolidatedEopHandler submitHandler,
        RefreshRhshfTokenHandler refreshHandler,
        GetRhshfCaseStatusHandler statusHandler,
        IOptions<RhshfSettings> settings,
        ILogger<RhshfSubmissionController> logger)
    {
        _submitHandler = submitHandler;
        _refreshHandler = refreshHandler;
        _statusHandler = statusHandler;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>POST /v1/credit-profiles — §4.1 of the integration brief.</summary>
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitConsolidatedEopRequest? request, CancellationToken ct)
    {
        if (!IsAuthorized())
        {
            _logger.LogWarning("RH-SHF submit: invalid API key from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { error = "unauthorized", message = "Invalid or missing API key." });
        }

        if (request is null)
            return BadRequest(new { error = "invalid_payload", message = "Request body is required." });

        if (request.SubmissionId == Guid.Empty)
            return BadRequest(new { error = "missing_field", message = "submissionId is required." });
        if (request.Programme is null || string.IsNullOrWhiteSpace(request.Programme.Code))
            return BadRequest(new { error = "missing_field", message = "programme.code is required." });
        if (request.Session is null || string.IsNullOrWhiteSpace(request.Session.Code))
            return BadRequest(new { error = "missing_field", message = "session.code is required." });
        if (request.Fac is null)
            return BadRequest(new { error = "missing_field", message = "fac is required." });
        if (string.IsNullOrWhiteSpace(request.CallbackUrl))
            return BadRequest(new { error = "missing_field", message = "callbackUrl is required." });

        // The ACTUAL request body, not a re-serialisation of the model-bound DTO.
        //
        // This previously did JsonSerializer.Serialize(request), which round-tripped through the
        // binder and silently discarded every property not on SubmitConsolidatedEopRequest. That
        // made RawSubmissionPayload useless for its one purpose: if the portal ever sent a field the
        // contract didn't yet know about (agronomic data, directors, financials), it was gone
        // beyond recovery. Reading the buffered body keeps the original verbatim.
        var rawPayload = await ReadRawBodyAsync(ct) ?? JsonSerializer.Serialize(request);
        var result = await _submitHandler.Handle(new SubmitConsolidatedEopCommand(request, rawPayload), ct);

        if (!result.IsSuccess)
            return BadRequest(new { error = "validation_failed", message = result.Error });

        return StatusCode(StatusCodes.Status201Created, result.Data);
    }

    /// <summary>POST /v1/credit-profiles/{reference}/token — §4.6 of the integration brief.</summary>
    [HttpPost("{reference}/token")]
    public async Task<IActionResult> Refresh(string reference, CancellationToken ct)
    {
        if (!IsAuthorized())
        {
            _logger.LogWarning("RH-SHF token refresh: invalid API key from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { error = "unauthorized", message = "Invalid or missing API key." });
        }

        var result = await _refreshHandler.Handle(new RefreshRhshfTokenCommand(reference), ct);
        if (!result.IsSuccess)
            return NotFound(new { error = "refresh_failed", message = result.Error });

        return Ok(result.Data);
    }

    /// <summary>GET /v1/credit-profiles/{reference}/status — §4.5 of the integration brief.</summary>
    [HttpGet("{reference}/status")]
    public async Task<IActionResult> Status(string reference, CancellationToken ct)
    {
        if (!IsAuthorized())
        {
            _logger.LogWarning("RH-SHF status: invalid API key from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { error = "unauthorized", message = "Invalid or missing API key." });
        }

        var result = await _statusHandler.Handle(new GetRhshfCaseStatusQuery(reference), ct);
        if (!result.IsSuccess)
            return NotFound(new { error = "not_found", message = result.Error });

        return Ok(result.Data);
    }

    /// <summary>
    /// Reads the raw request body. Requires EnableBuffering (set below before model binding has
    /// consumed the stream) — returns null rather than throwing if the body can't be re-read, so a
    /// traceability nicety can never fail a submission.
    /// </summary>
    private async Task<string?> ReadRawBodyAsync(CancellationToken ct)
    {
        try
        {
            if (!Request.Body.CanSeek)
                return null;

            Request.Body.Position = 0;
            using var reader = new StreamReader(Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync(ct);
            Request.Body.Position = 0;
            return string.IsNullOrWhiteSpace(body) ? null : body;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RH-SHF submit: could not read the raw request body for traceability.");
            return null;
        }
    }

    private bool IsAuthorized()
    {
        if (string.IsNullOrEmpty(_settings.ApiKey))
            return true; // no key configured — local/dev only, matches NampWebhookController's fallback

        Request.Headers.TryGetValue("X-Api-Key", out var providedKey);
        return CryptographicEquals(_settings.ApiKey, providedKey.ToString());
    }

    private static bool CryptographicEquals(string a, string b)
    {
        var aBytes = System.Text.Encoding.UTF8.GetBytes(a);
        var bBytes = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
