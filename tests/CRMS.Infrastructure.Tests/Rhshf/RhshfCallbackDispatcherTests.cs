using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.ExternalServices.Rhshf;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CRMS.Infrastructure.Tests.Rhshf;

/// <summary>
/// Drives RhshfCallbackDispatcher against a real in-memory CRMSDbContext + a scriptable fake
/// IRhshfCallbackService — proves the actual retry/backoff/give-up orchestration without needing to
/// run RhshfCallbackBackgroundService's infinite poll loop.
/// </summary>
public class RhshfCallbackDispatcherTests
{
    private const decimal TotalEopValue = 51_500_000.00m;

    private sealed class FakeCallbackService : IRhshfCallbackService
    {
        private readonly Queue<RhshfCallbackSendResult> _results;
        public List<object> SentPayloads { get; } = [];

        public FakeCallbackService(params RhshfCallbackSendResult[] results) => _results = new Queue<RhshfCallbackSendResult>(results);

        public Task<RhshfCallbackSendResult> SendAsync(string callbackUrl, object payload, CancellationToken ct = default)
        {
            SentPayloads.Add(payload);
            return Task.FromResult(_results.Count > 0 ? _results.Dequeue() : new RhshfCallbackSendResult(false, 500, "no more scripted results"));
        }
    }

    private static (CRMSDbContext Db, RhshfCreditProfile Profile) MakeHarness()
    {
        var options = new DbContextOptionsBuilder<CRMSDbContext>()
            .UseInMemoryDatabase("RhshfCallback_" + Guid.NewGuid())
            .Options;
        var db = new CRMSDbContext(options);

        var result = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: "01234567-0001",
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Nassarawa", totalEopValue: TotalEopValue, currency: "NGN", farmerCount: 1200,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
            eopLines: null, resolvedBranchId: null, resolvedOfficeId: null);
        var profile = result.Value;
        db.RhshfCreditProfiles.Add(profile);
        db.SaveChanges();

