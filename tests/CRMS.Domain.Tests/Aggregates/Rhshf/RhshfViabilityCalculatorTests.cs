using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

/// <summary>
/// The IRR tests here check against independently-derivable answers rather than against whatever
/// the implementation happens to return. This is the metric NAMP got wrong (its "IRR" is
/// annualSurplus/equity − 0.05, which is not an IRR), so it gets the most scrutiny.
/// </summary>
public class RhshfIrrTests
{
    [Fact]
    public void Irr_DoublingOverOneYear_Is100Percent()
    {
        // -100 at t=0, +200 at t=1 → NPV zero at r = 1.0 exactly.
        var irr = RhshfViabilityCalculator.Irr([-100m, 200m], cycleYears: 1m);

        Assert.NotNull(irr);
        Assert.Equal(1.0m, irr!.Value, precision: 4);
    }

    [Fact]
    public void Irr_NoGainOverOneYear_IsZero()
    {
        var irr = RhshfViabilityCalculator.Irr([-100m, 100m], cycleYears: 1m);

        Assert.NotNull(irr);
        Assert.Equal(0m, irr!.Value, precision: 4);
    }

    [Fact]
    public void Irr_HalfLossOverOneYear_IsMinus50Percent()
    {
        var irr = RhshfViabilityCalculator.Irr([-100m, 50m], cycleYears: 1m);

        Assert.NotNull(irr);
        Assert.Equal(-0.5m, irr!.Value, precision: 4);
    }

    [Fact]
    public void Irr_IsAnnualised_NotPeriodReturn()
    {
        // +21% over exactly half a year annualises to (1.21)^2 - 1 = 46.41%.
        // A period-return implementation would wrongly report 21% — this is the assertion that
        // distinguishes a real annualised IRR from a naive ratio.
        var irr = RhshfViabilityCalculator.Irr([-100m, 121m], cycleYears: 0.5m);

        Assert.NotNull(irr);
        Assert.Equal(0.4641m, irr!.Value, precision: 3);
    }

    [Fact]
    public void Irr_SixMonthCycle_MatchesClosedFormForTwoPointSeries()
    {
        // For a 2-point series the analytic answer is (inflow/outflow)^(1/years) - 1.
        // 180/150 = 1.2 over 0.5y → 1.2^2 - 1 = 0.44
        var irr = RhshfViabilityCalculator.Irr([-150m, 180m], cycleYears: 0.5m);

        Assert.NotNull(irr);
        Assert.Equal(0.44m, irr!.Value, precision: 3);
    }

    [Fact]
    public void Irr_AllPositiveFlows_ReturnsNull_RatherThanInventingANumber()
        => Assert.Null(RhshfViabilityCalculator.Irr([100m, 200m], cycleYears: 1m));

    [Fact]
    public void Irr_AllNegativeFlows_ReturnsNull()
        => Assert.Null(RhshfViabilityCalculator.Irr([-100m, -50m], cycleYears: 1m));

    [Fact]
    public void Irr_ZeroCycleLength_ReturnsNull()
        => Assert.Null(RhshfViabilityCalculator.Irr([-100m, 200m], cycleYears: 0m));

    [Fact]
    public void Npv_AtIrr_IsZero()
    {
        // Definitional cross-check: discounting at the computed IRR must zero the NPV.
        decimal[] series = [-250_000m, 410_000m];
        var irr = RhshfViabilityCalculator.Irr(series, cycleYears: 0.5m);

        var npvAtIrr = RhshfViabilityCalculator.Npv(series, cycleYears: 0.5m, hurdleRatePercent: irr!.Value * 100m);

        Assert.True(Math.Abs(npvAtIrr) < 0.5m, $"NPV at IRR should be ~0 but was {npvAtIrr}");
    }
}

public class RhshfViabilityCalculatorTests
{
    // A deliberately hand-workable scenario:
    //   revenue 10,000,000 | financed inputs 5,000,000 | own costs 1,000,000 + 500,000
    //   rate 12% over 6 months → interest = 5,000,000 × 0.12 × 0.5 = 300,000
    private static RhshfViabilityResult Baseline() => RhshfViabilityCalculator.Compute(
        grossRevenue: 10_000_000m,
        financedInputCost: 5_000_000m,
        ownProductionCost: 1_000_000m,
        harvestAndLogisticsCost: 500_000m,
        interestRatePercent: 12m,
        cycleMonths: 6);

    [Fact]
    public void Compute_InterestIsSimpleOverTheCycle_NotAnnualised()
    {
        var r = Baseline();

        Assert.Equal(300_000m, r.InterestCharge);
        Assert.Equal(5_300_000m, r.AmountDueAtHarvest);
    }

    [Fact]
    public void Compute_DebtServiceCashExcludesFinancedInputs()
    {
        var r = Baseline();

        // 10,000,000 − (1,000,000 + 500,000) = 8,500,000. The financed inputs are the loan, not a
        // competing outflow, so they must not be deducted here.
        Assert.Equal(8_500_000m, r.CashAvailableForDebtService);
        Assert.Equal(8_500_000m / 5_300_000m, r.Dscr, precision: 4);
    }

