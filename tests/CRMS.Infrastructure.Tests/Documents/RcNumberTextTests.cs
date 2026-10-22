using CRMS.Infrastructure.Documents;
using Xunit;

namespace CRMS.Infrastructure.Tests.Documents;

/// <summary>
/// How a borrower's registration number reads on a document.
///
/// The offer document used to prepend "RC" to a number that already carried one, so the borrower
/// line and the acceptance block both read "Sagad Farms Limited (RC RC771204)" — on the one page the
/// borrower signs. These pin the normalisation in both directions: a bare number gains the prefix,
/// a prefixed one does not gain a second.
/// </summary>
public class RcNumberTextTests
{
    [Theory]
    [InlineData("771204", "RC 771204")]        // bare, as the old code assumed
    [InlineData("RC771204", "RC 771204")]      // how CAC and the portal actually send it
    [InlineData("RC 771204", "RC 771204")]     // already spaced
    [InlineData("rc771204", "RC 771204")]      // lower case
    [InlineData("rc-771204", "RC 771204")]     // separated
    [InlineData("  RC771204  ", "RC 771204")]  // padded
    public void Format_LeavesExactlyOnePrefix(string stored, string expected)
        => Assert.Equal(expected, RcNumberText.Format(stored));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Format_IsEmptyWhenNoNumberIsHeld(string? stored)
        => Assert.Equal(string.Empty, RcNumberText.Format(stored));

    [Fact]
    public void Format_KeepsAValueThatIsNothingButThePrefix()
    {
        // Not a number at all. Printing "RC " with nothing after it would look like data loss, so the
        // original text is kept and whoever reads it can see what is actually on file.
        Assert.Equal("RC RC", RcNumberText.Format("RC"));
    }

    [Fact]
    public void Parenthesised_IsEmptyWhenNoNumberIsHeld_SoCallersCanAppendBlind()
    {
        Assert.Equal(string.Empty, RcNumberText.Parenthesised(null));
        Assert.Equal(" (RC 771204)", RcNumberText.Parenthesised("RC771204"));
    }

    [Fact]
    public void WithCompany_NamesTheBorrowerTheWayTheDocumentShould()
    {
        Assert.Equal("Sagad Farms Limited (RC 771204)",
            RcNumberText.WithCompany("Sagad Farms Limited", "RC771204"));

        // No number on file: the name stands alone rather than trailing an empty bracket.
        Assert.Equal("Sagad Farms Limited", RcNumberText.WithCompany("Sagad Farms Limited", null));
    }
}
