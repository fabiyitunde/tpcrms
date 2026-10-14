using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// One commodity line from the FAC's consolidated EOP (Estimate of Production). Context-only —
/// not independently workflow-driven. Own entity, independent of any NAMP equipment line item.
/// </summary>
public class RhshfEopLine : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }
    public string Commodity { get; private set; } = string.Empty;

    /// <summary>
    /// The quantity on this line, expressed in <see cref="UnitOfMeasurement"/>.
    /// <para>
    /// The name is a leftover from when every line was assumed to be in kilogrammes. It is not: the
    /// portal sends a unit per line, and a line can be counted in items (450 bags of fertiliser, 450
    /// extension visits) rather than weighed. Renaming the column is not an additive migration, so
    /// the name stays and the unit sits beside it — read the two together, never this alone.
    /// </para>
    /// </summary>
    public decimal QuantityKg { get; private set; }

    /// <summary>Price per unit of <see cref="UnitOfMeasurement"/>. Same naming caveat as
    /// <see cref="QuantityKg"/>.</summary>
    public decimal UnitPricePerKg { get; private set; }

    public decimal LineValue { get; private set; }

    /// <summary>
    /// The supplier or input producer assigned to this line on the portal ("Golden Fertilizer
    /// Limited"). Null on lines submitted before the portal began sending it — the offer letter's
    /// implementation plan then has no supplier to name for that line.
    /// </summary>
    public string? Supplier { get; private set; }

    /// <summary>The specific product under the commodity — commodity "NPK Fertilizer", product
    /// "NPK 20:10:10". Null where the portal sends only the commodity.</summary>
    public string? Product { get; private set; }

    /// <summary>Unit the quantity is counted in, as the portal states it ("Kg", "Bags", "Items").
    /// Null on lines predating it, where kilogrammes was the standing assumption.</summary>
    public string? UnitOfMeasurement { get; private set; }

    /// <summary>Quantity with its unit, for display: "3,600 Bags", or "3,600 kg" when the line
    /// predates the unit being sent. Never print the bare number — the reader cannot tell 450
    /// kilogrammes from 450 bags.</summary>
    public string QuantityDisplay =>
        $"{QuantityKg:N0} {(string.IsNullOrWhiteSpace(UnitOfMeasurement) ? "kg" : UnitOfMeasurement.Trim())}";

    /// <summary>What this line is, at its most specific: the product where one was sent, otherwise
    /// the commodity.</summary>
    public string ItemDescription =>
        string.IsNullOrWhiteSpace(Product) ? Commodity : Product.Trim();

    protected RhshfEopLine() { }

    public RhshfEopLine(
        Guid rhshfCreditProfileId, string commodity, decimal quantityKg, decimal unitPricePerKg,
        decimal lineValue, string? supplier = null, string? product = null, string? unitOfMeasurement = null)
    {
        RhshfCreditProfileId = rhshfCreditProfileId;
        Commodity = commodity;
        QuantityKg = quantityKg;
        UnitPricePerKg = unitPricePerKg;
        LineValue = lineValue;
        Supplier = Clean(supplier);
        Product = Clean(product);
        UnitOfMeasurement = Clean(unitOfMeasurement);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// One EOP line as it arrives from the portal, for RhshfCreditProfile.Create.
/// <para>
/// A record rather than the tuple this replaced: with supplier, product and unit added, a positional
/// 7-tuple of four strings and three decimals is a silent mis-ordering waiting to happen — swap
/// supplier and product and nothing complains until a letter names the wrong thing.
/// </para>
/// </summary>
public record RhshfEopLineInput(
    string Commodity,
    decimal QuantityKg,
    decimal UnitPricePerKg,
    decimal LineValue,
    string? Supplier = null,
    string? Product = null,
    string? UnitOfMeasurement = null);

/// <summary>Programme charges the portal computed and included in the EOP total, for
/// RhshfCreditProfile.Create. Not the appraisal or monitoring fee — those are core banking product
/// charges, deducted at booking rather than financed.</summary>
public record RhshfEopChargesInput(
    decimal? Insurance = null,
    decimal? WorkingCapital = null,
    decimal? TechnologyServicesFee = null);