    [Fact]
    public void Compute_TotalCostIncludesInputsOwnCostsAndInterest()
    {
        var r = Baseline();

        Assert.Equal(6_800_000m, r.TotalCost);      // 5,000,000 + 1,500,000 + 300,000
        Assert.Equal(3_200_000m, r.GrossMargin);    // 10,000,000 − 6,800,000
        Assert.Equal(32m, r.GrossMarginPercent);
    }

    [Fact]
    public void Compute_NetReturnToFarmer_IsRevenueLessAllOutflows()
    {
        var r = Baseline();

        // 10,000,000 − 500,000 harvest − 5,300,000 repayment − 1,000,000 own production
        Assert.Equal(3_200_000m, r.NetReturnToFarmer);
    }

    [Fact]
    public void Compute_UnviablePlan_ProducesDscrBelowOne()
    {
        var r = RhshfViabilityCalculator.Compute(
            grossRevenue: 4_000_000m, financedInputCost: 5_000_000m,
            ownProductionCost: 500_000m, harvestAndLogisticsCost: 200_000m,
            interestRatePercent: 12m, cycleMonths: 6);

        Assert.True(r.Dscr < 1m);
        Assert.True(r.GrossMargin < 0);
    }

    [Fact]
    public void Compute_ZeroRevenue_DoesNotThrow_AndReportsZeroDscr()
    {
        var r = RhshfViabilityCalculator.Compute(0m, 5_000_000m, 0m, 0m, 12m, 6);

        Assert.Equal(0m, r.GrossRevenue);
        Assert.True(r.Dscr <= 0m);
    }

    [Fact]
    public void BreakEvenYield_IsTheYieldWhereDscrEqualsOne()
    {
        var r = Baseline();
        // 10 ha, price 200/kg. Expected yield in the baseline is 10,000,000/(10×200) = 5,000 kg/ha.
        var breakEven = RhshfViabilityCalculator.BreakEvenYieldPerHectare(
            totalHectares: 10m, expectedPricePerKg: 200m,
            ownCashCosts: r.OwnCashCosts, amountDueAtHarvest: r.AmountDueAtHarvest);

        Assert.NotNull(breakEven);
        // Required revenue = 5,300,000 + 1,500,000 = 6,800,000 → /(10×200) = 3,400 kg/ha
        Assert.Equal(3_400m, breakEven!.Value, precision: 2);

        // Feeding that yield back in must produce DSCR of exactly 1.0.
        var atBreakEven = RhshfViabilityCalculator.Compute(
            grossRevenue: 10m * breakEven.Value * 200m,
            financedInputCost: 5_000_000m, ownProductionCost: 1_000_000m,
            harvestAndLogisticsCost: 500_000m, interestRatePercent: 12m, cycleMonths: 6);
        Assert.Equal(1.0m, atBreakEven.Dscr, precision: 4);
    }

    [Fact]
    public void BreakEvenPrice_IsThePriceWhereDscrEqualsOne()
    {
        var r = Baseline();
        var breakEven = RhshfViabilityCalculator.BreakEvenPricePerKg(
            totalHectares: 10m, expectedYieldPerHectare: 5_000m,
            ownCashCosts: r.OwnCashCosts, amountDueAtHarvest: r.AmountDueAtHarvest);

        Assert.NotNull(breakEven);
        // 6,800,000 / (10 × 5,000) = 136/kg
        Assert.Equal(136m, breakEven!.Value, precision: 2);
    }

    [Fact]
    public void Headroom_IsPositiveWhenExpectedBeatsBreakEven_NegativeOtherwise()
    {
        Assert.Equal(32m, RhshfViabilityCalculator.HeadroomPercent(5_000m, 3_400m)!.Value, precision: 2);
        Assert.True(RhshfViabilityCalculator.HeadroomPercent(3_000m, 3_400m)!.Value < 0);
    }

    [Fact]
    public void YieldAndPriceHeadroom_AreIndependentSensitivities()
    {
        // Doubling hectares changes the break-even yield but the price sensitivity moves too —
        // what matters is that they are computed from different inputs and can disagree in sign.
        var r = Baseline();

        var yieldHeadroom = RhshfViabilityCalculator.HeadroomPercent(
            5_000m, RhshfViabilityCalculator.BreakEvenYieldPerHectare(10m, 200m, r.OwnCashCosts, r.AmountDueAtHarvest));
        // A collapsed farm-gate price with the same yield: yield buffer intact, price buffer gone.
        var priceHeadroomAtLowPrice = RhshfViabilityCalculator.HeadroomPercent(
            120m, RhshfViabilityCalculator.BreakEvenPricePerKg(10m, 5_000m, r.OwnCashCosts, r.AmountDueAtHarvest));

        Assert.True(yieldHeadroom!.Value > 0);
        Assert.True(priceHeadroomAtLowPrice!.Value < 0);
    }
}
