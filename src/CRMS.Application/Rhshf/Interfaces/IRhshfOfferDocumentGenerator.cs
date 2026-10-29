namespace CRMS.Application.Rhshf.Interfaces;

/// <summary>
/// The single post-approval document: offer terms, facility conditions, the implementation plan and
/// the acceptance pages in one place.
/// <para>
/// Replaces the pair that used to ship together — an offer letter and a separate Key Facts
/// Statement, both of which the FAC had to sign and return. The programme team consolidated them, so
/// the key facts are now a panel in this document rather than a second file. Offers already issued
/// keep their two documents and their two-signature requirement; that falls out of
/// RhshfOffer.HasRequiredSignedDocuments, which only asks for a signed KFS when one was issued.
/// </para>
/// <para>
/// Modelled on the bank's approved template (Sagad Farms, V6). Content follows that document;
/// layout is CRMS's own.
/// </para>
/// </summary>
public interface IRhshfOfferDocumentGenerator
{
    Task<byte[]> GenerateAsync(RhshfOfferDocumentData data, CancellationToken ct = default);
}

public record RhshfOfferDocumentData(
    string Reference,
    string CompanyName,
    string RcNumber,
    string ProgrammeName,
    string SessionName,
    decimal ApprovedAmount,
    string Currency,
    DateTime GeneratedDate,
    string BankName,

    /// <summary>Addressee lines beneath "The Managing Director" — the CAC registered address where
    /// one was fetched, otherwise the LGA and state from the portal. Empty when neither is known, in
    /// which case the block is omitted rather than printing a stub address.</summary>
    IReadOnlyList<string> AddressLines,

    /// <summary>Facility terms from the credit appraisal. Null where no appraisal was recorded — the
    /// document then states the term is per the approved credit terms rather than inventing one.</summary>
    decimal? InterestRatePercent,
    int? TenorMonths,

    decimal? AmountDueAtHarvest,
    decimal? InterestCharge,
    int? FarmerCount,

    /// <summary>The implementation plan: one row per financed input, then one per programme charge,
    /// footing to <see cref="ApprovedAmount"/>.</summary>
    IReadOnlyList<RhshfOfferPlanRow> PlanRows,

    /// <summary>True when the plan was built from the portal's itemised breakdown. False on a case
    /// predating it, where the rows are the crop-level summary plus a single balancing row — worth
    /// saying, because the suppliers cannot be named on those.</summary>
    bool PlanIsItemised,

    /// <summary>Fees configured on the core banking loan product, which it deducts at booking. Read
    /// from the product rather than held here, so the letter cannot state one fee while the booking
    /// charges another. Empty when the product defines none.</summary>
    IReadOnlyList<RhshfOfferFee> ProductFees,

    /// <summary>Security as the FAC declared and the Legal Officer verified it. Empty is possible —
    /// the document then says security is as recorded on the case.</summary>
    IReadOnlyList<string> SecurityDescriptions,

    /// <summary>Where the borrower pays any up-front charges. Null leaves the instruction out.</summary>
    RhshfOfferFeesAccount? FeesAccount,

    /// <summary>Days the offer stands before it lapses. The approved template says seven.</summary>
    int ValidityDays,

    /// <summary>Which offer this is on the case — printed on every page, so a corrected offer can
    /// never be mistaken for the one it replaced.</summary>
    int OfferVersion = 1,

    /// <summary>Everyone registered under the FAC, as the portal sends it. Printed beside
    /// <see cref="FarmerCount"/> (the farmers financed) only when they differ, so neither is read
    /// as the other.</summary>
    int? RegisteredFarmerCount = null,

    /// <summary>Schedule A: one per approved EOP, each with its own input rows and subtotals. Empty
    /// when the case has no itemised breakdown, in which case <see cref="PlanRows"/> carries the
    /// crop-level fallback.</summary>
    IReadOnlyList<RhshfOfferEopSchedule>? Schedules = null,

    /// <summary>Schedule B: the whole facility by component — inputs, services, packaging excess and
    /// each charge — footing to the approved amount, each charge marked with how it is paid.</summary>
    IReadOnlyList<RhshfOfferSummaryRow>? Summary = null);

/// <summary>One approved EOP's part of the facility.</summary>
public record RhshfOfferEopSchedule(
    string Title,
    int? Farmers,
    IReadOnlyList<RhshfOfferInputRow> Rows,
    /// <summary>Inputs, packaging excess and the EOP's own share of each charge. Empty when the
    /// portal sent no per-EOP summary — the rows are then printed without subtotals rather than with
    /// figures CRMS worked out itself.</summary>
    IReadOnlyList<RhshfOfferSummaryRow> Subtotals,
    decimal? Total,
    /// <summary>How the EOP total is built — "30 farmers × NGN 427,268.80 per farmer + NGN 66,400.00
    /// whole-pack adjustments" — the form the approved EOP itself states it in. Null when the
    /// per-farmer figure does not divide exactly, rather than printing a rounded one that does not add up.</summary>
    string? Basis = null);

/// <summary>One input or service as financed for an EOP. Every figure is the portal's own; the total
/// quantity is the per-farmer quantity times the EOP's farmers, which is how the portal counts it.</summary>
public record RhshfOfferInputRow(
    string Input,
    string? Product,
    string? Supplier,
    string? PerFarmer,
    string? TotalQuantity,
    decimal? UnitPrice,
    decimal Amount,
    decimal PackagingExcess,
    /// <summary>Farmers the line is costed for, so "2 Kg per farmer" is never read as 2 kg in total.</summary>
    int? Farmers = null,
    /// <summary>Packs bought and the price version, or a service provider's rate, where the portal
    /// sent them. Null otherwise.</summary>
    string? Detail = null);

/// <summary>A component of the facility. Treatment says how the borrower pays it — "Financed in the
/// loan" for everything the EOP finances, so nothing on the letter reads as payable twice.</summary>
public record RhshfOfferSummaryRow(string Label, decimal Amount, string? Basis, string? Treatment)
{
    public const string FinancedInLoan = "Financed in the loan";
}

/// <summary>
/// One row of the implementation plan. Supplier and quantity are null on charge rows and on
/// crop-level rows from a case with no itemised breakdown.
/// </summary>
public record RhshfOfferPlanRow(
    string? Supplier,
    string Description,
    string? QuantityDisplay,
    decimal Amount,
    /// <summary>True for the programme charges (insurance, working capital, technology services fee)
    /// and for a balancing row, so the plan can set them apart from the supplier lines they follow.</summary>
    bool IsCharge,
    /// <summary>The portal's own wording for how a charge was arrived at — "4% of the per-farmer
    /// input subtotal". Null where it sent none, or on an input row.</summary>
    string? Basis);

/// <summary>A fee the core banking product applies at booking.</summary>
public record RhshfOfferFee(string Name, string Display);

public record RhshfOfferFeesAccount(string BankName, string AccountName, string AccountNumber);
