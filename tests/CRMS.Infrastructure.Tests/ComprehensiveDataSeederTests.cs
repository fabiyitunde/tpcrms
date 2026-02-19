using CRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CRMS.Infrastructure.Tests;

public class ComprehensiveDataSeederTests
{
    [Fact]
    public async Task SeedComprehensiveData_WithInMemoryDatabase_ShouldSucceed()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CRMSDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb_" + Guid.NewGuid())
            .Options;

        var mockLogger = new Mock<ILogger>();
        mockLogger.Setup(x => x.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()));

        using var context = new CRMSDbContext(options);

        // Act
        await ComprehensiveDataSeeder.SeedComprehensiveDataAsync(context, mockLogger.Object);

        // Assert - verify data was created
        Assert.True(await context.Users.AnyAsync(), "Users should be seeded");
        Assert.True(await context.LoanProducts.AnyAsync(), "Loan products should be seeded");
        Assert.True(await context.Roles.AnyAsync(), "Roles should be seeded");
        Assert.True(await context.WorkflowDefinitions.AnyAsync(), "Workflow definitions should be seeded");
        Assert.True(await context.LoanApplications.AnyAsync(), "Loan applications should be seeded");
        Assert.True(await context.NotificationTemplates.AnyAsync(), "Notification templates should be seeded");
        Assert.True(await context.ScoringParameters.AnyAsync(), "Scoring parameters should be seeded");
        Assert.True(await context.AuditLogs.AnyAsync(), "Audit logs should be seeded");
    }

    [Fact]
    public async Task SeedComprehensiveData_RunTwice_ShouldNotDuplicate()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CRMSDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb_NoDuplicates_" + Guid.NewGuid())
            .Options;

        var mockLogger = new Mock<ILogger>();

        using var context = new CRMSDbContext(options);

        // Act - run twice
        await ComprehensiveDataSeeder.SeedComprehensiveDataAsync(context, mockLogger.Object);
        var userCountAfterFirst = await context.Users.CountAsync();
        
        await ComprehensiveDataSeeder.SeedComprehensiveDataAsync(context, mockLogger.Object);
        var userCountAfterSecond = await context.Users.CountAsync();

        // Assert - counts should be the same
        Assert.Equal(userCountAfterFirst, userCountAfterSecond);
    }
}
