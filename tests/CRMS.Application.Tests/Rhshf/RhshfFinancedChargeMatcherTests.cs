using CRMS.Application.Rhshf.Services;
using Xunit;

namespace CRMS.Application.Tests.Rhshf;

/// <summary>
/// Recognising a core banking fee as something the EOP already finances.
///
/// The programme's rule is that no fee may be financed inside the EOP and then deducted again at
/// booking. CRMS never adds those fees itself, but the offer letter prints whatever the core banking
/// product says it will deduct — so a product configured with insurance would have the letter
/// promise the borrower exactly the double charge the rule forbids.
///
/// Both directions of failure matter. Missing a collision puts a double charge in writing; matching
/// too eagerly drops a legitimate fee off the letter, which nobody notices until the borrower
/// queries a deduction they were never told about.
/// </summary>
public class RhshfFinancedChargeMatcherTests
{
    /// <summary>What the portal sends on a fully financed EOP.</summary>
    private static readonly (string Code, string Label)[] Financed =
    [
        ("insurance", "Insurance"),
        ("workingCapital", "Working capital"),
        ("techServicesFee", "Technology services fee"),
    ];

    [Theory]
    [InlineData("Insurance")]
    [InlineData("Insurance Fee")]
    [InlineData("INSURANCE PREMIUM")]
    [InlineData("Working Capital")]
    [InlineData("Working Capital Fee")]
    [InlineData("WORKING-CAPITAL")]
    [InlineData("Technology Services Fee")]
    [InlineData("Tech Services Fee")]
    public void AFeeTheEopAlreadyFinancesIsRecognised(string productCharge)
        => Assert.True(RhshfFinancedChargeMatcher.IsAlreadyFinanced(productCharge, Financed));

    [Theory]
    [InlineData("Appraisal Fee")]          // 1%, correctly deducted at booking
    [InlineData("Monitoring Fee")]         // 0.5%, likewise
    [InlineData("Capital Gains Levy")]     // contains "capital", is not working capital
    [InlineData("Processing Fee")]
    [InlineData("Legal Documentation Fee")]
    public void AFeeThatIsGenuinelyDeductedAtBookingIsLeftAlone(string productCharge)
        => Assert.False(RhshfFinancedChargeMatcher.IsAlreadyFinanced(productCharge, Financed));

    [Fact]
    public void NothingIsSuppressedOnACaseWithNoItemisedFinancing()
    {
        // Older cases carry no financing block, so nothing is known to be financed and every product
        // fee stands — the letter behaves exactly as it did before this guard existed.
        Assert.False(RhshfFinancedChargeMatcher.IsAlreadyFinanced("Insurance Fee", []));
    }

    [Fact]
    public void AChargeWithNoNameIsNotTreatedAsACollision()
    {
        Assert.False(RhshfFinancedChargeMatcher.IsAlreadyFinanced("", Financed));
        Assert.False(RhshfFinancedChargeMatcher.IsAlreadyFinanced("   ", Financed));
    }

    [Fact]
    public void AShortLabelDoesNotMatchEverythingByAccident()
    {
        // A two-letter label would otherwise be a substring of half the fee names in the catalogue.
        (string, string)[] financed = [("other", "WC")];

        Assert.False(RhshfFinancedChargeMatcher.IsAlreadyFinanced("WC Processing Fee", financed));
    }

    [Fact]
    public void AnUnknownChargeCodeStillMatchesOnItsLabel()
    {
        // If the portal adds a fourth programme charge, its label alone carries the match until the
        // synonym list learns the new code.
        (string, string)[] financed = [("logisticsLevy", "Logistics levy")];

        Assert.True(RhshfFinancedChargeMatcher.IsAlreadyFinanced("Logistics Levy Fee", financed));
        Assert.False(RhshfFinancedChargeMatcher.IsAlreadyFinanced("Appraisal Fee", financed));
    }
}
