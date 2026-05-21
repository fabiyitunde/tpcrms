using CRMS.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.ExternalServices.Namp;

/// <summary>
/// No-op callback implementation used in local development / testing.
/// </summary>
public class MockNampCallbackService : INampCallbackService
{
    private readonly ILogger<MockNampCallbackService> _logger;

    public MockNampCallbackService(ILogger<MockNampCallbackService> logger)
    {
        _logger = logger;
    }

    public Task SendCallbackAsync(
        string applicationReference,
        NampCallbackStatus status,
        string? note = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[MOCK] NAMP callback: Reference={Reference}, Status={Status}, Note={Note}",
            applicationReference, status, note);

        return Task.CompletedTask;
    }
}
