using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

public class RhshfLegalClearanceTests
{
    [Fact]
    public void Create_RecordsTheOutcomeAndTheClearingOfficer()
    {
        var legalOfficerId = Guid.NewGuid();

        var result = RhshfLegalClearance.Create(
            Guid.NewGuid(), 1, legalOfficerId, RhshfLegalClearanceOutcome.Granted, "all clear");

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfLegalClearanceOutcome.Granted, result.Value.Outcome);
        // Risk review, the committee vote and ratification all exclude this id, so it has to be stored.
        Assert.Equal(legalOfficerId, result.Value.LegalOfficerId);
    }

    [Fact]
    public void Create_WithoutALegalOfficer_Fails()
    {
        var result = RhshfLegalClearance.Create(
            Guid.NewGuid(), 1, Guid.Empty, RhshfLegalClearanceOutcome.Granted, null);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_NoLongerChecksAFinalApprover_BecauseNoneExistsYet()
    {
        // Clearance happens before committee voting and ratification, so there is no ratifier to be
        // distinct from at this point — the exclusion runs the other way round, at the later stages.
        var sharedId = Guid.NewGuid();

        var result = RhshfLegalClearance.Create(
            Guid.NewGuid(), 1, sharedId, RhshfLegalClearanceOutcome.Granted, null);

        Assert.True(result.IsSuccess);
    }
}
