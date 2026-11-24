using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CRMS.Infrastructure.Tests.Rhshf;

/// <summary>
/// The pre-deployment account check shows the FAC's own BOA account, but its old wording called it
/// the input supplier's or the disbursement account. The back-fill rewords it where the default
/// wording is untouched, and leaves anything an administrator changed.
/// </summary>
public class RhshfRepaymentAccountWordingBackfillTests
{
    private static CRMSDbContext NewContext() => new(new DbContextOptionsBuilder<CRMSDbContext>()
        .UseInMemoryDatabase("RepaymentWording_" + Guid.NewGuid()).Options);

    private static RhshfPreDeploymentChecklistTemplate Template(string title, string description) =>
        RhshfPreDeploymentChecklistTemplate.Create(title, description, isMandatory: true, sortOrder: 10,
            RhshfPreDeploymentVerificationKind.AccountConfirmation).Value;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task An_old_default_wording_is_replaced(int wording)
    {
        await using var context = NewContext();
        var (title, description) = RhshfWorkflowSeeder.OldAccountCheckWordings[wording];
        context.RhshfPreDeploymentChecklistTemplates.Add(Template(title, description));
        await context.SaveChangesAsync();

        await RhshfWorkflowSeeder.BackfillRepaymentAccountWordingAsync(context, NullLogger.Instance);

        var t = await context.RhshfPreDeploymentChecklistTemplates.SingleAsync();
        Assert.Equal(RhshfWorkflowSeeder.RepaymentAccountTitle, t.Title);
        Assert.Equal(RhshfWorkflowSeeder.RepaymentAccountDescription, t.Description);
        Assert.Equal(RhshfPreDeploymentVerificationKind.AccountConfirmation, t.Kind);
    }

    [Fact]
    public async Task Wording_an_administrator_changed_is_left_alone()
    {
        await using var context = NewContext();
        context.RhshfPreDeploymentChecklistTemplates.Add(
            Template("Disbursement Account Verified", "Our own wording for this check."));
        await context.SaveChangesAsync();

        await RhshfWorkflowSeeder.BackfillRepaymentAccountWordingAsync(context, NullLogger.Instance);

        var t = await context.RhshfPreDeploymentChecklistTemplates.SingleAsync();
        Assert.Equal("Disbursement Account Verified", t.Title);
        Assert.Equal("Our own wording for this check.", t.Description);
    }
}
