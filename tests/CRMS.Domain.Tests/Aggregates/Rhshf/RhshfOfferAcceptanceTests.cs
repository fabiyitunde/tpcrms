using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

public class RhshfOfferAcceptanceTests
{
    private static RhshfOffer CreateGeneratedOffer()
        => RhshfOffer.Create(Guid.NewGuid(), cycleNumber: 1, "rhshf-offers/RHSHF-2026-000123/offer.pdf").Value;

    [Fact]
    public void Accept_WithoutUploadedDocument_Fails()
    {
        var offer = CreateGeneratedOffer();

        var result = offer.Accept(null);

        Assert.True(result.IsFailure);
        Assert.Equal(RhshfOfferStatus.Generated, offer.Status);
    }

    [Fact]
    public void Accept_WithUploadedDocument_Succeeds()
    {
        var offer = CreateGeneratedOffer();
        offer.AddDocument("signed-offer.pdf", "application/pdf", "rhshf-offer-signed/path.pdf", 2048);

        var result = offer.Accept("Looks good");

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfOfferStatus.Accepted, offer.Status);
        Assert.NotNull(offer.FacRespondedAt);
        Assert.Equal("Looks good", offer.FacResponseNotes);
    }

    [Fact]
    public void Reject_WithoutAnyDocument_Succeeds()
    {
        var offer = CreateGeneratedOffer();

        var result = offer.Reject("Terms not acceptable");

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfOfferStatus.Rejected, offer.Status);
    }

    [Fact]
    public void AddDocument_AllowsMultipleUploadsBeforeDecision()
    {
        var offer = CreateGeneratedOffer();

        var first = offer.AddDocument("scan1.pdf", "application/pdf", "path1", 100);
        var second = offer.AddDocument("scan2.pdf", "application/pdf", "path2", 200);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, offer.Documents.Count);
    }

    [Fact]
    public void AddDocument_AfterDecision_Fails()
    {
        var offer = CreateGeneratedOffer();
        offer.Reject(null);

        var result = offer.AddDocument("late.pdf", "application/pdf", "path", 100);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Accept_AfterAlreadyDecided_Fails()
    {
        var offer = CreateGeneratedOffer();
        offer.AddDocument("signed.pdf", "application/pdf", "path", 100);
        offer.Accept(null);

        var result = offer.Accept(null);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Reject_AfterAlreadyDecided_Fails()
    {
        var offer = CreateGeneratedOffer();
        offer.Reject(null);

        var result = offer.Reject(null);

        Assert.True(result.IsFailure);
    }
}
