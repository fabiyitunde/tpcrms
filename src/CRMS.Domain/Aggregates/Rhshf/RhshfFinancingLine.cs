using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// One line of the approved EOP at input-item grain, as the portal sent it — the rows the offer
/// letter's implementation plan is printed from.
/// <para>
/// Distinct from <see cref="RhshfEopLine"/>, which stays what it always was: a crop-level summary
/// the portal sends with zero quantities. These are the real financing rows, one per input item per
/// EOP, carrying the supplier and the unit the quantity is counted in.
/// </para>
/// <para>
/// Every amount is stored exactly as received. The portal rounds supplier-pack unit prices down to
/// the kobo, so <see cref="Amount"/> is NOT <see cref="QuantityPerFarmer"/> × <see cref="UnitPrice"/>
/// × <see cref="Farmers"/> and must never be recomputed from them — the sent figure is the one the
/// borrower signs against and the one the totals reconcile to.
/// </para>
/// </summary>
public class RhshfFinancingLine : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }

    /// <summary>Which approved EOP this line belongs to. A case can carry several.</summary>
    public Guid? EopId { get; private set; }

    public string? Crop { get; private set; }

    /// <summary>The programme input — "NPK Fertilizer", "Extension Services".</summary>
    public string Item { get; private set; } = string.Empty;

    public string? Category { get; private set; }

    /// <summary>The product actually chosen — "NPK 20:10:10".</summary>
    public string? SpecificInput { get; private set; }

    /// <summary>Null for service lines and for lines with no supplier assigned yet.</summary>
    public string? Supplier { get; private set; }

    public decimal? QuantityPerFarmer { get; private set; }

    /// <summary>"Kg", "Litre", "Per Farmer", "Per Hectare" — printed with the quantity, never
    /// assumed.</summary>
    public string? Unit { get; private set; }

    public decimal? UnitPrice { get; private set; }

    /// <summary>"perFarmer" or "wholeEop". A wholeEop line is a service priced for the whole EOP and
    /// is not scaled by the farmer count, which is why the two cannot be displayed identically.</summary>
    public string? Basis { get; private set; }

    public int? Farmers { get; private set; }

    /// <summary>The line's total for the whole EOP.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Cost of whole supplier packs above the per-farmer requirement. Usually zero.</summary>
    public decimal PackagingExcess { get; private set; }

    /// <summary>Whether this line forms part of the financed loan amount. Everything is financed
    /// today, but the flag is what decides it — not an assumption that approved equals financed.</summary>
    public bool InLoan { get; private set; }

    /// <summary>What this line is, at its most specific: the chosen product where one was recorded,
    /// otherwise the programme input.</summary>
    public string Description =>
        string.IsNullOrWhiteSpace(SpecificInput) ? Item : SpecificInput.Trim();

    /// <summary>Quantity with its unit, or null where the portal sent neither — a service line
    /// priced for the whole EOP has no meaningful per-farmer quantity to print.</summary>
    public string? QuantityDisplay =>
        QuantityPerFarmer is null || string.IsNullOrWhiteSpace(Unit)
            ? null
            : $"{QuantityPerFarmer:N0} {Unit.Trim()}";

    protected RhshfFinancingLine() { }

    public RhshfFinancingLine(
        Guid rhshfCreditProfileId, Guid? eopId, string? crop, string item, string? category,
        string? specificInput, string? supplier, decimal? quantityPerFarmer, string? unit,
        decimal? unitPrice, string? basis, int? farmers, decimal amount, decimal packagingExcess,
        bool inLoan)
    {
        RhshfCreditProfileId = rhshfCreditProfileId;
        EopId = eopId;
        Crop = Clean(crop);
        Item = string.IsNullOrWhiteSpace(item) ? "(Unnamed input)" : item.Trim();
        Category = Clean(category);
        SpecificInput = Clean(specificInput);
        Supplier = Clean(supplier);
        QuantityPerFarmer = quantityPerFarmer;
        Unit = Clean(unit);
        UnitPrice = unitPrice;
        Basis = Clean(basis);
        Farmers = farmers;
        Amount = amount;
        PackagingExcess = packagingExcess;
        InLoan = inLoan;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// One programme charge financed inside the loan, as the portal computed it. Stored with the
/// portal's own label and rate wording so the offer letter states the charge the way the programme
/// states it, rather than CRMS inventing a description.
/// </summary>
public class RhshfFinancingCharge : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }

    /// <summary>"insurance", "workingCapital" or "techServicesFee".</summary>
    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    /// <summary>The percentage, or naira per farmer for the technology fee. Null when the case's
    /// EOPs used different rates — the letter then prints the amount alone rather than implying a
    /// single rate applied across the case.</summary>
    public decimal? Rate { get; private set; }

    /// <summary>The portal's plain-language description of what the rate applies to.</summary>
    public string? RateBasis { get; private set; }

    public decimal Amount { get; private set; }

    public bool InLoan { get; private set; }

    protected RhshfFinancingCharge() { }

    public RhshfFinancingCharge(
        Guid rhshfCreditProfileId, string code, string label, decimal? rate, string? rateBasis,
        decimal amount, bool inLoan)
    {
        RhshfCreditProfileId = rhshfCreditProfileId;
        Code = string.IsNullOrWhiteSpace(code) ? "unknown" : code.Trim();
        Label = string.IsNullOrWhiteSpace(label) ? Code : label.Trim();
        Rate = rate;
        RateBasis = string.IsNullOrWhiteSpace(rateBasis) ? null : rateBasis.Trim();
        Amount = amount;
        InLoan = inLoan;
    }
}

