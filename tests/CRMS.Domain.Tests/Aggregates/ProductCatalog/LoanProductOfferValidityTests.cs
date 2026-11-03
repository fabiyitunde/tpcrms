using CRMS.Domain.Aggregates.ProductCatalog;
using CRMS.Domain.Enums;
using CRMS.Domain.ValueObjects;

namespace CRMS.Domain.Tests.Aggregates.ProductCatalog;

/// <summary>The RH-SHF offer acceptance period is an administrator's setting: the product paper and
/// core banking define none.</summary>
public class LoanProductOfferValidityTests
{
    private static LoanProduct Product() => LoanProduct.Create("RHSH", "RH-SHF", "", LoanProductType.Rhshf,
        Money.Create(1_000_000), Money.Create(100_000_000), 1, 1).Value;

    [Fact]
    public void Unset_means_the_seven_day_default()
    {
        var product = Product();

        Assert.Null(product.OfferValidityDays);
        Assert.Equal(7, product.EffectiveOfferValidityDays);
    }

    [Fact]
    public void A_set_period_is_used_and_can_be_cleared_back_to_the_default()
    {
        var product = Product();

        Assert.True(product.SetOfferValidityDays(14).IsSuccess);
        Assert.Equal(14, product.EffectiveOfferValidityDays);

        Assert.True(product.SetOfferValidityDays(null).IsSuccess);
        Assert.Equal(7, product.EffectiveOfferValidityDays);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(91)]
    public void A_period_outside_one_to_ninety_days_is_refused(int days)
    {
        var product = Product();

        Assert.True(product.SetOfferValidityDays(days).IsFailure);
        Assert.Null(product.OfferValidityDays);
    }
}
