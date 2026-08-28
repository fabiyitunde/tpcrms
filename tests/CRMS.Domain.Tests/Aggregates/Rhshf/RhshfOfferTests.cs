using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

public class RhshfOfferTests
{
    [Fact]
    public void Create_WithValidPath_Succeeds_StatusGenerated()
    {
        var result = RhshfOffer.Create(Guid.NewGuid(), cycleNumber: 1, "rhshf-offers/RHSHF-2026-000123/offer.pdf");

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfOfferStatus.Generated, result.Value.Status);
    }

    [Fact]
    public void Create_WithEmptyPath_Fails()
    {
        var result = RhshfOffer.Create(Guid.NewGuid(), 1, "");

        Assert.True(result.IsFailure);
    }
}
