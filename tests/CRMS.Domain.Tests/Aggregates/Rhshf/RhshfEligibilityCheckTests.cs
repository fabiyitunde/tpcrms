using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

public class RhshfEligibilityCheckTests
{
    [Fact]
    public void Create_Satisfied_Succeeds()
    {
        var result = RhshfEligibilityCheck.Create(
            Guid.NewGuid(), 1, RhshfEligibilityCriterion.CacIncorporation, isSatisfied: true, notes: null, verifiedBy: Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsSatisfied);
    }

    [Fact]
    public void Create_Unsatisfied_WithNotes_Succeeds()
    {
        var result = RhshfEligibilityCheck.Create(
            Guid.NewGuid(), 1, RhshfEligibilityCriterion.ZeroNplHistory, isSatisfied: false,
            notes: "One restructured facility found on record", verifiedBy: Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsSatisfied);
        Assert.Equal("One restructured facility found on record", result.Value.Notes);
    }

    [Fact]
    public void Create_WithoutVerifiedBy_Fails()
    {
        var result = RhshfEligibilityCheck.Create(
            Guid.NewGuid(), 1, RhshfEligibilityCriterion.CacIncorporation, isSatisfied: true, notes: null, verifiedBy: Guid.Empty);

        Assert.True(result.IsFailure);
    }
}
