using CRMS.Domain.Enums;

namespace CRMS.Application.Rhshf.DTOs;

/// <summary>View model for the profiling form (Razor Pages front door) — internal to CRMS, not part
/// of the portal's wire contract.</summary>
public record RhshfProfilingSessionDto(
    string Reference,
    RhshfCaseStatus Status,
    RhshfProfilingStage? CurrentStage,
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
    /// <summary>
    /// True when an offer is generated and awaiting the FAC's response. Profiling itself is
    /// finished at that point, so the wizard shows a completed panel — but the case is not done
    /// with the FAC, and without a route onward they land on "you can close this tab" while an
    /// offer sits waiting. Lets the completed panel link through to the offer page.
    /// </summary>
    bool IsAwaitingOfferAcceptance = false);

public record RhshfSupportingDocumentDto(Guid Id, string FileName, long SizeBytes, DateTime UploadedAt)
{
    /// <summary>Defaulted so existing staff-side callers that don't care about categories are
    /// unaffected.</summary>
    public RhshfDocumentCategory Category { get; init; } = RhshfDocumentCategory.Other;
}

public record RhshfDocumentRequirementStatusDto(
    RhshfDocumentCategory Category, string Title, string? Description,
    bool IsMandatory, int SortOrder, bool IsSatisfied, int AttachedCount);

public record RhshfProfilingFarmPlanDto(
    Guid Id, string Crop, decimal Hectares, decimal ExpectedYieldKgPerHectare,
    decimal ExpectedPricePerKg, decimal ExpectedOutputKg, decimal ExpectedRevenue);
