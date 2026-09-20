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
        var signature = ComputeSignature(rawBody, _settings.CallbackSigningSecret);

        using var request = new HttpRequestMessage(HttpMethod.Post, callbackUrl)
        {
            Content = new StringContent(rawBody, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-CRMS-Signature", $"sha256={signature}");

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

    /// <summary>Public + static so it's directly verifiable in tests against a known secret/body,
    /// without needing an HTTP round-trip.</summary>
    public static string ComputeSignature(string rawBody, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
