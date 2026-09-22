namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// Crop-economics viability maths for RH-SHF dry-season input financing.
///
/// Lives in the domain and is re-computed server-side on every save — unlike NAMP's equivalent,
/// which computes entirely in the Razor view layer and whose handler stores whatever the browser
/// posted without re-deriving or validating anything.
///
/// Product shape drives the model: this is a SINGLE-CYCLE BULLET loan. The bank finances the input
/// package (the EOP) up front; the farmer repays principal + interest in one payment at harvest.
/// That is not a multi-year capital investment, so NAMP's 5-year perpetual-surplus NPV framework
/// does not transfer.
///
/// Metric independence (the defect this deliberately avoids):
/// for any conventional cash-flow series, NPV &gt; 0 ⟺ IRR &gt; hurdle ⟺ BCR &gt; 1, and NAMP's
/// Profitability Index is algebraically identical to its BCR. NAMP gates on all of NPV, BCR, IRR and
/// PI, so four of its five badges can never disagree. Here exactly one profitability gate is used
/// (IRR); NPV is surfaced as context only, and the remaining gates measure genuinely different
/// things — debt service capacity, enterprise margin, and two separate downside sensitivities
/// (yield risk vs price risk).
/// </summary>
public static class RhshfViabilityCalculator
{
    /// <param name="grossRevenue">Σ per-crop (hectares × yield/ha × price/kg).</param>
    /// <param name="financedInputCost">The EOP value the bank disburses to the supplier.</param>
    /// <param name="ownProductionCost">Land prep, planting and weeding labour — the farmer's own outlay.</param>
    /// <param name="harvestAndLogisticsCost">Harvest, haulage and aggregation — also the farmer's own outlay.</param>
    /// <param name="interestRatePercent">Nominal annual rate on the facility.</param>
    /// <param name="cycleMonths">Length of the production cycle, disbursement to repayment.</param>
    public static RhshfViabilityResult Compute(
        decimal grossRevenue,
        decimal financedInputCost,
        decimal ownProductionCost,
        decimal harvestAndLogisticsCost,
        decimal interestRatePercent,
        int cycleMonths)
    {
        var cycleYears = cycleMonths <= 0 ? 0m : cycleMonths / 12m;

        // Simple interest over the cycle — this is a single bullet repayment, not an amortising
        // schedule, so there is no reducing balance to compound against.
        var interestCharge = financedInputCost * (interestRatePercent / 100m) * cycleYears;
        var amountDueAtHarvest = financedInputCost + interestCharge;

        var ownCashCosts = ownProductionCost + harvestAndLogisticsCost;
        var totalCost = financedInputCost + ownCashCosts + interestCharge;

        var grossMargin = grossRevenue - totalCost;
        var grossMarginPercent = grossRevenue == 0 ? 0m : grossMargin / grossRevenue * 100m;

        // Cash the farmer actually has available to service the debt at harvest: sale proceeds less
        // the costs they funded themselves. The financed inputs are excluded — they are the loan,
        // not a competing cash outflow.
        var cashAvailableForDebtService = grossRevenue - ownCashCosts;
        var dscr = amountDueAtHarvest == 0 ? 0m : cashAvailableForDebtService / amountDueAtHarvest;

        // Cash-flow series for the return metrics, from the FARMER's perspective:
        //   t=0        outflow of their own production costs
        //   t=cycleEnd sale proceeds, less harvest costs, less the loan repayment
        // The financed inputs never appear as a farmer outflow; they arrive as the loan and leave as
        // the repayment.
        var netAtHarvest = grossRevenue - harvestAndLogisticsCost - amountDueAtHarvest;
        var series = new[] { -ownProductionCost, netAtHarvest };

        var irr = Irr(series, cycleYears);
        var npv = Npv(series, cycleYears, hurdleRatePercent: 0m); // hurdle applied by the caller's config

        return new RhshfViabilityResult(
            GrossRevenue: Round(grossRevenue),
            FinancedInputCost: Round(financedInputCost),
            OwnCashCosts: Round(ownCashCosts),
            InterestCharge: Round(interestCharge),
            TotalCost: Round(totalCost),
            AmountDueAtHarvest: Round(amountDueAtHarvest),
            CashAvailableForDebtService: Round(cashAvailableForDebtService),
            GrossMargin: Round(grossMargin),
            GrossMarginPercent: Round(grossMarginPercent, 2),
            Dscr: Round(dscr, 4),
            NetReturnToFarmer: Round(netAtHarvest - ownProductionCost),
            Irr: irr.HasValue ? Round(irr.Value, 6) : null,
            NetPresentValue: Round(npv));
    }

