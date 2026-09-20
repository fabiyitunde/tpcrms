using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

public class RhshfCallbackAttemptTests
{
    [Fact]
    public void CreateFirst_SetsAttemptNumberOne_ReadyImmediately()
    {
        var profileId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow.AddMinutes(-1);

        var attempt = RhshfCallbackAttempt.CreateFirst(profileId, RhshfCallbackEventType.Decided, occurredAt);

        Assert.Equal(1, attempt.AttemptNumber);
        Assert.Equal(occurredAt, attempt.EventOccurredAt);
        Assert.False(attempt.Succeeded);
        Assert.NotNull(attempt.NextRetryAt);
        Assert.StartsWith("evt_", attempt.EventId);
    }

    [Fact]
    public void CreateRetry_PreservesEventId_IncrementsAttemptNumber()
    {
        var first = RhshfCallbackAttempt.CreateFirst(Guid.NewGuid(), RhshfCallbackEventType.OfferReady, DateTime.UtcNow);
        var nextRetryAt = DateTime.UtcNow.AddMinutes(5);

        var retry = first.CreateRetry(nextRetryAt);

        Assert.Equal(first.EventId, retry.EventId);
        Assert.Equal(first.EventType, retry.EventType);
        Assert.Equal(first.EventOccurredAt, retry.EventOccurredAt);
        Assert.Equal(2, retry.AttemptNumber);
        Assert.Equal(nextRetryAt, retry.NextRetryAt);
    }

    [Fact]
    public void RecordResult_Success_ClearsNextRetryAt()
    {
        var attempt = RhshfCallbackAttempt.CreateFirst(Guid.NewGuid(), RhshfCallbackEventType.Decided, DateTime.UtcNow);

        attempt.RecordResult(succeeded: true, responseStatusCode: 200);

        Assert.True(attempt.Succeeded);
        Assert.Equal(200, attempt.ResponseStatusCode);
        Assert.Null(attempt.NextRetryAt);
        Assert.NotNull(attempt.SentAt);
    }

    [Fact]
    public void RecordResult_Failure_ClearsNextRetryAt_LeavesRowResolved()
    {
        var attempt = RhshfCallbackAttempt.CreateFirst(Guid.NewGuid(), RhshfCallbackEventType.Decided, DateTime.UtcNow);

        attempt.RecordResult(succeeded: false, responseStatusCode: 503);

        Assert.False(attempt.Succeeded);
        Assert.Equal(503, attempt.ResponseStatusCode);
        Assert.Null(attempt.NextRetryAt);
    }
}
