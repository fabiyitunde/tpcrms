using CRMS.Domain.Enums;

namespace CRMS.Application.Rhshf.DTOs;

/// <summary>View model for the profiling form (Razor Pages front door) — internal to CRMS, not part
/// of the portal's wire contract.</summary>
public record RhshfProfilingSessionDto(
    string Reference,
    RhshfCaseStatus Status,
    RhshfProfilingStage? CurrentStage,
    /// <summary>Highest stage the FAC has reached this cycle — the bound on back/forward navigation.
    /// Steps up to and including this are navigable; beyond it is locked. Null when profiling is done.</summary>
    RhshfProfilingStage? FurthestStage,
    string CompanyName,
    string RcNumber,
    string? Tin,
    string BoaAccountNumber,
    string State,
    string Lga,
    decimal TotalEopValue,
    string Currency,
    int? FarmerCount,
    List<RhshfEopLineDto> EopLines,
    RhshfBureauOutcome BureauCheckOutcome,
    int? BureauTotalLoans,
    int? BureauActiveLoans,
    int? BureauDelinquentFacilities,
    decimal? BureauTotalOutstanding,
    decimal? BureauTotalOverdue,
    List<RhshfSupportingDocumentDto> SupportingDocuments,
    /// <summary>The configured required-document checklist, each marked with whether this case has
    /// satisfied it. Drives both the upload UI and the submission gate's error message.</summary>
    List<RhshfDocumentRequirementStatusDto> DocumentRequirements,
    /// <summary>Farm plan for the cycle being prepared — the agronomic basis the appraisal needs.</summary>
    List<RhshfProfilingFarmPlanDto> FarmPlans,
    /// <summary>Directors the FAC declares (with BVN) during profiling — the source the bureau checks
    /// use. BVN is presence-only on the wire. Moved from officer-entered to FAC-supplied (control fix).</summary>
    List<RhshfProfilingDirectorDto> Directors,
    /// <summary>Guarantors the FAC declares during profiling (also moved from officer-entered).</summary>
    List<RhshfProfilingGuarantorDto> Guarantors,
    /// <summary>Collateral the FAC declares during profiling — the Legal Officer verifies and perfects
    /// it later rather than scouting for it (control fix). Filed under the profiling target cycle.</summary>
    List<RhshfProfilingCollateralDto> Collateral,
    /// <summary>
    /// True when an offer is generated and awaiting the FAC's response. Profiling itself is
    /// finished at that point, so the wizard shows a completed panel — but the case is not done
    /// with the FAC, and without a route onward they land on "you can close this tab" while an
    /// offer sits waiting. Lets the completed panel link through to the offer page.
    /// </summary>
    bool IsAwaitingOfferAcceptance = false,
    /// <summary>Total shareholding declared across the directors, and what is left of 100%. Drives
    /// the running total, the input ceiling, and the over-/under-allocated messages — all computed
    /// on the aggregate so the page and the server agree on the arithmetic.</summary>
    decimal DeclaredShareholdingTotal = 0m,
    decimal RemainingShareholding = 100m,
    /// <summary>Set while a return from the bank is unanswered: the officer's reason, and when.
    /// The FAC arrives here from the portal's "correction required" prompt, so the form has to say
    /// what to fix, not just reopen.</summary>
    string? ReturnReason = null,
    DateTime? ReturnedAt = null,
    bool IsReturned = false);

public record RhshfSupportingDocumentDto(Guid Id, string FileName, long SizeBytes, DateTime UploadedAt)
{
    /// <summary>Defaulted for the same reason as Category — only the FAC preview needs it, to pick
    /// between an inline viewer and a download-only message.</summary>
    public string ContentType { get; init; } = "";

    /// <summary>Defaulted so existing staff-side callers that don't care about categories are
    /// unaffected.</summary>
    public RhshfDocumentCategory Category { get; init; } = RhshfDocumentCategory.Other;

    /// <summary>Case reference this document was carried over from, or null if it was uploaded here.
    /// Shown so neither the FAC nor the reviewer mistakes a document from an earlier season for one
    /// prepared for this application.</summary>
    public string? ReusedFromReference { get; init; }

    /// <summary>When the FAC originally supplied a carried-over document, on the source case. Null
    /// for an ordinary upload, where <see cref="UploadedAt"/> is the only date that applies.</summary>
    public DateTime? OriginalUploadedAt { get; init; }

    public bool IsReused => ReusedFromReference is not null;
}

public record RhshfDocumentRequirementStatusDto(
    RhshfDocumentCategory Category, string Title, string? Description,
    bool IsMandatory, int SortOrder, bool IsSatisfied, int AttachedCount,
    /// <summary>Formats this slot accepts; empty means anything. Shown on the form so the FAC
    /// learns the rule before choosing a file rather than from a rejection afterwards.</summary>
    IReadOnlyList<RhshfDocumentFormat>? AllowedFormats = null,
    bool MustMatchFinancialTemplate = false);

/// <summary>A document from one of this FAC's earlier cases, offered for carry-over on the documents
/// stage. The source reference, session and status are shown so the FAC can tell which document it
/// is picking up — CRMS enforces no expiry, so that judgement is theirs.</summary>
public record RhshfReusableDocumentDto(
    Guid DocumentId, string FileName, long SizeBytes, DateTime UploadedAt,
    string SourceReference, string SourceSessionName, RhshfCaseStatus SourceStatus);

public record RhshfProfilingFarmPlanDto(
    Guid Id, string Crop, decimal Hectares, decimal ExpectedYieldKgPerHectare,
    decimal ExpectedPricePerKg, decimal ExpectedOutputKg, decimal ExpectedRevenue);

public record RhshfProfilingDirectorDto(
    Guid Id, string FullName, bool HasBvn, decimal? ShareholdingPercent, bool IsChairman);

public record RhshfProfilingGuarantorDto(
    Guid Id, string FullName, RhshfGuarantorType GuarantorType, bool HasBvn, string? RcNumber,
    string? RelationshipDisplay, decimal? GuaranteeAmount,
    /// <summary>The raw category and specification, as well as the display text: the edit form has to
    /// pre-select the dropdown and refill the Specify box, and the display text alone cannot do that
    /// — for Other it is the free text, which matches no category.</summary>
    RhshfGuarantorRelationship? RelationshipCategory = null,
    string? RelationshipOther = null);

public record RhshfProfilingCollateralDto(
    Guid Id, RhshfCollateralType Type, string? ReferenceNumber,
    string? GuarantorBankName, decimal? GuaranteeAmount, decimal? CrgCoveragePercentage,
    string? PropertyDescription, decimal? PropertyValue, string? Notes,
    List<RhshfProfilingCollateralDocumentDto> Documents);

public record RhshfProfilingCollateralDocumentDto(Guid Id, string FileName, long SizeBytes, string ContentType = "");