/// <summary>
/// One approved EOP within the case, as the portal summarised it: its farmers and what it costs,
/// charge by charge. A case can carry several, and each needs its own schedule on the offer letter.
/// <para>
/// <see cref="Farmers"/> is the EOP's own target — the farmers this facility actually finances. It
/// is not the profile's <c>FarmerCount</c>, which the portal sends as everyone registered under the
/// FAC (220 for Grandbai, against 30 financed). Amounts are stored exactly as sent, like the lines.
/// </para>
/// </summary>
public class RhshfFinancingEop : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }
    public Guid? EopId { get; private set; }
    public string? Crop { get; private set; }
    public string? State { get; private set; }
    public int? Farmers { get; private set; }
    public decimal Inputs { get; private set; }
    public decimal PackagingExcess { get; private set; }
    public decimal Insurance { get; private set; }
    public decimal WorkingCapital { get; private set; }
    public decimal TechServicesFee { get; private set; }
    public decimal ApprovedTotal { get; private set; }
    public decimal LoanAmount { get; private set; }

    protected RhshfFinancingEop() { }

    public RhshfFinancingEop(Guid rhshfCreditProfileId, RhshfFinancingEopInput input)
    {
        RhshfCreditProfileId = rhshfCreditProfileId;
        EopId = input.EopId;
        Crop = string.IsNullOrWhiteSpace(input.Crop) ? null : input.Crop.Trim();
        State = string.IsNullOrWhiteSpace(input.State) ? null : input.State.Trim();
        Farmers = input.Farmers;
        Inputs = input.Inputs;
        PackagingExcess = input.PackagingExcess;
        Insurance = input.Insurance;
        WorkingCapital = input.WorkingCapital;
        TechServicesFee = input.TechServicesFee;
        ApprovedTotal = input.ApprovedTotal;
        LoanAmount = input.LoanAmount;
    }
}

/// <summary>The itemised financing breakdown as it arrives from the portal, for
/// RhshfCreditProfile.Create.</summary>
public record RhshfFinancingInput(
    IReadOnlyList<RhshfFinancingLineInput> InputLines,
    IReadOnlyList<RhshfFinancingChargeInput> Charges,
    decimal? InputsTotal,
    decimal? PackagingExcessTotal,
    decimal? ChargesTotal,
    decimal? ApprovedEopTotal,
    decimal? LoanAmount,
    decimal? ExcludedFromLoan,
    IReadOnlyList<RhshfFinancingEopInput>? Eops = null);

public record RhshfFinancingEopInput(
    Guid? EopId, string? Crop, string? State, int? Farmers, decimal Inputs, decimal PackagingExcess,
    decimal Insurance, decimal WorkingCapital, decimal TechServicesFee, decimal ApprovedTotal,
    decimal LoanAmount);

public record RhshfFinancingLineInput(
    Guid? EopId, string? Crop, string Item, string? Category, string? SpecificInput, string? Supplier,
    decimal? QuantityPerFarmer, string? Unit, decimal? UnitPrice, string? Basis, int? Farmers,
    decimal Amount, decimal PackagingExcess, bool InLoan);

public record RhshfFinancingChargeInput(
    string Code, string Label, decimal? Rate, string? RateBasis, decimal Amount, bool InLoan);
