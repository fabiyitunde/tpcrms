using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.ExternalServices.Rhshf;

/// <summary>No-op callback implementation used in local development / testing, mirroring
/// MockNampCallbackService — always "succeeds" so the background service doesn't spin on retries
/// against an unreachable local callbackUrl.</summary>
public class MockRhshfCallbackService : IRhshfCallbackService
{
    private readonly ILogger<MockRhshfCallbackService> _logger;

    public MockRhshfCallbackService(ILogger<MockRhshfCallbackService> logger)
    {
        _logger = logger;
    }

    public Task<RhshfCallbackSendResult> SendAsync(string callbackUrl, object payload, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[MOCK] RH-SHF callback -> {Url}: {Payload}",
            callbackUrl, JsonSerializer.Serialize(payload, RhshfCallbackService.JsonOptions));

        return Task.FromResult(new RhshfCallbackSendResult(true, 200, null));
    }
}
