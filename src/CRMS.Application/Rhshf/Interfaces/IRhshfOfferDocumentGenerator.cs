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

    /// <summary>Months of the tenor during which no repayment falls due. Null until the bank settles
    /// whether this comes from the core banking product or is set per case; the repayment section
    /// then describes the schedule without naming a grace period.</summary>
    int? GraceMonths,

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
    int ValidityDays);

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
