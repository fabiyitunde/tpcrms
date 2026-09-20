using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// One outbound-webhook delivery attempt (design doc §4.4, Phase 10) — its own aggregate root, own
/// table (like RhshfCommitteeReview/RhshfOffer/RhshfLegalClearance), not a navigable child of
/// RhshfCreditProfile despite the plan's "child entity" phrasing — a background service polls due
/// attempts across ALL profiles at once, which needs a flat repository query, not a
/// load-one-profile-then-look-at-its-collection access pattern.
///
/// One row per attempt, immutable once resolved (RecordResult). EventId is stable across every
/// retry of the same logical event (the portal de-dupes on it); a failed-but-retryable attempt gets
/// a brand new row for the next attempt rather than mutating this one, so history is fully
/// preserved for the exhausted-retry manual-reconciliation case the brief asks for.
/// </summary>
public class RhshfCallbackAttempt : AggregateRoot
{
    public Guid RhshfCreditProfileId { get; private set; }
    public string EventId { get; private set; } = string.Empty;
    public RhshfCallbackEventType EventType { get; private set; }

    /// <summary>When the underlying domain event actually fired — stable across retries, used for
    /// the payload's decidedAt/occurredAt so it doesn't drift to "now" on a later retry.</summary>
    public DateTime EventOccurredAt { get; private set; }

    public int AttemptNumber { get; private set; }
    public DateTime? SentAt { get; private set; }
    public int? ResponseStatusCode { get; private set; }
    public bool Succeeded { get; private set; }

    /// <summary>Null once resolved (succeeded, or exhausted all attempts) — the background service's
    /// due-query is simply "not succeeded and NextRetryAt has passed", so an exhausted attempt
    /// naturally drops out without a separate status flag.</summary>
    public DateTime? NextRetryAt { get; private set; }

    protected RhshfCallbackAttempt() { }

    public static RhshfCallbackAttempt CreateFirst(Guid rhshfCreditProfileId, RhshfCallbackEventType eventType, DateTime eventOccurredAt)
        => new()
        {
            RhshfCreditProfileId = rhshfCreditProfileId,
            EventId = $"evt_{Guid.NewGuid():N}",
            EventType = eventType,
            EventOccurredAt = eventOccurredAt,
            AttemptNumber = 1,
            NextRetryAt = DateTime.UtcNow,
        };

    /// <summary>Schedules the next attempt for the same logical event after this one failed.</summary>
    public RhshfCallbackAttempt CreateRetry(DateTime nextRetryAt)
        => new()
        {
            RhshfCreditProfileId = RhshfCreditProfileId,
            EventId = EventId,
            EventType = EventType,
            EventOccurredAt = EventOccurredAt,
            AttemptNumber = AttemptNumber + 1,
            NextRetryAt = nextRetryAt,
        };

    /// <summary>Records the outcome of actually sending this attempt. Clears NextRetryAt regardless
    /// of outcome — on success there is nothing left to retry; on failure, the caller decides
    /// separately (via CreateRetry) whether a new row is warranted, so this row itself is done
    /// either way.</summary>
    public void RecordResult(bool succeeded, int? responseStatusCode)
    {
        SentAt = DateTime.UtcNow;
        Succeeded = succeeded;
        ResponseStatusCode = responseStatusCode;
        NextRetryAt = null;
    }
}
