using CRMS.Application.CustomerCheck.Commands;
using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// The setup link repository leans on conditional updates (ExecuteUpdate) and a grouped query,
/// which the in-memory provider either does not support or does not translate, so it cannot vouch
/// for them. These build each statement with the real MySQL provider against an address nothing
/// listens on: EF translates the query before it opens a connection, so a statement MySQL could
/// not run fails with "could not be translated", and one that is fine fails only at the door.
/// </summary>
public class ApiConsumerSetupLinkRepositoryTests
{
    private static CRMSDbContext NowhereContext()
    {
        var options = new DbContextOptionsBuilder<CRMSDbContext>()
            .UseMySql("Server=127.0.0.1;Port=1;Database=none;User=none;Password=none;Connection Timeout=2;",
                new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;
        return new CRMSDbContext(options);
    }

    private static async Task AssertTranslatesAsync(Func<ApiConsumerSetupLinkRepository, Task> statement)
    {
        await using var context = NowhereContext();
        var failure = await Record.ExceptionAsync(() => statement(new ApiConsumerSetupLinkRepository(context)));

        // Reaching the (absent) server is the pass: the statement was built.
        Assert.NotNull(failure);
        var text = failure.ToString();
        Assert.DoesNotContain("could not be translated", text);
        Assert.DoesNotContain("not supported", text);
        Assert.Contains("connect", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public Task Claiming_a_link_is_one_conditional_update() =>
        AssertTranslatesAsync(r => r.TryClaimAsync(Guid.NewGuid(), DateTime.UtcNow, "203.0.113.7"));

    [Fact]
    public Task Releasing_a_claim_translates() =>
        AssertTranslatesAsync(r => r.ReleaseAsync(Guid.NewGuid()));

    [Fact]
    public Task Revoking_open_links_translates() =>
        AssertTranslatesAsync(r => r.RevokeOpenAsync(Guid.NewGuid(), DateTime.UtcNow));

    [Fact]
    public Task Finding_a_link_by_its_token_translates() =>
        AssertTranslatesAsync(r => r.GetByTokenHashAsync(ApiConsumerSetupLinks.Hash("token")));

    [Fact]
    public Task A_consumers_history_translates() =>
        AssertTranslatesAsync(r => r.GetByConsumerAsync(Guid.NewGuid(), 10));

    [Fact]
    public Task The_newest_link_per_consumer_translates() =>
        AssertTranslatesAsync(r => r.GetLatestPerConsumerAsync());

    [Fact]
    public void The_link_table_holds_a_hash_of_the_token_and_nowhere_to_put_a_secret()
    {
        using var context = NowhereContext();
        var entity = context.Model.FindEntityType(typeof(ApiConsumerSetupLink))!;

        Assert.Equal("ApiConsumerSetupLinks", entity.GetTableName());
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique && i.Properties.Single().Name == nameof(ApiConsumerSetupLink.TokenHash));
        // Every text column, by name. A key, a secret or the token itself would have to be one
        // of these; adding such a column should mean changing this list on purpose.
        var text = entity.GetProperties().Where(p => p.ClrType == typeof(string)).Select(p => p.Name).Order();
        Assert.Equal(["CreatedBy", "ModifiedBy", "OpenedFromIp", "SentByName", "SentTo", "TokenHash"], text);
    }
}
