using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.ExternalServices.CoreBanking;

/// <summary>
/// DelegatingHandler that acquires an OAuth 2.0 bearer token via client_credentials
/// and attaches it to every outgoing CBS request. Caches the token and refreshes
/// when it is about to expire (with a 60-second safety margin).
/// Follows the same auth pattern as BOAFarmers LiveApiTestFixture.
/// </summary>
public class CoreBankingAuthHandler : DelegatingHandler
{
    private readonly CoreBankingSettings _settings;
    private readonly ILogger<CoreBankingAuthHandler> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public CoreBankingAuthHandler(
        IOptions<CoreBankingSettings> settings,
        ILogger<CoreBankingAuthHandler> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string?> GetTokenAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
            return _cachedToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
                return _cachedToken;

            _cachedToken = await AcquireTokenAsync(ct);
            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<string?> AcquireTokenAsync(CancellationToken ct)
    {
        var baseUrl = _settings.BaseUrl.TrimEnd('/');
        var endpoint = _settings.TokenEndpoint.TrimStart('/');
        var tokenUrl = $"{baseUrl}/{endpoint}";

        _logger.LogInformation("CBS: Acquiring OAuth2 token from {Url}", tokenUrl);

        using var tokenClient = new HttpClient { Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds) };

        // Try form-urlencoded (standard OAuth2)
        var formData = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _settings.ClientId,
            ["client_secret"] = _settings.ClientSecret
        };

        HttpResponseMessage? response = null;
        try
        {
            response = await tokenClient.PostAsync(tokenUrl, new FormUrlEncodedContent(formData), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CBS form-urlencoded token request failed");
        }

        // Fallback: JSON body
        if (response == null || !response.IsSuccessStatusCode)
        {
            try
            {
                var jsonPayload = JsonSerializer.Serialize(new
                {
                    client_id = _settings.ClientId,
                    client_secret = _settings.ClientSecret,
                    grant_type = "client_credentials"
                });
                var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");
                response = await tokenClient.PostAsync(tokenUrl, content, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CBS JSON token request failed");
            }
        }

        if (response == null || !response.IsSuccessStatusCode)
        {
            var errorBody = response != null ? await response.Content.ReadAsStringAsync(ct) : "no response";
            _logger.LogError("CBS: Failed to acquire token. Status={Status}, Body={Body}",
                response?.StatusCode, errorBody);
            return null;
        }

        try
        {
            var tokenResponse = await response.Content.ReadFromJsonAsync<CbsOAuth2TokenResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);

            var token = tokenResponse?.ResolvedToken;
            if (string.IsNullOrEmpty(token))
            {
                // Last resort: parse raw JSON looking for any token field
                var raw = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("CBS: Could not extract token from response: {Raw}", raw);
                return null;
            }

            var expiresIn = tokenResponse?.ExpiresIn > 0 ? tokenResponse.ExpiresIn : 3600;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60); // 60-second safety margin

            _logger.LogInformation("CBS: Token acquired, expires in {Seconds}s", expiresIn);
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CBS: Failed to parse token response");
            return null;
        }
    }
}
