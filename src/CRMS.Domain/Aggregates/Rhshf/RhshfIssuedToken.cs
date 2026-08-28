using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// Tracks a single issued §4.2 reference token for single-use/replay-prevention purposes. The
/// token itself is a stateless signed JWT — this record exists only to enforce that a token can
/// authenticate the profiling form's first page load exactly once (design doc §6 #5).
/// </summary>
public class RhshfIssuedToken : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }
    public string Jti { get; private set; } = string.Empty;
    public DateTime IssuedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }

    protected RhshfIssuedToken() { }

    public RhshfIssuedToken(Guid rhshfCreditProfileId, string jti, DateTime issuedAt, DateTime expiresAt)
    {
        RhshfCreditProfileId = rhshfCreditProfileId;
        Jti = jti;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    public Result Consume()
    {
        if (ConsumedAt is not null)
            return Result.Failure("Token has already been used.");

        ConsumedAt = DateTime.UtcNow;
        return Result.Success();
    }
}
