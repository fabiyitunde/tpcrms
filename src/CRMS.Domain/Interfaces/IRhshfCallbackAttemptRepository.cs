using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Interfaces;

public interface IRhshfCallbackAttemptRepository
{
    /// <summary>Attempts ready to (re)send right now — not yet resolved and past their scheduled
    /// retry time. An exhausted attempt (all retries used up) naturally never appears here again
    /// since no further row gets a NextRetryAt.</summary>
    Task<IReadOnlyList<RhshfCallbackAttempt>> GetDueAsync(DateTime asOf, CancellationToken ct = default);

    Task AddAsync(RhshfCallbackAttempt attempt, CancellationToken ct = default);
}
