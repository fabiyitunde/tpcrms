using System.Text;
using CRMS.Application.CustomerCheck;
using CRMS.Infrastructure.ExternalServices.Rhshf;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.ExternalServices.CustomerCheck;

/// <summary>
/// Posts one customer-check callback (docs/Customer_Check.md, "Callback"), signed with the
/// consumer's own secret in the CRMS's V2 scheme: HMAC-SHA256 over "{timestamp}.{body}". Only the
/// timestamp-bound signature is sent; there is no V1 to retire here. Retries and scheduling are the
/// dispatcher's business.
/// </summary>
public class CustomerCheckCallbackSender : ICustomerCheckCallbackSender
{
    private readonly HttpClient _http;
    private readonly ILogger<CustomerCheckCallbackSender> _logger;

    public CustomerCheckCallbackSender(HttpClient http, ILogger<CustomerCheckCallbackSender> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<CallbackSendResult> SendAsync(string url, string secret, string eventId, string eventType, string body, CancellationToken ct = default)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-CRMS-Event-Id", eventId);
        request.Headers.Add("X-CRMS-Event-Type", eventType);
        request.Headers.Add("X-CRMS-Timestamp", timestamp);
        request.Headers.Add("X-CRMS-Signature-V2", $"sha256={RhshfCallbackService.ComputeTimestampedSignature(timestamp, body, secret)}");

        var host = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "?";
        try
        {
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            var status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Customer check callback {EventId} to {Host} answered {Status}.", eventId, host, status);
            return new CallbackSendResult(response.IsSuccessStatusCode, status, response.IsSuccessStatusCode ? null : $"HTTP {status}");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Customer check callback {EventId} to {Host} timed out.", eventId, host);
            return new CallbackSendResult(false, null, "No answer in time.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("Customer check callback {EventId} to {Host} failed: {Error}", eventId, host, ex.Message);
            return new CallbackSendResult(false, null, ex.Message);
        }
    }
}
