using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

public class RhshfLegalClearanceTests
{
    [Fact]
    public void Create_WithDistinctLegalOfficerAndFinalApprover_Succeeds()
    {
        var result = RhshfLegalClearance.Create(
            Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), RhshfLegalClearanceOutcome.Granted, "all clear");

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfLegalClearanceOutcome.Granted, result.Value.Outcome);
    }

    [Fact]
    public void Create_WithSamePersonAsLegalOfficerAndFinalApprover_Fails()
    {
        var sharedId = Guid.NewGuid();

        var result = RhshfLegalClearance.Create(
            Guid.NewGuid(), 1, sharedId, sharedId, RhshfLegalClearanceOutcome.Granted, null);

        Assert.True(result.IsFailure);
    }
}
