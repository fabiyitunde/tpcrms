using CRMS.Domain.Common;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

/// <summary>
/// The guarantor relationship categories, which replaced a free-text field.
///
/// The category lives in its own column and the original free-text column became the "Other"
/// specification — converting the old column to a string-persisted enum would throw on reading any
/// row holding text that is not an enum member, and every pre-existing row holds whatever staff
/// typed. ClassifyLegacyRelationship is what the startup backfill uses to reclassify those rows
/// without discarding the wording.
/// </summary>
public class RhshfGuarantorRelationshipTests
{
    [Fact]
    public void Create_WithACategory_StoresItAndLeavesTheSpecificationEmpty()
    {
        var result = Create(RhshfGuarantorRelationship.Director, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfGuarantorRelationship.Director, result.Value.RelationshipCategory);
        Assert.Null(result.Value.RelationshipOther);
        Assert.Equal("Director", result.Value.RelationshipDisplay);
    }

    [Fact]
    public void Create_WithoutACategory_IsRefused()
    {
        var result = Create(null, null);

        Assert.True(result.IsFailure);
        Assert.Contains("relationship to the FAC", result.Error);
    }

    [Fact]
    public void Create_Other_WithoutASpecification_IsRefused()
    {
        var result = Create(RhshfGuarantorRelationship.Other, null);

        Assert.True(result.IsFailure);
        Assert.Contains("State the relationship", result.Error);
    }

    [Fact]
    public void Create_Other_WithASpecification_ReadsAsThatText()
    {
        var result = Create(RhshfGuarantorRelationship.Other, "Produce off-taker");

        Assert.True(result.IsSuccess);
        Assert.Equal("Produce off-taker", result.Value.RelationshipOther);
        Assert.Equal("Produce off-taker", result.Value.RelationshipDisplay);
    }

    [Fact]
    public void RelationshipDisplay_SpacesOutAMultiWordCategory()
    {
        // The dropdown that produced it says "Parent Company", so the FAC's table, the staff tab and
        // the loan pack PDF must not say "ParentCompany".
        var result = Create(RhshfGuarantorRelationship.ParentCompany, null);

        Assert.Equal("Parent Company", result.Value.RelationshipDisplay);
    }

    [Fact]
    public void Update_AwayFromOther_ClearsTheSpecification()
    {
        // Otherwise the old text stays visible beside a category that contradicts it.
        var guarantor = Create(RhshfGuarantorRelationship.Other, "Produce off-taker").Value;

        var result = guarantor.Update(
            RhshfGuarantorType.Individual, "Ada Surety", "12345678901", null,
            RhshfGuarantorRelationship.Shareholder, "Produce off-taker",
            null, null, null, 1_000_000m, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfGuarantorRelationship.Shareholder, guarantor.RelationshipCategory);
        Assert.Null(guarantor.RelationshipOther);
        Assert.Equal("Shareholder", guarantor.RelationshipDisplay);
    }

    // ── RC number ─────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("RC123456", "RC123456")]
    [InlineData("rc 123-456", "RC123456")]
    [InlineData("BN/987654", "BN987654")]
    [InlineData("  1234567  ", "1234567")]
    public void Corporate_RcNumber_IsNormalised(string entered, string expected)
    {
        var result = RhshfGuarantor.Create(
            Guid.NewGuid(), RhshfGuarantorType.Corporate, "Sahel Holdings Limited", null, entered,
            RhshfGuarantorRelationship.ParentCompany, null, null, null, null, null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.RcNumber);
    }

    [Theory]
    [InlineData("RC1")]
    [InlineData("RC-12")]
    [InlineData("RC12345678901234567890123")]
    [InlineData("RC12#456")]
    public void Corporate_RcNumber_RejectsWhatCannotBeARegistration(string entered)
    {
        var result = RhshfGuarantor.Create(
            Guid.NewGuid(), RhshfGuarantorType.Corporate, "Sahel Holdings Limited", null, entered,
            RhshfGuarantorRelationship.ParentCompany, null, null, null, null, null, null);

        Assert.True(result.IsFailure);
        Assert.Contains("RC number", result.Error);
    }

    [Fact]
    public void SwitchingTypeToIndividual_ClearsTheRcNumber()
    {
        // A stale identifier would be checked against the wrong bureau.
        var guarantor = RhshfGuarantor.Create(
            Guid.NewGuid(), RhshfGuarantorType.Corporate, "Sahel Holdings Limited", null, "RC123456",
            RhshfGuarantorRelationship.ParentCompany, null, null, null, null, null, null).Value;

        guarantor.Update(
            RhshfGuarantorType.Individual, "Ada Surety", "12345678901", "RC123456",
            RhshfGuarantorRelationship.Director, null, null, null, null, null, null);

        Assert.Null(guarantor.RcNumber);
        Assert.Equal("12345678901", guarantor.Bvn);
    }

    // ── Backfill of historical free text ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Director", RhshfGuarantorRelationship.Director)]
    [InlineData("director", RhshfGuarantorRelationship.Director)]
    [InlineData("Shareholders", RhshfGuarantorRelationship.Shareholder)]
    [InlineData("Business Partner", RhshfGuarantorRelationship.BusinessPartner)]
    [InlineData("business-partner", RhshfGuarantorRelationship.BusinessPartner)]
    [InlineData("Parent Company", RhshfGuarantorRelationship.ParentCompany)]
    [InlineData("Sister Company", RhshfGuarantorRelationship.RelatedCompany)]
    public void ClassifyLegacyRelationship_MapsRecognisableText(string text, RhshfGuarantorRelationship expected)
    {
        var (category, other) = RhshfGuarantor.ClassifyLegacyRelationship(text);

        Assert.Equal(expected, category);
        Assert.Null(other);
    }

    [Fact]
    public void ClassifyLegacyRelationship_KeepsUnrecognisedTextUnderOther()
    {
        // Nothing staff recorded is discarded — it becomes the Other specification verbatim.
        var (category, other) = RhshfGuarantor.ClassifyLegacyRelationship("Village head, guarantees by custom");

        Assert.Equal(RhshfGuarantorRelationship.Other, category);
        Assert.Equal("Village head, guarantees by custom", other);
    }

    [Fact]
    public void BackfillRelationship_SetsTheCategoryWithoutTrippingTheWriteValidation()
    {
        // The backfill reclassifies what is already stored rather than accepting new input, so it is
        // allowed to land an Other with whatever text the row held.
        var guarantor = Create(RhshfGuarantorRelationship.Director, null).Value;

        guarantor.BackfillRelationship(RhshfGuarantorRelationship.Other, "Village head");

        Assert.Equal(RhshfGuarantorRelationship.Other, guarantor.RelationshipCategory);
        Assert.Equal("Village head", guarantor.RelationshipOther);
    }

    private static Result<RhshfGuarantor> Create(RhshfGuarantorRelationship? category, string? other) =>
        RhshfGuarantor.Create(
            Guid.NewGuid(), RhshfGuarantorType.Individual, "Ada Surety", "12345678901", null,
            category, other, null, null, null, 1_000_000m, null);
}
