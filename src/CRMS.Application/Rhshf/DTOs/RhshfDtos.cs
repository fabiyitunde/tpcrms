namespace CRMS.Application.Rhshf.DTOs;

/// <summary>Response shape for POST /v1/credit-profiles, matching brief §4.1 exactly.</summary>
public record SubmitConsolidatedEopResultDto(
    string Reference,
    string Token,
    string ProfilingUrl,
    DateTime TokenExpiresAt,
    string Status);

/// <summary>Response shape for POST /v1/credit-profiles/{reference}/token, matching brief §4.6.</summary>
public record RhshfTokenRefreshResultDto(string Token, DateTime TokenExpiresAt);

/// <summary>Internal result of verifying+consuming a profiling token — not part of the brief's wire
/// contract; consumed by Phase 3's Razor Pages front door, not the portal.</summary>
public record RhshfTokenVerificationResultDto(string Reference, string Status, string? CurrentStage);

/// <summary>
/// One EOP line on the wire from the portal.
/// <para>
/// Supplier, Product and UnitOfMeasurement are optional so the portal can begin sending them
/// independently of this deployment: a payload without them is accepted exactly as before. The
/// offer letter's implementation plan names the supplier per line where one is sent, and states the
/// quantity in the unit given rather than assuming kilogrammes — a line can be counted in bags or
/// visits.
/// </para>
/// </summary>
public record RhshfEopLineDto(
    string Commodity,
    decimal QuantityKg,
    decimal UnitPricePerKg,
    decimal LineValue,
    string? Supplier = null,
    string? Product = null,
    string? UnitOfMeasurement = null)
{
    /// <summary>Quantity with its unit — mirrors RhshfEopLine.QuantityDisplay, so the staff-side
    /// tables and the generated documents word it identically. Falls back to kilogrammes for lines
    /// recorded before the portal sent a unit.</summary>
    public string QuantityDisplay =>
        $"{QuantityKg:N0} {(string.IsNullOrWhiteSpace(UnitOfMeasurement) ? "kg" : UnitOfMeasurement.Trim())}";

    /// <summary>The product where one was sent, otherwise the commodity.</summary>
    public string ItemDescription =>
        string.IsNullOrWhiteSpace(Product) ? Commodity : Product.Trim();
}

public record RhshfProgrammeDto(string Code, string Name);

public record RhshfSessionDto(string Code, string Name);

public record RhshfFacContactDto(string? Email, string? Phone);

public record RhshfFacDto(
    Guid FacId,
    string CompanyName,
    string RcNumber,
    string? Tin,
    string BoaAccountNumber,
    RhshfFacContactDto? Contact,
    string? State,
    string? Lga);

/// <summary>Request shape for POST /v1/credit-profiles, matching brief §4.1 exactly.</summary>
public record SubmitConsolidatedEopRequest(
    Guid SubmissionId,
    RhshfProgrammeDto Programme,
    RhshfSessionDto Session,
    RhshfFacDto Fac,
    decimal TotalEopValue,
    string Currency,
    int? FarmerCount,
    List<RhshfEopLineDto>? EopLines,
    string CallbackUrl,
    Dictionary<string, string>? Metadata,
    /// <summary>
    /// The itemised financing breakdown (portal interface change, 14 Oct 2026). Optional: a
    /// submission without it behaves exactly as before, which is what lets the portal roll it out
    /// independently of this deployment.
    /// </summary>
    RhshfFinancingDto? Financing = null);

/// <summary>
/// The approved EOP broken into the rows the offer letter prints, with the totals they reconcile to.
/// <para>
/// Every amount is used <b>exactly as sent</b>. The portal rounds unit prices of supplier-pack lines
/// down to the kobo, and rounds insurance and working capital per farmer before multiplying by the
/// farmer count, so recomputing from quantity × price or rate × base gives slightly different
/// figures. Two systems disagreeing by kobo on a document a borrower signs is the thing this whole
/// contract exists to prevent.
/// </para>
/// </summary>
public record RhshfFinancingDto(
    string? Currency = null,
    /// <summary>Every line of every approved EOP in the case, at input-item grain. This — not
    /// <c>eopLines</c>, which stays a crop-level summary with zero quantities — is what the offer
    /// letter's implementation plan is printed from.</summary>
    List<RhshfFinancingInputLineDto>? InputLines = null,
    /// <summary>The three programme charges financed inside the loan: insurance, working capital and
    /// the technology services fee. The appraisal and monitoring fees are deliberately not here —
    /// they are not financed, but configured on the core banking loan product and deducted at
    /// booking.</summary>
    List<RhshfFinancingChargeDto>? Charges = null,
    decimal? InputsTotal = null,
    decimal? PackagingExcessTotal = null,
    decimal? ChargesTotal = null,
    /// <summary>What BOA approved across the case's EOPs.</summary>
    decimal? ApprovedEopTotal = null,
    /// <summary>The amount to profile; equals the submission's TotalEopValue.</summary>
    decimal? LoanAmount = null,
    /// <summary>Approved but not financed under programme policy. Zero today, because the programme
    /// finances everything — but it exists, so the letter must never assume approved equals
    /// financed.</summary>
    decimal? ExcludedFromLoan = null,
    /// <summary>One summary per approved EOP. A case can carry several, each with its own farmer
    /// count and its own rates as they stood when it was approved.</summary>
    List<RhshfFinancingEopDto>? Eops = null);

public record RhshfFinancingInputLineDto(
    Guid? EopId = null,
    string? Crop = null,
    string? Item = null,
    string? Category = null,
    string? SpecificInput = null,
    string? Supplier = null,
    decimal? QuantityPerFarmer = null,
    string? Unit = null,
    decimal? UnitPrice = null,
    /// <summary>"perFarmer" — the amount is the per-farmer cost times the farmer count. "wholeEop" —
    /// a service priced for the whole EOP, not scaled by farmers.</summary>
    string? Basis = null,
    int? Farmers = null,
    /// <summary>The line's total for the whole EOP.</summary>
    decimal? Amount = null,
    /// <summary>What whole supplier packs cost beyond the per-farmer requirement — three bags bought
    /// where 2.4 are needed. Usually zero, and part of the amount financed when it is not.</summary>
    decimal? PackagingExcess = null,
    bool? InLoan = null);

public record RhshfFinancingChargeDto(
    /// <summary>"insurance", "workingCapital" or "techServicesFee".</summary>
    string? Code = null,
    /// <summary>The text to print — the portal supplies the wording, so the letter cannot drift from
    /// what the programme calls it.</summary>
    string? Label = null,
    /// <summary>The percentage, or naira per farmer for the technology fee. <b>Null when the case's
    /// EOPs used different rates</b>, in which case the letter prints the amount alone rather than
    /// implying one rate applied throughout.</summary>
    decimal? Rate = null,
    string? RateBasis = null,
    decimal? Amount = null,
    bool? InLoan = null);

public record RhshfFinancingEopDto(
    Guid? EopId = null,
    string? Crop = null,
    string? State = null,
    int? Farmers = null,
    decimal? Inputs = null,
    decimal? PackagingExcess = null,
    decimal? Insurance = null,
    decimal? WorkingCapital = null,
    decimal? TechServicesFee = null,
    decimal? ApprovedTotal = null,
    decimal? LoanAmount = null);
