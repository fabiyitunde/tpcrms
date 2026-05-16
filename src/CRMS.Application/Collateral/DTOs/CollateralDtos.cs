namespace CRMS.Application.Collateral.DTOs;

public record CollateralDto(
    Guid Id,
    Guid LoanApplicationId,
    string CollateralReference,
    string Type,
    string Status,
    string PerfectionStatus,
    string Description,
    string? AssetIdentifier,
    string? Location,
    string? OwnerName,
    string? OwnershipType,
    decimal? IndicativeValue,
    decimal? MarketValue,
    decimal? ForcedSaleValue,
    decimal HaircutPercentage,
    string ValuationBasis,
    decimal? AcceptableValue,
    decimal? ValuerAcceptableValue,
    string? ValuerName,
    string? ValuerCompany,
    string? ValuationReportPath,
    string? Currency,
    DateTime? LastValuationDate,
    DateTime? NextRevaluationDue,
    string? LienType,
    string? LienReference,
    DateTime? LienRegistrationDate,
    bool IsInsured,
    string? InsurancePolicyNumber,
    decimal? InsuredValue,
    DateTime? InsuranceExpiryDate,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    string? RejectionReason,
    Guid? CollateralTypeConfigId,
    List<CollateralValuationDto> Valuations,
    List<CollateralDocumentDto> Documents,
    bool IsLegalCleared = false,
    DateTime? LegalClearedAt = null,
    string? LegalClearanceNotes = null
);

public record CollateralSummaryDto(
    Guid Id,
    string CollateralReference,
    string Type,
    string Status,
    string Description,
    decimal? AcceptableValue,
    string? Currency,
    string PerfectionStatus,
    DateTime CreatedAt
);

public record CollateralValuationDto(
    Guid Id,
    string Type,
    string Status,
    DateTime ValuationDate,
    decimal MarketValue,
    decimal? ForcedSaleValue,
    string? Currency,
    string? ValuerName,
    string? ValuerCompany,
    DateTime? ExpiryDate
);

public record CollateralDocumentDto(
    Guid Id,
    string DocumentType,
    string FileName,
    long FileSizeBytes,
    bool IsVerified,
    DateTime UploadedAt,
    string? Description = null
);

public record CollateralTypeConfigDto(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    decimal HaircutRate,
    string ValuationBasis,
    bool IsActive,
    int SortOrder
);

public record AddCollateralRequest(
    Guid LoanApplicationId,
    string Type,
    string Description,
    string? AssetIdentifier,
    string? Location,
    string? OwnerName,
    string? OwnershipType,
    Guid? CollateralTypeConfigId = null,
    decimal? IndicativeValue = null,
    string Currency = "NGN"
);

public record SetCollateralValuationRequest(
    decimal MarketValue,
    decimal? ForcedSaleValue,
    string Currency,
    decimal? HaircutPercentage,
    string? ValuationBasis = null,
    decimal? ValuerAcceptableValue = null,
    string? ValuerName = null,
    string? ValuerCompany = null,
    string? ValuationReportPath = null
);

public record RecordPerfectionRequest(
    string LienType,
    string LienReference,
    string RegistrationAuthority,
    DateTime? RegistrationDate
);

public record RecordInsuranceRequest(
    string PolicyNumber,
    string Company,
    decimal InsuredValue,
    string Currency,
    DateTime ExpiryDate
);

public record AddValuationRequest(
    string Type,
    DateTime ValuationDate,
    decimal MarketValue,
    decimal? ForcedSaleValue,
    string Currency,
    string? ValuerName,
    string? ValuerCompany,
    string? ValuerLicense,
    string? ReportReference,
    string? Remarks
);
