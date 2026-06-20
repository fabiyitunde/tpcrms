using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.ExternalServices;

/// <summary>
/// Logs every outbound third-party HTTP call (method, URL, request + response bodies, status code,
/// elapsed time) at Information level so the calls are visible in production logs. Designed to be
/// attached to each third-party typed HttpClient via <c>.AddHttpMessageHandler&lt;ThirdPartyApiLoggingHandler&gt;()</c>.
///
/// Safety:
///   • Sensitive values (passwords, secrets, tokens, API keys) are redacted from logged bodies.
///   • HTTP headers are never logged (so Authorization / X-Api-Key never leak).
///   • Bodies are truncated to <c>ThirdPartyApi:MaxBodyChars</c> (default 32000) to keep log
///     volume bounded — set it to <c>0</c> for no truncation when you need the full payload to
///     refine DTOs.
///   • Disable entirely with config <c>ThirdPartyApi:LogPayloads = false</c>
///     (or env var <c>ThirdPartyApi__LogPayloads=false</c>). Default: enabled.
///
/// Note: these bodies can contain PII (BVN/NIN/personal data), which is the point — they're for
/// diagnosing third-party payload issues. Turn the flag off if that's not acceptable in your env.
/// </summary>
public sealed partial class ThirdPartyApiLoggingHandler : DelegatingHandler
{
    private readonly ILogger<ThirdPartyApiLoggingHandler> _logger;
    private readonly bool _enabled;
    private readonly int _maxBodyChars;

    public ThirdPartyApiLoggingHandler(IConfiguration configuration, ILogger<ThirdPartyApiLoggingHandler> logger)
    {
        _logger = logger;
        _enabled = configuration.GetValue("ThirdPartyApi:LogPayloads", true);
        // 0 (or negative) = no truncation — log the full payload.
        _maxBodyChars = configuration.GetValue("ThirdPartyApi:MaxBodyChars", 32000);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (!_enabled)
            return await base.SendAsync(request, ct);

        var sw = Stopwatch.StartNew();
        var requestBody = await SafeReadAsync(request.Content, ct);
        _logger.LogInformation("[3rd-party →] {Method} {Uri} body={Body}",
            request.Method, request.RequestUri, BodyForLog(requestBody));

        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "[3rd-party ✗] {Method} {Uri} failed after {Ms}ms",
                request.Method, request.RequestUri, sw.ElapsedMilliseconds);
            throw;
        }
        sw.Stop();

        var responseBody = await SafeReadAsync(response.Content, ct);
        _logger.Log(
            response.IsSuccessStatusCode ? LogLevel.Information : LogLevel.Warning,
            "[3rd-party ←] {Method} {Uri} → {Status} in {Ms}ms body={Body}",
            request.Method, request.RequestUri, (int)response.StatusCode, sw.ElapsedMilliseconds, BodyForLog(responseBody));

        return response;
    }

    // ReadAsStringAsync buffers the content, so the downstream handler / caller can still read it.
    private static async Task<string?> SafeReadAsync(HttpContent? content, CancellationToken ct)
    {
        if (content is null) return null;
        try { return await content.ReadAsStringAsync(ct); }
        catch { return "(unreadable)"; }
    }

    private string BodyForLog(string? body)
    {
        if (string.IsNullOrEmpty(body)) return "(none)";
        var redacted = SecretRegex().Replace(body, m => m.Groups[1].Value + "\"***\"");
        if (_maxBodyChars <= 0 || redacted.Length <= _maxBodyChars)
            return redacted;
        return string.Concat(redacted.AsSpan(0, _maxBodyChars), $"…(truncated {redacted.Length} chars)");
    }

    // Matches  "password": "xyz"  /  "access_token":"xyz"  /  "apiKey" : "xyz"  (case-insensitive key)
    // and replaces only the value with "***".
    [GeneratedRegex(
        "(\"(?:password|secret|client_secret|clientSecret|access_token|accessToken|refresh_token|refreshToken|token|api[_-]?key|apikey|secret[_-]?key|access[_-]?key)\"\\s*:\\s*)\"[^\"]*\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretRegex();
}
