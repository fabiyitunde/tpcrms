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
    string Tin,
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
    List<RhshfSupportingDocumentDto> SupportingDocuments);

public record RhshfSupportingDocumentDto(Guid Id, string FileName, long SizeBytes, DateTime UploadedAt);
