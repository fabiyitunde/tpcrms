namespace CRMS.Infrastructure.ExternalServices.Rhshf;

/// <summary>
/// Sends one HMAC-signed outcome-webhook HTTP attempt (design doc §4.4, Phase 10). Deliberately
/// does not know about retries, attempt history, or scheduling — that's RhshfCallbackBackgroundService's
/// job, driven by RhshfCallbackAttempt. Independent of INampCallbackService.
/// </summary>
public interface IRhshfCallbackService
{
    Task<RhshfCallbackSendResult> SendAsync(string callbackUrl, object payload, CancellationToken ct = default);
}

public record RhshfCallbackSendResult(bool Succeeded, int? StatusCode, string? ErrorMessage);
