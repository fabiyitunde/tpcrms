using CRMS.Infrastructure.Documents;

namespace CRMS.Infrastructure.Tests.Documents;

/// <summary>
/// The amount in words printed on offer letters. Worth testing closely: the figure appears twice on
/// every letter, a borrower signs against it, and a wrong word is not something a reader can correct
/// from the digits beside it.
/// </summary>
public class NairaAmountInWordsTests
{
    [Fact]
    public void MatchesTheApprovedTemplateExactly()
    {
        // The wording on the approved Sagad offer, which is what this has to reproduce.
        var words = NairaAmountInWords.Format(245_563_704.00m);

        Assert.Equal(
            "Two hundred and forty-five million, five hundred and sixty-three thousand, "
            + "seven hundred and four naira, zero kobo",
            words);
    }

    [Theory]
    [InlineData(0, "Zero naira, zero kobo")]
    [InlineData(1, "One naira, zero kobo")]
    [InlineData(15, "Fifteen naira, zero kobo")]
    [InlineData(40, "Forty naira, zero kobo")]
    [InlineData(45, "Forty-five naira, zero kobo")]
    [InlineData(100, "One hundred naira, zero kobo")]
    [InlineData(104, "One hundred and four naira, zero kobo")]
    [InlineData(1_000, "One thousand naira, zero kobo")]
    [InlineData(1_004, "One thousand and four naira, zero kobo")]
    [InlineData(1_104, "One thousand, one hundred and four naira, zero kobo")]
    [InlineData(1_000_000, "One million naira, zero kobo")]
    public void SpellsWholeNaira(long amount, string expected)
    {
        Assert.Equal(expected, NairaAmountInWords.Format(amount));
    }

    [Theory]
    [InlineData("0.01", "Zero naira, one kobo")]
    [InlineData("0.50", "Zero naira, fifty kobo")]
    [InlineData("1.99", "One naira, ninety-nine kobo")]
    [InlineData("2455637.04", "Two million, four hundred and fifty-five thousand, six hundred and thirty-seven naira, four kobo")]
    public void SpellsKobo(string amount, string expected)
    {
        Assert.Equal(expected, NairaAmountInWords.Format(decimal.Parse(amount)));
    }

    [Fact]
    public void RoundsKoboAwayFromZero_AndCarriesIntoNaira()
    {
        // 99.999 must not read as "ninety-nine naira, one hundred kobo".
        Assert.Equal("One hundred naira, zero kobo", NairaAmountInWords.Format(99.999m));
        Assert.Equal("One naira, zero kobo", NairaAmountInWords.Format(0.995m));
    }

    [Fact]
    public void HandlesTheFeeFiguresFromTheTemplate()
    {
        // The 1% appraisal fee and 0.5% monitoring fee on the Sagad facility.
        Assert.Equal(
            "Two million, four hundred and fifty-five thousand, six hundred and thirty-seven naira, four kobo",
            NairaAmountInWords.Format(2_455_637.04m));
        Assert.Equal(
            "One million, two hundred and twenty-seven thousand, eight hundred and eighteen naira, fifty-two kobo",
            NairaAmountInWords.Format(1_227_818.52m));
    }

    [Fact]
    public void UpperCasesForTheSubjectLine()
    {
        Assert.Equal(
            "TWO HUNDRED AND FORTY-FIVE MILLION, FIVE HUNDRED AND SIXTY-THREE THOUSAND, "
            + "SEVEN HUNDRED AND FOUR NAIRA, ZERO KOBO",
            NairaAmountInWords.FormatUpper(245_563_704.00m));
    }

    [Fact]
    public void HandlesAmountsLargerThanTheProgrammeWillEverSee()
    {
        // Not a real facility size — a guard that the scale loop does not silently drop a group.
        Assert.Equal("One billion naira, zero kobo", NairaAmountInWords.Format(1_000_000_000m));
        Assert.Equal(
            "One trillion, one billion, one million, one thousand and one naira, zero kobo",
            NairaAmountInWords.Format(1_001_001_001_001m));
    }

    [Fact]
    public void HandlesNegatives_RatherThanProducingNonsense()
    {
        // No offer is negative, but a caller passing a difference should not get silent rubbish.
        Assert.Equal("minus One naira, zero kobo", NairaAmountInWords.Format(-1m));
    }
}