        return (db, profile);
    }

    private static RhshfCallbackDispatcher MakeDispatcher(CRMSDbContext db, FakeCallbackService fakeCallback)
        => new(db, new RhshfCreditProfileRepository(db), new RhshfCallbackAttemptRepository(db), fakeCallback, db,
            new FakeRhshfPublicUrlProvider(), NullLogger<RhshfCallbackDispatcher>.Instance);

    [Fact]
    public async Task ProcessDueAttempts_Success_ResolvesAttempt_NoRetryScheduled()
    {
        var (db, profile) = MakeHarness();
        var attempt = RhshfCallbackAttempt.CreateFirst(profile.Id, RhshfCallbackEventType.Decided, DateTime.UtcNow);
        db.RhshfCallbackAttempts.Add(attempt);
        await db.SaveChangesAsync();
        var fakeCallback = new FakeCallbackService(new RhshfCallbackSendResult(true, 200, null));
        var dispatcher = MakeDispatcher(db, fakeCallback);

        var processed = await dispatcher.ProcessDueAttemptsAsync();

        Assert.Equal(1, processed);
        var stored = await db.RhshfCallbackAttempts.ToListAsync();
        Assert.Single(stored);
        Assert.True(stored[0].Succeeded);
        Assert.Null(stored[0].NextRetryAt);
    }

    [Fact]
    public async Task ProcessDueAttempts_NonSuccess_SchedulesRetry_SameEventId_IncrementedAttemptNumber()
    {
        var (db, profile) = MakeHarness();
        var attempt = RhshfCallbackAttempt.CreateFirst(profile.Id, RhshfCallbackEventType.Decided, DateTime.UtcNow);
        db.RhshfCallbackAttempts.Add(attempt);
        await db.SaveChangesAsync();
        var fakeCallback = new FakeCallbackService(new RhshfCallbackSendResult(false, 503, null));
        var dispatcher = MakeDispatcher(db, fakeCallback);

        await dispatcher.ProcessDueAttemptsAsync();

        var stored = await db.RhshfCallbackAttempts.OrderBy(x => x.AttemptNumber).ToListAsync();
        Assert.Equal(2, stored.Count);
        Assert.False(stored[0].Succeeded);
        Assert.Null(stored[0].NextRetryAt);
        Assert.Equal(2, stored[1].AttemptNumber);
        Assert.Equal(attempt.EventId, stored[1].EventId);
        Assert.NotNull(stored[1].NextRetryAt);
        Assert.True(stored[1].NextRetryAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task ProcessDueAttempts_FailsThenSucceeds_StopsRetryingOnSuccess()
    {
        var (db, profile) = MakeHarness();
        var attempt = RhshfCallbackAttempt.CreateFirst(profile.Id, RhshfCallbackEventType.Decided, DateTime.UtcNow);
        db.RhshfCallbackAttempts.Add(attempt);
        await db.SaveChangesAsync();

        // First poll: fails, schedules attempt 2 (due immediately for test purposes we force it).
        var dispatcher1 = MakeDispatcher(db, new FakeCallbackService(new RhshfCallbackSendResult(false, 500, null)));
        await dispatcher1.ProcessDueAttemptsAsync();

        var scheduled = await db.RhshfCallbackAttempts.SingleAsync(x => x.AttemptNumber == 2);
        // Force it due now instead of waiting for the real backoff delay.
        typeof(RhshfCallbackAttempt).GetProperty(nameof(RhshfCallbackAttempt.NextRetryAt))!.SetValue(scheduled, DateTime.UtcNow);
        await db.SaveChangesAsync();

        // Second poll: succeeds — must not schedule a third attempt.
        var dispatcher2 = MakeDispatcher(db, new FakeCallbackService(new RhshfCallbackSendResult(true, 200, null)));
        var processedSecondPoll = await dispatcher2.ProcessDueAttemptsAsync();

        Assert.Equal(1, processedSecondPoll);
        var all = await db.RhshfCallbackAttempts.ToListAsync();
        Assert.Equal(2, all.Count);
        Assert.True(all.Single(x => x.AttemptNumber == 2).Succeeded);
        Assert.DoesNotContain(all, x => x.AttemptNumber == 3);
    }

    [Fact]
    public async Task ProcessDueAttempts_ExhaustsAllAttempts_NoFurtherRetryScheduled()
    {
        var (db, profile) = MakeHarness();
        var attempt = RhshfCallbackAttempt.CreateFirst(profile.Id, RhshfCallbackEventType.Decided, DateTime.UtcNow);
        // Force this straight to the last allowed attempt number so one more failure exhausts it.
        typeof(RhshfCallbackAttempt).GetProperty(nameof(RhshfCallbackAttempt.AttemptNumber))!
            .SetValue(attempt, RhshfCallbackDispatcher.MaxAttempts);
        db.RhshfCallbackAttempts.Add(attempt);
        await db.SaveChangesAsync();
        var fakeCallback = new FakeCallbackService(new RhshfCallbackSendResult(false, 500, "still down"));
        var dispatcher = MakeDispatcher(db, fakeCallback);

        await dispatcher.ProcessDueAttemptsAsync();

        var stored = await db.RhshfCallbackAttempts.ToListAsync();
        Assert.Single(stored); // no new row — exhausted, not retried further
        Assert.False(stored[0].Succeeded);
        Assert.Null(stored[0].NextRetryAt);
    }

    [Fact]
    public async Task ProcessDueAttempts_NotYetDue_IsIgnored()
    {
        var (db, profile) = MakeHarness();
        var attempt = RhshfCallbackAttempt.CreateFirst(profile.Id, RhshfCallbackEventType.Decided, DateTime.UtcNow);
        typeof(RhshfCallbackAttempt).GetProperty(nameof(RhshfCallbackAttempt.NextRetryAt))!.SetValue(attempt, DateTime.UtcNow.AddMinutes(10));
        db.RhshfCallbackAttempts.Add(attempt);
        await db.SaveChangesAsync();
        var fakeCallback = new FakeCallbackService(new RhshfCallbackSendResult(true, 200, null));
        var dispatcher = MakeDispatcher(db, fakeCallback);

        var processed = await dispatcher.ProcessDueAttemptsAsync();

        Assert.Equal(0, processed);
        Assert.Empty(fakeCallback.SentPayloads);
    }
}
