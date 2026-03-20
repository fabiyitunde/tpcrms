using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.ExternalServices.FineractDirect;

/// <summary>
/// DelegatingHandler that authenticates against Fineract using Basic Auth.
/// Calls POST /authentication to get the base64EncodedAuthenticationKey,
/// then attaches it as Authorization: Basic header + tenant header on every request.
/// Matches the pattern used in TPAPIProvider.authenticate().
/// </summary>
public class FineractDirectAuthHandler : DelegatingHandler
{
    private readonly FineractDirectSettings _settings;
    private readonly ILogger<FineractDirectAuthHandler> _logger;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    private string? _cachedAuthKey;
    private DateTime _keyExpiry = DateTime.MinValue;

    public FineractDirectAuthHandler(
        IOptions<FineractDirectSettings> settings,
        ILogger<FineractDirectAuthHandler> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authKey = await GetAuthKeyAsync(cancellationToken);
        if (!string.IsNullOrEmpty(authKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authKey);
        }
        request.Headers.TryAddWithoutValidation("fineract-platform-tenantid", _settings.TenantId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string?> GetAuthKeyAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_cachedAuthKey) && DateTime.UtcNow < _keyExpiry)
            return _cachedAuthKey;

        await _authLock.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrEmpty(_cachedAuthKey) && DateTime.UtcNow < _keyExpiry)
                return _cachedAuthKey;

            _cachedAuthKey = await AuthenticateAsync(ct);
            return _cachedAuthKey;
        }
        finally
        {
            _authLock.Release();
        }
    }

    private async Task<string?> AuthenticateAsync(CancellationToken ct)
    {
        var baseUrl = _settings.BaseUrl.TrimEnd('/');
        var authUrl = $"{baseUrl}/authentication";

        _logger.LogInformation("Fineract: Authenticating at {Url}", authUrl);

        // Fineract uses self-signed certs — handler configured in DI
        using var authClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                errors == System.Net.Security.SslPolicyErrors.None ||
                errors == System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch
        })
        {
            Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds)
        };

        var body = new { username = _settings.Username, password = _settings.Password };
        var request = new HttpRequestMessage(HttpMethod.Post, authUrl)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("fineract-platform-tenantid", _settings.TenantId);

        try
        {
            var response = await authClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Fineract: Authentication failed ({Status}): {Body}", response.StatusCode, errorBody);
                return null;
            }

            var authResponse = await response.Content.ReadFromJsonAsync<FineractAuthResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);

            var key = authResponse?.Base64EncodedAuthenticationKey;
            if (string.IsNullOrEmpty(key))
            {
                _logger.LogError("Fineract: Authentication response missing base64EncodedAuthenticationKey");
                return null;
            }

            // Cache for 8 hours (Fineract basic auth keys don't expire, but refresh periodically)
            _keyExpiry = DateTime.UtcNow.AddHours(8);
            _logger.LogInformation("Fineract: Authenticated successfully, key cached for 8 hours");
            return key;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fineract: Authentication error");
            return null;
        }
    }
}
