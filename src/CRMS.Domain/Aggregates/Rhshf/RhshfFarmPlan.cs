using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// The agronomic basis for one crop in a cycle: how much land, what yield is expected from it, and
/// what the produce is expected to fetch. These three numbers are what turn an input package into a
/// repayment forecast, and nothing in RH-SHF captured any of them before — EOP lines describe the
/// inputs being financed (seed, fertiliser), not the production they are expected to generate.
///
/// Per crop rather than per case: maize and rice have materially different yields and farm-gate
/// prices, so collapsing them to a single blended figure would hide exactly the variation a credit
/// officer needs to see.
///
/// Staff-entered in CRMS today; the submit contract can carry these later without changing anything
/// here (portal-supplied values simply pre-fill the same fields).
/// </summary>
public class RhshfFarmPlan : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }
    public int CycleNumber { get; private set; }

    public string Crop { get; private set; } = string.Empty;
    public decimal Hectares { get; private set; }
    public decimal ExpectedYieldKgPerHectare { get; private set; }
    public decimal ExpectedPricePerKg { get; private set; }

    public Guid RecordedBy { get; private set; }
    public DateTime RecordedAt { get; private set; }

    /// <summary>Expected sale proceeds for this crop. Derived, never stored independently.</summary>
    public decimal ExpectedRevenue => Hectares * ExpectedYieldKgPerHectare * ExpectedPricePerKg;

    /// <summary>Total expected output in kg — used for the blended-yield figure across crops.</summary>
    public decimal ExpectedOutputKg => Hectares * ExpectedYieldKgPerHectare;

    protected RhshfFarmPlan() { }

    public static Result<RhshfFarmPlan> Create(
        Guid rhshfCreditProfileId, int cycleNumber, string crop,
        decimal hectares, decimal expectedYieldKgPerHectare, decimal expectedPricePerKg, Guid recordedBy)
    {
        if (string.IsNullOrWhiteSpace(crop))
            return Result.Failure<RhshfFarmPlan>("Crop is required.");
        if (hectares <= 0)
            return Result.Failure<RhshfFarmPlan>("Hectares must be greater than zero.");
        if (expectedYieldKgPerHectare <= 0)
            return Result.Failure<RhshfFarmPlan>("Expected yield per hectare must be greater than zero.");
        if (expectedPricePerKg <= 0)
            return Result.Failure<RhshfFarmPlan>("Expected price per kg must be greater than zero.");
        if (recordedBy == Guid.Empty)
            return Result.Failure<RhshfFarmPlan>("The recording user is required.");

        return Result.Success(new RhshfFarmPlan
        {
            RhshfCreditProfileId = rhshfCreditProfileId,
            CycleNumber = cycleNumber,
            Crop = crop.Trim(),
            Hectares = hectares,
            ExpectedYieldKgPerHectare = expectedYieldKgPerHectare,
            ExpectedPricePerKg = expectedPricePerKg,
            RecordedBy = recordedBy,
            RecordedAt = DateTime.UtcNow,
        });
    }

    public Result Update(decimal hectares, decimal expectedYieldKgPerHectare, decimal expectedPricePerKg)
    {
        if (hectares <= 0)
            return Result.Failure("Hectares must be greater than zero.");
        if (expectedYieldKgPerHectare <= 0)
            return Result.Failure("Expected yield per hectare must be greater than zero.");
        if (expectedPricePerKg <= 0)
            return Result.Failure("Expected price per kg must be greater than zero.");

        Hectares = hectares;
        ExpectedYieldKgPerHectare = expectedYieldKgPerHectare;
        ExpectedPricePerKg = expectedPricePerKg;
        return Result.Success();
    }
}
