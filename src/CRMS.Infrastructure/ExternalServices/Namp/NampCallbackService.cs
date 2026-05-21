using System.Text;
using System.Text.Json;
using CRMS.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.ExternalServices.Namp;

public class NampCallbackService : INampCallbackService
{
    private readonly HttpClient _httpClient;
    private readonly NampSettings _settings;
    private readonly ILogger<NampCallbackService> _logger;

    public NampCallbackService(
        HttpClient httpClient,
        IOptions<NampSettings> settings,
        ILogger<NampCallbackService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendCallbackAsync(
        string applicationReference,
        NampCallbackStatus status,
        string? note = null,
        CancellationToken ct = default)
    {
        var payload = new
        {
            applicationReference,
            status = status.ToString(),
            note,
            timestamp = DateTime.UtcNow,
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        try
        {
            var response = await _httpClient.PostAsync(_settings.CallbackUrl, content, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "NAMP callback for {Reference} returned {StatusCode}",
                    applicationReference,
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send NAMP callback for {Reference} with status {Status}",
                applicationReference,
                status);
        }
    }
}
