using CRMS.Application.Rhshf.Services;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

/// <summary>
/// Tenor and rate come from the core banking product, the one it books (CRMSQA#2). The RH-SHF
/// product is one repayment every nine months at 9% a year; the letter used to say six months, the
/// officer's crop-cycle figure.
/// </summary>
public class RhshfProductTermsTests
{
    private static FineractLoanProduct Product(int repayments = 1, int every = 9, string frequency = "Months", decimal rate = 9m)
        => new(7, "Renewed Hope Smallholder Farmers Programme (RHSHFP)", "RHSH", null, "NGN", "₦",
            1_000_000m, 5_000_000m, 10_000_000_000m, rate, rate, rate, rate, "Per year",
            repayments, repayments, repayments, every, frequency, "Equal installments", "Declining Balance",
            1, null, null, true);

    [Fact]
    public void Tenor_is_the_number_of_repayments_times_the_repayment_period()
    {
        var terms = RhshfProductTermsResolver.Build(Product()).Value;

        Assert.Equal(9, terms.TenorMonths);
        Assert.Equal(1, terms.NumberOfRepayments);
        Assert.Equal(9m, terms.AnnualRatePercent);
        Assert.Equal("Declining Balance", terms.InterestType);
    }

    [Fact]
    public void Grandbai_interest_over_nine_months_is_estimated_on_the_approved_amount()
        => Assert.Equal(869_701.32m, RhshfProductTermsResolver.Build(Product()).Value.EstimatedInterest(12_884_464.00m));

    [Fact]
    public void A_product_not_counted_in_months_is_refused_rather_than_guessed()
    {
        var result = RhshfProductTermsResolver.Build(Product(repayments: 36, every: 1, frequency: "Weeks"));

        Assert.True(result.IsFailure);
        Assert.Contains("months", result.Error);
    }

    [Fact]
    public void A_product_repaid_in_instalments_is_refused()
    {
        // TEST's RH-SHF product was once linked to "MICRO NON AGRIC": six monthly repayments at 15%.
        var result = RhshfProductTermsResolver.Build(Product(repayments: 6, every: 1, rate: 15m));

        Assert.True(result.IsFailure);
        Assert.Contains("single payment", result.Error);
    }

    [Fact]
    public void A_product_without_a_schedule_is_refused()
        => Assert.True(RhshfProductTermsResolver.Build(Product(repayments: 0)).IsFailure);
}
