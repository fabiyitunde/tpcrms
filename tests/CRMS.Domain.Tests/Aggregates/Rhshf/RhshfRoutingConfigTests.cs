using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

public class RhshfRoutingConfigTests
{
    [Fact]
    public void Create_ValidRange_Succeeds()
    {
        var result = RhshfRoutingConfig.Create(CommitteeType.BranchCredit, 0, 2_000_000m, priority: 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(CommitteeType.BranchCredit, result.Value.Tier);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public void Create_MaxLessThanMin_Fails()
    {
        var result = RhshfRoutingConfig.Create(CommitteeType.BranchCredit, 2_000_000m, 1_000_000m);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Update_MaxLessThanMin_Fails()
    {
        var config = RhshfRoutingConfig.Create(CommitteeType.ZonalCredit, 2_000_001m, 10_000_000m).Value;

        var result = config.Update(10_000_000m, 2_000_001m, priority: 2);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ActivateDeactivate_TogglesIsActive()
    {
        var config = RhshfRoutingConfig.Create(CommitteeType.RegionalCredit, 10_000_001m, 30_000_000m).Value;

        config.Deactivate();
        Assert.False(config.IsActive);

        config.Activate();
        Assert.True(config.IsActive);
    }
}
