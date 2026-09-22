using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// Admin-configurable PASS/FAIL thresholds for the viability framework.
///
/// NAMP hard-codes its equivalents as literals inside Detail.razor (15% hurdle, 5-year window,
/// equity 25%/15%, tenor 36/60, fallback rate 9%) — while the on-screen footnote claims equity is
/// "locked to the NAMP loan product", which it is not. Credit policy parameters belong in data the
/// bank can change, not in a view file.
///
/// Single active row; edits create a new version so a historical appraisal can always be read back
/// against the thresholds that were in force when it was taken.
/// </summary>
public class RhshfAppraisalThresholds : AggregateRoot
{
    /// <summary>Minimum debt service coverage ratio. Cash available at harvest ÷ amount due.</summary>
    public decimal MinDscr { get; private set; }

    /// <summary>Minimum enterprise gross margin, as a percentage of gross revenue.</summary>
    public decimal MinGrossMarginPercent { get; private set; }

    /// <summary>Discount/hurdle rate for NPV, and the bar the IRR must clear.</summary>
    public decimal HurdleRatePercent { get; private set; }

    /// <summary>Minimum buffer between expected and break-even yield — the agronomic risk margin.</summary>
    public decimal MinYieldHeadroomPercent { get; private set; }

    /// <summary>Minimum buffer between expected and break-even farm-gate price — the market risk margin.</summary>
    public decimal MinPriceHeadroomPercent { get; private set; }

    public bool IsActive { get; private set; }

    protected RhshfAppraisalThresholds() { }

    public static Result<RhshfAppraisalThresholds> Create(
        decimal minDscr, decimal minGrossMarginPercent, decimal hurdleRatePercent,
        decimal minYieldHeadroomPercent, decimal minPriceHeadroomPercent)
    {
        if (minDscr <= 0)
            return Result.Failure<RhshfAppraisalThresholds>("Minimum DSCR must be greater than zero.");
        if (hurdleRatePercent < 0)
            return Result.Failure<RhshfAppraisalThresholds>("Hurdle rate cannot be negative.");

        return Result.Success(new RhshfAppraisalThresholds
        {
            MinDscr = minDscr,
            MinGrossMarginPercent = minGrossMarginPercent,
            HurdleRatePercent = hurdleRatePercent,
            MinYieldHeadroomPercent = minYieldHeadroomPercent,
            MinPriceHeadroomPercent = minPriceHeadroomPercent,
            IsActive = true,
        });
    }

    public Result Update(
        decimal minDscr, decimal minGrossMarginPercent, decimal hurdleRatePercent,
        decimal minYieldHeadroomPercent, decimal minPriceHeadroomPercent)
    {
        if (minDscr <= 0)
            return Result.Failure("Minimum DSCR must be greater than zero.");
        if (hurdleRatePercent < 0)
            return Result.Failure("Hurdle rate cannot be negative.");

        MinDscr = minDscr;
        MinGrossMarginPercent = minGrossMarginPercent;
        HurdleRatePercent = hurdleRatePercent;
        MinYieldHeadroomPercent = minYieldHeadroomPercent;
        MinPriceHeadroomPercent = minPriceHeadroomPercent;
        return Result.Success();
    }

    public void Deactivate() => IsActive = false;
}
