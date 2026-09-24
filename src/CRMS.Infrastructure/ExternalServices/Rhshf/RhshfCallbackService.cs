using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.ExternalServices.Rhshf;

public class RhshfCallbackService : IRhshfCallbackService
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _httpClient;
    private readonly RhshfSettings _settings;
    private readonly ILogger<RhshfCallbackService> _logger;

    public RhshfCallbackService(HttpClient httpClient, IOptions<RhshfSettings> settings, ILogger<RhshfCallbackService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<RhshfCallbackSendResult> SendAsync(string callbackUrl, object payload, CancellationToken ct = default)
    {
        var rawBody = JsonSerializer.Serialize(payload, JsonOptions);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        using var request = new HttpRequestMessage(HttpMethod.Post, callbackUrl)
        {
            Content = new StringContent(rawBody, Encoding.UTF8, "application/json"),
        };

        // Two signatures, deliberately.
        //
        // A timestamp only prevents replay if it is inside the signed material — otherwise an
        // attacker replaying a captured callback simply rewrites the header. But the portal's
        // receiver verifies sha256(body) today, so changing what we sign would break it on the
        // next deploy.
        //
        // So: X-CRMS-Signature keeps its existing meaning, and X-CRMS-Signature-V2 covers
        // "{timestamp}.{body}". Once the portal verifies V2 and rejects stale timestamps, V1 can be
        // retired — at which point the replay window actually closes.
        request.Headers.Add("X-CRMS-Timestamp", timestamp);
        request.Headers.Add("X-CRMS-Signature", $"sha256={ComputeSignature(rawBody, _settings.CallbackSigningSecret)}");
        request.Headers.Add(
            "X-CRMS-Signature-V2",
            $"sha256={ComputeTimestampedSignature(timestamp, rawBody, _settings.CallbackSigningSecret)}");

        try
        {
            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("RH-SHF callback to {Url} returned {StatusCode}", callbackUrl, response.StatusCode);
            }
            return new RhshfCallbackSendResult(response.IsSuccessStatusCode, (int)response.StatusCode, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RH-SHF callback POST to {Url} failed", callbackUrl);
            return new RhshfCallbackSendResult(false, null, ex.Message);
        }
    }

    /// <summary>
    /// Signs "{timestamp}.{rawBody}" — the V2 scheme. Binding the timestamp into the signature is
    /// the whole point: a receiver can then reject anything outside a tolerance window and know the
    /// timestamp was not tampered with. The separator keeps the two fields unambiguous, so a body
    /// beginning with digits cannot be confused for part of the timestamp.
    /// </summary>
    public static string ComputeTimestampedSignature(string timestamp, string rawBody, string secret)
        => ComputeSignature($"{timestamp}.{rawBody}", secret);

    /// <summary>Public + static so it's directly verifiable in tests against a known secret/body,
    /// without needing an HTTP round-trip.</summary>
    public static string ComputeSignature(string rawBody, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
