using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories.Rhshf;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRMS.Infrastructure.Tests.Rhshf;

/// <summary>
/// A staff session queues a re-send; the background sender, in its own context, sends it. The staff
/// session must then see the sender's result, not the queued copy it created (CRMSQA#2).
/// </summary>
public class RhshfCallbackAttemptRepositoryTests
{
    private readonly string _database = "rhshf-attempts-" + Guid.NewGuid();
    private static readonly Guid Profile = Guid.NewGuid();

    private CRMSDbContext Session() =>
        new(new DbContextOptionsBuilder<CRMSDbContext>().UseInMemoryDatabase(_database).Options);

    [Fact]
    public async Task AStaffSessionSeesTheSendersResult_ForAReSendItQueued()
    {
        await using var staff = Session();
        var staffRepo = new RhshfCallbackAttemptRepository(staff);
        var resend = RhshfCallbackAttempt.CreateFirst(Profile, RhshfCallbackEventType.Decided, DateTime.UtcNow);
        await staffRepo.AddAsync(resend);
        await staff.SaveChangesAsync();

        await using (var sender = Session())
        {
            var row = await sender.RhshfCallbackAttempts.SingleAsync();
            row.RecordResult(succeeded: true, responseStatusCode: 200);
            await sender.SaveChangesAsync();
        }

        var seen = Assert.Single(await staffRepo.GetByProfileAsync(Profile));
        Assert.True(seen.Succeeded);
        Assert.Equal(200, seen.ResponseStatusCode);
        Assert.Null(seen.NextRetryAt);
    }
}
