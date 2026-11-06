using CRMS.Application.CustomerCheck;
using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// A consumer row is written by sessions that do not share a DbContext: an administrator's
/// intranet session, the setup-link page, and the API's job worker. Each context below stands for
/// one of them. What must never happen is one of them saving its older copy of the row over a key
/// that another has just replaced.
/// </summary>
public class ApiConsumerRepositoryTests
{
    private readonly string _database = "api-consumers-" + Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();

    private CRMSDbContext Session() =>
        new(new DbContextOptionsBuilder<CRMSDbContext>().UseInMemoryDatabase(_database).Options);

    private async Task<Guid> SeedAsync()
    {
        await using var db = Session();
        var consumer = ApiConsumer.Create("AgriPay", ApiKeys.Hash("old-key"), "crms_oldkey", CustomerCheckSections.All, 30, false, 30, null, Admin).Value;
        db.ApiConsumers.Add(consumer);
        await db.SaveChangesAsync();
        return consumer.Id;
    }

    /// <summary>Another session replaces the key, as opening a setup link does.</summary>
    private async Task ReplaceKeyElsewhereAsync(Guid id)
    {
        await using var other = Session();
        var repo = new ApiConsumerRepository(other);
        var consumer = (await repo.GetByIdAsync(id))!;
        consumer.RotateKey(ApiKeys.Hash("new-key"), "crms_newkey", Admin);
        repo.Update(consumer);
        await other.SaveChangesAsync();
    }

    private async Task<ApiConsumer> StoredAsync(Guid id)
    {
        await using var db = Session();
        return await db.ApiConsumers.AsNoTracking().SingleAsync(c => c.Id == id);
    }

    [Fact]
    public async Task A_check_that_finishes_after_the_key_was_replaced_does_not_put_the_old_key_back()
    {
        var id = await SeedAsync();

        // The worker loads the consumer when the check starts...
        await using var worker = Session();
        var repo = new ApiConsumerRepository(worker);
        var consumer = (await repo.GetByIdAsync(id))!;

        // ...the key is replaced while the check runs...
        await ReplaceKeyElsewhereAsync(id);

        // ...and the worker stamps the consumer when the check ends.
        consumer.Touch();
        repo.Update(consumer);
        await worker.SaveChangesAsync();

        var stored = await StoredAsync(id);
        Assert.Equal(ApiKeys.Hash("new-key"), stored.KeyHash);
        Assert.Equal("crms_newkey", stored.KeyPrefix);
        Assert.NotNull(stored.LastUsedAt);
    }

    [Fact]
    public async Task An_administrators_long_session_sees_and_keeps_a_key_replaced_elsewhere()
    {
        var id = await SeedAsync();

        // The administrator's session has had the consumer loaded for a while.
        await using var session = Session();
        var repo = new ApiConsumerRepository(session);
        Assert.Equal("crms_oldkey", (await repo.GetByIdAsync(id))!.KeyPrefix);

        await ReplaceKeyElsewhereAsync(id);

        // The list shows the row as it is now, not the copy the session holds.
        Assert.Equal("crms_newkey", (await repo.GetAllAsync()).Single().KeyPrefix);

        // Editing something else from that session leaves the new key alone.
        var consumer = (await repo.GetByIdAsync(id))!;
        Assert.Equal(ApiKeys.Hash("new-key"), consumer.KeyHash);
        Assert.True(consumer.Update("AgriPay", CustomerCheckSections.All, 45, false, 30, "renewed", Admin).IsSuccess);
        repo.Update(consumer);
        await session.SaveChangesAsync();

        var stored = await StoredAsync(id);
        Assert.Equal(ApiKeys.Hash("new-key"), stored.KeyHash);
        Assert.Equal(45, stored.PerMinuteLimit);
        Assert.Equal("renewed", stored.Notes);
    }

    [Fact]
    public async Task Work_in_progress_in_the_same_session_is_not_thrown_away_by_a_second_load()
    {
        var id = await SeedAsync();
        await using var session = Session();
        var repo = new ApiConsumerRepository(session);

        var consumer = (await repo.GetByIdAsync(id))!;
        consumer.SetActive(false, Admin);

        Assert.Same(consumer, await repo.GetByIdAsync(id));
        Assert.False(consumer.IsActive);
    }
}
