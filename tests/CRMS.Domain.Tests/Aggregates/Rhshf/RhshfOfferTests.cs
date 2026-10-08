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

    [Fact]
    public void Accept_RequiresBothSignedOfferLetterAndSignedKfs_WhenAKfsWasIssued()
    {
        var offer = RhshfOffer.Create(Guid.NewGuid(), 1, "offer.pdf").Value;
        offer.AttachKfs("kfs.pdf");
        offer.AddDocument("signed-offer.pdf", "application/pdf", "p1.pdf", 1024, RhshfOfferDocumentKind.SignedOfferLetter);

        var blocked = offer.Accept(null); // KFS not signed yet
        Assert.True(blocked.IsFailure);

        offer.AddDocument("signed-kfs.pdf", "application/pdf", "p2.pdf", 1024, RhshfOfferDocumentKind.SignedKfs);
        var ok = offer.Accept(null);
        Assert.True(ok.IsSuccess);
    }

    [Fact]
    public void RegenerateDocument_WhileGenerated_ReplacesThePath()
    {
        var offer = RhshfOffer.Create(Guid.NewGuid(), 1, "old/path.pdf").Value;

        var result = offer.RegenerateDocument("new/path.pdf");

        Assert.True(result.IsSuccess);
        Assert.Equal("new/path.pdf", offer.OfferDocumentPath);
    }

    [Fact]
    public void RemoveDocument_WhileGenerated_RemovesIt()
    {
        var offer = RhshfOffer.Create(Guid.NewGuid(), 1, "offer.pdf").Value;
        var doc = offer.AddDocument("wrong.pdf", "application/pdf", "p1.pdf", 1024, RhshfOfferDocumentKind.SignedOfferLetter).Value;

        var result = offer.RemoveDocument(doc.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(offer.Documents);
    }

    [Fact]
    public void RemoveDocument_UnknownId_Fails()
    {
        var offer = RhshfOffer.Create(Guid.NewGuid(), 1, "offer.pdf").Value;

        var result = offer.RemoveDocument(Guid.NewGuid());

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void RemoveDocument_AfterDecided_Fails()
    {
        // Once accepted the signed package is part of the contractual record — it can't be pulled.
        var offer = RhshfOffer.Create(Guid.NewGuid(), 1, "offer.pdf").Value;
        var doc = offer.AddDocument("signed.pdf", "application/pdf", "p1.pdf", 1024, RhshfOfferDocumentKind.SignedOfferLetter).Value;
        offer.Accept(null);

        var result = offer.RemoveDocument(doc.Id);

        Assert.True(result.IsFailure);
        Assert.Single(offer.Documents);
    }

    [Fact]
    public void RegenerateDocument_AfterAccepted_IsRejected()
    {
        // An accepted offer is contractual — the letter must not be silently replaced.
        var offer = RhshfOffer.Create(Guid.NewGuid(), 1, "old/path.pdf").Value;
        offer.AddDocument("signed.pdf", "application/pdf", "signed/path.pdf", 1024);
        offer.Accept(null);

        var result = offer.RegenerateDocument("new/path.pdf");

        Assert.True(result.IsFailure);
        Assert.Equal("old/path.pdf", offer.OfferDocumentPath);
    }
}
