using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// The Credit Officer's structured financial appraisal for one cycle. Replaces the free-text note
/// that was previously the entire appraisal record (RhshfAppraisal carried an outcome enum and a
/// nullable string — nothing else).
///
/// Two deliberate departures from NampFinancialAppraisalReport:
///
/// 1. Keyed per CYCLE, not one-per-case. NAMP's unique index on NampApplicationId means a case
///    returned to the applicant and re-appraised silently overwrites its prior appraisal. RH-SHF
///    cases round-trip to the FAC by design, so each cycle keeps its own record.
///
/// 2. Computed figures are derived here from the captured inputs, never accepted from the caller.
///    NAMP's handler stores whatever the browser posts, so a tampered or stale client can persist
///    metrics that don't follow from the inputs beside them.
/// </summary>
public class RhshfFinancialAppraisalReport : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }
    public int CycleNumber { get; private set; }
    public Guid PreparedByUserId { get; private set; }
    public DateTime SavedAt { get; private set; }

    // ── Officer-captured assumptions ──────────────────────────────────────
    public decimal OwnProductionCost { get; private set; }
    public decimal HarvestAndLogisticsCost { get; private set; }
    public int CycleMonths { get; private set; }
    public decimal InterestRatePercent { get; private set; }
    /// <summary>Basis for the yield/price assumptions — off-take agreement, prior season, extension advice.</summary>
    public string? AssumptionBasisNote { get; private set; }

    // ── Computed (server-side, from the captured inputs + farm plans) ─────
    public decimal GrossRevenue { get; private set; }
    public decimal FinancedInputCost { get; private set; }
    public decimal OwnCashCosts { get; private set; }
    public decimal InterestCharge { get; private set; }
    public decimal TotalCost { get; private set; }
    public decimal AmountDueAtHarvest { get; private set; }
    public decimal CashAvailableForDebtService { get; private set; }
    public decimal GrossMargin { get; private set; }
    public decimal GrossMarginPercent { get; private set; }
    public decimal Dscr { get; private set; }
    public decimal NetReturnToFarmer { get; private set; }
    public decimal? Irr { get; private set; }
    public decimal NetPresentValue { get; private set; }

    public decimal TotalHectares { get; private set; }
    public decimal BlendedYieldKgPerHectare { get; private set; }
    public decimal BlendedPricePerKg { get; private set; }
    public decimal? BreakEvenYieldKgPerHectare { get; private set; }
    public decimal? BreakEvenPricePerKg { get; private set; }
    public decimal? YieldHeadroomPercent { get; private set; }
    public decimal? PriceHeadroomPercent { get; private set; }

    // ── Thresholds in force when this appraisal was taken ─────────────────
    // Snapshotted so a historical report can be read back against the policy of its own time.
    public decimal ThresholdMinDscr { get; private set; }
    public decimal ThresholdMinGrossMarginPercent { get; private set; }
    public decimal ThresholdHurdleRatePercent { get; private set; }
    public decimal ThresholdMinYieldHeadroomPercent { get; private set; }
    public decimal ThresholdMinPriceHeadroomPercent { get; private set; }

    // ── Gate outcomes ─────────────────────────────────────────────────────
    public bool DscrPass { get; private set; }
    public bool GrossMarginPass { get; private set; }
    public bool IrrPass { get; private set; }
    public bool YieldHeadroomPass { get; private set; }
    public bool PriceHeadroomPass { get; private set; }
    public bool AllGatesPass => DscrPass && GrossMarginPass && IrrPass && YieldHeadroomPass && PriceHeadroomPass;
    public int GatesPassed => (DscrPass ? 1 : 0) + (GrossMarginPass ? 1 : 0) + (IrrPass ? 1 : 0)
        + (YieldHeadroomPass ? 1 : 0) + (PriceHeadroomPass ? 1 : 0);

    // ── Officer conclusion ────────────────────────────────────────────────
    public RhshfRepaymentCapacityRating RepaymentCapacityRating { get; private set; }
    public RhshfCreditRecommendation CreditOfficerRecommendation { get; private set; }
    public string? SummaryNotes { get; private set; }
    /// <summary>Why the officer recommended Pass despite one or more failing gates. Required in that
    /// case — an override must be justified in writing, not just clicked.</summary>
    public string? OverrideJustification { get; private set; }

    protected RhshfFinancialAppraisalReport() { }

    public static Result<RhshfFinancialAppraisalReport> Create(
        Guid rhshfCreditProfileId, int cycleNumber, Guid preparedByUserId,
        decimal ownProductionCost, decimal harvestAndLogisticsCost, int cycleMonths, decimal interestRatePercent,
        string? assumptionBasisNote,
        IReadOnlyList<RhshfFarmPlan> farmPlans, decimal financedInputCost,
        RhshfAppraisalThresholds thresholds,
        RhshfCreditRecommendation recommendation, string? summaryNotes, string? overrideJustification)
    {
        var report = new RhshfFinancialAppraisalReport
        {
            RhshfCreditProfileId = rhshfCreditProfileId,
            CycleNumber = cycleNumber,
            PreparedByUserId = preparedByUserId,
        };

        var result = report.Populate(
            preparedByUserId, ownProductionCost, harvestAndLogisticsCost, cycleMonths, interestRatePercent,
            assumptionBasisNote, farmPlans, financedInputCost, thresholds,
            recommendation, summaryNotes, overrideJustification);

        return result.IsFailure
            ? Result.Failure<RhshfFinancialAppraisalReport>(result.Error)
            : Result.Success(report);
    }

    public Result Update(
        Guid preparedByUserId,
        decimal ownProductionCost, decimal harvestAndLogisticsCost, int cycleMonths, decimal interestRatePercent,
        string? assumptionBasisNote,
        IReadOnlyList<RhshfFarmPlan> farmPlans, decimal financedInputCost,
        RhshfAppraisalThresholds thresholds,
        RhshfCreditRecommendation recommendation, string? summaryNotes, string? overrideJustification)
        => Populate(
            preparedByUserId, ownProductionCost, harvestAndLogisticsCost, cycleMonths, interestRatePercent,
            assumptionBasisNote, farmPlans, financedInputCost, thresholds,
            recommendation, summaryNotes, overrideJustification);

    private Result Populate(
        Guid preparedByUserId,
        decimal ownProductionCost, decimal harvestAndLogisticsCost, int cycleMonths, decimal interestRatePercent,
        string? assumptionBasisNote,
        IReadOnlyList<RhshfFarmPlan> farmPlans, decimal financedInputCost,
        RhshfAppraisalThresholds thresholds,
        RhshfCreditRecommendation recommendation, string? summaryNotes, string? overrideJustification)
    {
        if (preparedByUserId == Guid.Empty)
            return Result.Failure("The preparing officer is required.");
        if (farmPlans.Count == 0)
            return Result.Failure("At least one farm plan (crop, hectares, yield, price) is required before appraising.");
        if (cycleMonths <= 0)
            return Result.Failure("The production cycle length in months must be greater than zero.");
        if (ownProductionCost < 0 || harvestAndLogisticsCost < 0)
            return Result.Failure("Costs cannot be negative.");
        if (interestRatePercent < 0)
            return Result.Failure("Interest rate cannot be negative.");

        var grossRevenue = farmPlans.Sum(p => p.ExpectedRevenue);
        var totalHectares = farmPlans.Sum(p => p.Hectares);
        var totalOutputKg = farmPlans.Sum(p => p.ExpectedOutputKg);

        var viability = RhshfViabilityCalculator.Compute(
            grossRevenue, financedInputCost, ownProductionCost, harvestAndLogisticsCost,
            interestRatePercent, cycleMonths);

        // Blended figures are what the break-even sensitivities are measured against when a case
        // carries multiple crops.
        var blendedYield = totalHectares == 0 ? 0m : totalOutputKg / totalHectares;
        var blendedPrice = totalOutputKg == 0 ? 0m : grossRevenue / totalOutputKg;

        var breakEvenYield = RhshfViabilityCalculator.BreakEvenYieldPerHectare(
            totalHectares, blendedPrice, viability.OwnCashCosts, viability.AmountDueAtHarvest);
        var breakEvenPrice = RhshfViabilityCalculator.BreakEvenPricePerKg(
            totalHectares, blendedYield, viability.OwnCashCosts, viability.AmountDueAtHarvest);

        var yieldHeadroom = RhshfViabilityCalculator.HeadroomPercent(blendedYield, breakEvenYield);
        var priceHeadroom = RhshfViabilityCalculator.HeadroomPercent(blendedPrice, breakEvenPrice);

        var npvAtHurdle = RhshfViabilityCalculator.Npv(
            [-ownProductionCost, grossRevenue - harvestAndLogisticsCost - viability.AmountDueAtHarvest],
            cycleMonths / 12m, thresholds.HurdleRatePercent);

        // ── Gates ─────────────────────────────────────────────────────────
        var dscrPass = viability.Dscr >= thresholds.MinDscr;
        var marginPass = viability.GrossMarginPercent >= thresholds.MinGrossMarginPercent;
        // A null IRR (no sign change in the series) cannot clear the hurdle — it fails rather than
        // being quietly treated as a pass.
        var irrPass = viability.Irr.HasValue && viability.Irr.Value * 100m >= thresholds.HurdleRatePercent;
        var yieldPass = yieldHeadroom.HasValue && yieldHeadroom.Value >= thresholds.MinYieldHeadroomPercent;
        var pricePass = priceHeadroom.HasValue && priceHeadroom.Value >= thresholds.MinPriceHeadroomPercent;

        var allPass = dscrPass && marginPass && irrPass && yieldPass && pricePass;

        // Thresholds are enforced, not decorative. NAMP lets an officer record Pass with every metric
        // failing; here an override is permitted but must carry a written justification.
        if (recommendation == RhshfCreditRecommendation.Pass && !allPass
            && string.IsNullOrWhiteSpace(overrideJustification))
        {
            return Result.Failure(
                "One or more viability gates failed. A Pass recommendation requires a written override justification.");
        }

        PreparedByUserId = preparedByUserId;
        SavedAt = DateTime.UtcNow;

        OwnProductionCost = ownProductionCost;
        HarvestAndLogisticsCost = harvestAndLogisticsCost;
        CycleMonths = cycleMonths;
        InterestRatePercent = interestRatePercent;
        AssumptionBasisNote = assumptionBasisNote;

        GrossRevenue = viability.GrossRevenue;
        FinancedInputCost = viability.FinancedInputCost;
        OwnCashCosts = viability.OwnCashCosts;
        InterestCharge = viability.InterestCharge;
        TotalCost = viability.TotalCost;
        AmountDueAtHarvest = viability.AmountDueAtHarvest;
        CashAvailableForDebtService = viability.CashAvailableForDebtService;
        GrossMargin = viability.GrossMargin;
        GrossMarginPercent = viability.GrossMarginPercent;
        Dscr = viability.Dscr;
        NetReturnToFarmer = viability.NetReturnToFarmer;
        Irr = viability.Irr;
        NetPresentValue = Math.Round(npvAtHurdle, 2, MidpointRounding.AwayFromZero);

        TotalHectares = totalHectares;
        BlendedYieldKgPerHectare = Math.Round(blendedYield, 2, MidpointRounding.AwayFromZero);
        BlendedPricePerKg = Math.Round(blendedPrice, 2, MidpointRounding.AwayFromZero);
        BreakEvenYieldKgPerHectare = breakEvenYield.HasValue ? Math.Round(breakEvenYield.Value, 2, MidpointRounding.AwayFromZero) : null;
        BreakEvenPricePerKg = breakEvenPrice.HasValue ? Math.Round(breakEvenPrice.Value, 2, MidpointRounding.AwayFromZero) : null;
        YieldHeadroomPercent = yieldHeadroom.HasValue ? Math.Round(yieldHeadroom.Value, 2, MidpointRounding.AwayFromZero) : null;
        PriceHeadroomPercent = priceHeadroom.HasValue ? Math.Round(priceHeadroom.Value, 2, MidpointRounding.AwayFromZero) : null;

        ThresholdMinDscr = thresholds.MinDscr;
        ThresholdMinGrossMarginPercent = thresholds.MinGrossMarginPercent;
        ThresholdHurdleRatePercent = thresholds.HurdleRatePercent;
        ThresholdMinYieldHeadroomPercent = thresholds.MinYieldHeadroomPercent;
        ThresholdMinPriceHeadroomPercent = thresholds.MinPriceHeadroomPercent;

        DscrPass = dscrPass;
        GrossMarginPass = marginPass;
        IrrPass = irrPass;
        YieldHeadroomPass = yieldPass;
        PriceHeadroomPass = pricePass;

        RepaymentCapacityRating = RateCapacity(viability.Dscr, thresholds.MinDscr);
        CreditOfficerRecommendation = recommendation;
        SummaryNotes = summaryNotes;
        OverrideJustification = allPass ? null : overrideJustification;

        return Result.Success();
    }

    /// <summary>Bands are relative to the configured minimum rather than absolute, so tightening
    /// policy moves the ratings with it instead of leaving them anchored to stale numbers.</summary>
    private static RhshfRepaymentCapacityRating RateCapacity(decimal dscr, decimal minDscr) => dscr switch
    {
        _ when dscr >= minDscr * 1.5m => RhshfRepaymentCapacityRating.Strong,
        _ when dscr >= minDscr => RhshfRepaymentCapacityRating.Adequate,
        _ when dscr >= 1.0m => RhshfRepaymentCapacityRating.Marginal,
        _ => RhshfRepaymentCapacityRating.Insufficient,
    };
}
