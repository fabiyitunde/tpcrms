using System.Security.Cryptography;
using System.Text;

namespace CRMS.Infrastructure.ExternalServices.Rhshf;

/// <summary>
/// Verifies a request the portal signed — the same scheme the CRMS uses on its callbacks to the
/// portal (X-CRMS-Signature-V2), applied in the other direction:
/// <code>
/// X-Portal-Timestamp: unix seconds
/// X-Portal-Signature: sha256=hex(HMAC-SHA256(secret, "{timestamp}.{raw body}"))
/// </code>
/// The secret is the shared RH-SHF callback signing secret. The timestamp is inside the signed
/// material, so a captured request cannot be replayed outside the tolerance window by rewriting it.
/// A GET signs an empty body.
/// </summary>
public static class PortalRequestSignature
{
    public const string TimestampHeader = "X-Portal-Timestamp";
    public const string SignatureHeader = "X-Portal-Signature";

    /// <returns>Null when valid; otherwise the reason, which never includes the body.</returns>
    public static string? Verify(
        string? timestamp, string? signature, string rawBody, string secret, int toleranceSeconds, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return "The CRMS has no signing secret configured.";
        if (string.IsNullOrWhiteSpace(timestamp) || !long.TryParse(timestamp, out var seconds))
            return $"{TimestampHeader} is missing or not a number.";
        if (Math.Abs(now.ToUnixTimeSeconds() - seconds) > toleranceSeconds)
            return $"{TimestampHeader} is outside the {toleranceSeconds}-second window.";
        if (string.IsNullOrWhiteSpace(signature) || !signature.StartsWith("sha256=", StringComparison.Ordinal))
            return $"{SignatureHeader} is missing or not in the form sha256=<hex>.";

        var expected = RhshfCallbackService.ComputeTimestampedSignature(timestamp, rawBody, secret);
        var given = signature["sha256=".Length..].Trim().ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given))
            ? null
            : "The signature does not match the request.";
    }
}