    /// <summary>NPV of a dated series at a hurdle rate. Element 0 is at t=0; element 1 at t=cycleYears.</summary>
    public static decimal Npv(decimal[] series, decimal cycleYears, decimal hurdleRatePercent)
    {
        var rate = (double)(hurdleRatePercent / 100m);
        double npv = 0;
        for (var i = 0; i < series.Length; i++)
        {
            var t = i == 0 ? 0d : (double)cycleYears;
            npv += (double)series[i] / Math.Pow(1 + rate, t);
        }
        return (decimal)npv;
    }

    /// <summary>
    /// Annualised IRR by bisection on the discount rate that zeroes NPV.
    ///
    /// A real root-find, not NAMP's closed-form stand-in (annual surplus ÷ equity, minus a flat 5
    /// percentage points), which is not an internal rate of return by any definition and cannot be
    /// reconciled with the cash flows it claims to describe.
    ///
    /// Returns null when the series has no sign change (all-positive or all-negative flows), because
    /// no IRR exists — reporting a number there would be inventing one.
    /// </summary>
    public static decimal? Irr(decimal[] series, decimal cycleYears, decimal tolerance = 0.0000001m, int maxIterations = 200)
    {
        if (series.Length < 2 || cycleYears <= 0) return null;
        if (series.All(v => v >= 0) || series.All(v => v <= 0)) return null;

        double NpvAt(double rate)
        {
            double sum = 0;
            for (var i = 0; i < series.Length; i++)
            {
                var t = i == 0 ? 0d : (double)cycleYears;
                sum += (double)series[i] / Math.Pow(1 + rate, t);
            }
            return sum;
        }

        // Bracket the root. Lower bound sits just above -100% (a total loss asymptote); expand the
        // upper bound until the sign flips or we give up.
        double lo = -0.9999, hi = 1.0;
        var fLo = NpvAt(lo);
        var fHi = NpvAt(hi);

        var expansions = 0;
        while (fLo * fHi > 0 && expansions++ < 60)
        {
            hi *= 2;
            fHi = NpvAt(hi);
        }
        if (fLo * fHi > 0) return null; // no sign change found within a sane range

        for (var i = 0; i < maxIterations; i++)
        {
            var mid = (lo + hi) / 2;
            var fMid = NpvAt(mid);

            if (Math.Abs(fMid) < (double)tolerance || (hi - lo) / 2 < (double)tolerance)
                return (decimal)mid;

            if (fLo * fMid < 0) { hi = mid; fHi = fMid; }
            else { lo = mid; fLo = fMid; }
        }

        return (decimal)((lo + hi) / 2);
    }

    /// <summary>
    /// Yield per hectare at which DSCR falls to exactly 1.0, holding price and costs constant.
    /// The gap between this and the expected yield is the agronomic buffer — how much of a crop
    /// failure the facility survives.
    /// </summary>
    public static decimal? BreakEvenYieldPerHectare(
        decimal totalHectares, decimal expectedPricePerKg, decimal ownCashCosts, decimal amountDueAtHarvest)
    {
        if (totalHectares <= 0 || expectedPricePerKg <= 0) return null;
        // Solve: (hectares × yield × price) − ownCashCosts = amountDue
        var requiredRevenue = amountDueAtHarvest + ownCashCosts;
        return requiredRevenue / (totalHectares * expectedPricePerKg);
    }

    /// <summary>
    /// Farm-gate price at which DSCR falls to exactly 1.0, holding yield and costs constant.
    /// Independent of the yield sensitivity above: this is market risk, that one is production risk.
    /// </summary>
    public static decimal? BreakEvenPricePerKg(
        decimal totalHectares, decimal expectedYieldPerHectare, decimal ownCashCosts, decimal amountDueAtHarvest)
    {
        if (totalHectares <= 0 || expectedYieldPerHectare <= 0) return null;
        var requiredRevenue = amountDueAtHarvest + ownCashCosts;
        return requiredRevenue / (totalHectares * expectedYieldPerHectare);
    }

    /// <summary>Headroom as a percentage of the expected figure. Positive = buffer, negative = the
    /// plan is already under water on that dimension.</summary>
    public static decimal? HeadroomPercent(decimal expected, decimal? breakEven)
    {
        if (breakEven is null || expected <= 0) return null;
        return (expected - breakEven.Value) / expected * 100m;
    }

    private static decimal Round(decimal value, int places = 2) =>
        Math.Round(value, places, MidpointRounding.AwayFromZero);
}

/// <summary>Computed viability figures. All derived — never user-entered.</summary>
public record RhshfViabilityResult(
    decimal GrossRevenue,
    decimal FinancedInputCost,
    decimal OwnCashCosts,
    decimal InterestCharge,
    decimal TotalCost,
    decimal AmountDueAtHarvest,
    decimal CashAvailableForDebtService,
    decimal GrossMargin,
    decimal GrossMarginPercent,
    decimal Dscr,
    decimal NetReturnToFarmer,
    decimal? Irr,
    decimal NetPresentValue);
