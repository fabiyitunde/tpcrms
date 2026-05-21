using CRMS.Domain.Enums;

namespace CRMS.Infrastructure.ExternalServices.Namp;

public interface INampCallbackService
{
    /// <summary>
    /// Sends an outbound status callback to PAYS / Heifer Nigeria for a given NAMP application.
    /// </summary>
    Task SendCallbackAsync(string applicationReference, NampCallbackStatus status, string? note = null, CancellationToken ct = default);
}
