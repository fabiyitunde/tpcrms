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

    // ── Pack and price detail (CRMSQA#3 item 2) ───────────────────────────────────────────────────
    // Optional: sent by the portal from its Slice B onwards, null on every earlier case. Descriptive
    // only — none of these is used to compute or check an amount; Amount stays the figure as sent.

    /// <summary>The unit the per-farmer quantity is counted in — "Kg".</summary>
    public string? BaseUnit { get; private set; }
    /// <summary>How much one supplier pack holds, in <see cref="BaseUnit"/> — 50 for a 50 Kg bag.</summary>
    public decimal? PackSize { get; private set; }
    /// <summary>What a pack is called — "Bag".</summary>
    public string? PackUnit { get; private set; }
    /// <summary>Whole packs bought for the EOP.</summary>
    public decimal? PackCount { get; private set; }
    public decimal? PricePerPack { get; private set; }
    /// <summary>Base units actually bought — the requirement rounded up to whole packs.</summary>
    public decimal? ProcurementQuantity { get; private set; }
    /// <summary>Base units bought above the requirement because packs are whole.</summary>
    public decimal? PackagingExcessQuantity { get; private set; }
    /// <summary>The supplier price list entry the line was priced from, so a later price change can
    /// be told apart from the price this offer used.</summary>
    public string? PriceVersionId { get; private set; }
    public string? SupplierProductId { get; private set; }

    // ── Service provider (CRMSQA#3 item 1) ────────────────────────────────────────────────────────
    // The provider BOA approved for this EOP's service line. Services are not supplied by an input
    // supplier, so until the portal sent this a financed service printed as "To be confirmed".

    public string? ProviderId { get; private set; }
    public string? ProviderName { get; private set; }
    public string? ProviderServiceType { get; private set; }
    public decimal? ProviderRate { get; private set; }
    public string? ProviderRateBasis { get; private set; }
    public string? ProviderRateVersion { get; private set; }
    public decimal? ProviderBillableQuantity { get; private set; }
    /// <summary>The portal's assignment of this provider to this line, for tracing the approval.</summary>
    public string? ProviderAssignmentId { get; private set; }

    /// <summary>Who delivers the line: the input supplier, or for a service the approved provider.</summary>
    public string? ResponsibleParty => Supplier ?? ProviderName;

    /// <summary>"6 Bags of 50 Kg at NGN 22,400.00 each" — what was actually bought, or null where the
    /// portal sent no pack detail.</summary>
    public string? PackDisplay(string currency)
    {
        if (PackCount is not { } count || PackSize is not { } size)
            return null;
        var unit = PackUnit ?? "pack";
        var plural = count == 1m || unit.EndsWith('s') ? unit : unit + "s";
        var of = BaseUnit is null ? $"{size:0.###}" : $"{size:0.###} {BaseUnit}";
        var price = PricePerPack is { } p ? $" at {currency} {p:N2} each" : "";
        return $"{count:0.###} {plural} of {of}{price}";
    }

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
        bool inLoan, RhshfFinancingLineDetail? detail = null)
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

        if (detail is null)
            return;
        BaseUnit = Text(detail.BaseUnit);
        PackSize = detail.PackSize;
        PackUnit = Text(detail.PackUnit);
        PackCount = detail.PackCount;
        PricePerPack = detail.PricePerPack;
        ProcurementQuantity = detail.ProcurementQuantity;
        PackagingExcessQuantity = detail.PackagingExcessQuantity;
        PriceVersionId = Text(detail.PriceVersionId);
        SupplierProductId = Text(detail.SupplierProductId);
        ProviderId = Text(detail.ProviderId);
        ProviderName = Text(detail.ProviderName);
        ProviderServiceType = Text(detail.ProviderServiceType);
        ProviderRate = detail.ProviderRate;
        ProviderRateBasis = Text(detail.ProviderRateBasis);
        ProviderRateVersion = Text(detail.ProviderRateVersion);
        ProviderBillableQuantity = detail.ProviderBillableQuantity;
        ProviderAssignmentId = Text(detail.ProviderAssignmentId);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Optional detail is capped at its column, so an unusually long value from the portal is
    /// shortened rather than failing the whole submission.</summary>
    private static string? Text(string? value) => Clean(value) is { } v && v.Length > 200 ? v[..200] : Clean(value);
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

    // ── EOP snapshot reference (CRMSQA#3 item 3) ──────────────────────────────────────────────────
    // Optional, from the portal's Slice B. Lets the offer, the portal's EOP and BOA's supplier release
    // letter name the same version of the same plan.

    /// <summary>The portal's readable EOP reference, the same one its release letters print.</summary>
    public string? EopReference { get; private set; }
    public DateTime? EopApprovedAt { get; private set; }
    /// <summary>Which approved revision of the EOP this is, where the portal numbers them.</summary>
    public int? EopRevision { get; private set; }
    /// <summary>The portal's fingerprint of the EOP's lines and charges as sent.</summary>
    public string? SnapshotHash { get; private set; }

    /// <summary>"EOP-1A2B3C4D" when the portal sent a reference, otherwise the short id the letter has
    /// always printed.</summary>
    public string? DisplayReference =>
        EopReference ?? (EopId is { } id ? $"EOP {id.ToString()[..8].ToUpperInvariant()}" : null);

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
        EopReference = Cap(input.EopReference);
        EopApprovedAt = input.EopApprovedAt;
        EopRevision = input.EopRevision;
        SnapshotHash = Cap(input.SnapshotHash);
    }

    /// <summary>Capped at the column, so an unusually long value shortens rather than failing the
    /// submission.</summary>
    private static string? Cap(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        return v.Length > 200 ? v[..200] : v;
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
    decimal LoanAmount, string? EopReference = null, DateTime? EopApprovedAt = null, int? EopRevision = null,
    string? SnapshotHash = null);

public record RhshfFinancingLineInput(
    Guid? EopId, string? Crop, string Item, string? Category, string? SpecificInput, string? Supplier,
    decimal? QuantityPerFarmer, string? Unit, decimal? UnitPrice, string? Basis, int? Farmers,
    decimal Amount, decimal PackagingExcess, bool InLoan, RhshfFinancingLineDetail? Detail = null);

/// <summary>The optional pack, price-version and service-provider detail of one line (CRMSQA#3
/// items 1 and 2). Every field may be absent.</summary>
public record RhshfFinancingLineDetail(
    string? BaseUnit = null, decimal? PackSize = null, string? PackUnit = null, decimal? PackCount = null,
    decimal? PricePerPack = null, decimal? ProcurementQuantity = null, decimal? PackagingExcessQuantity = null,
    string? PriceVersionId = null, string? SupplierProductId = null,
    string? ProviderId = null, string? ProviderName = null, string? ProviderServiceType = null,
    decimal? ProviderRate = null, string? ProviderRateBasis = null, string? ProviderRateVersion = null,
    decimal? ProviderBillableQuantity = null, string? ProviderAssignmentId = null);

public record RhshfFinancingChargeInput(
    string Code, string Label, decimal? Rate, string? RateBasis, decimal Amount, bool InLoan);
