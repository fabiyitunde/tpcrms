namespace CRMS.Application.Guarantor.DTOs;

public record GuarantorDto(
    Guid Id,
    Guid LoanApplicationId,
    string GuarantorReference,
    string Type,
    string Status,
    string GuaranteeType,
    string FullName,
    string? BVN,
    string? CompanyName,
    string? RegistrationNumber,
    string? Email,
    string? Phone,
    string? Address,
    string? RelationshipToApplicant,
    bool IsDirector,
    bool IsShareholder,
    decimal? ShareholdingPercentage,
    decimal? DeclaredNetWorth,
    decimal? VerifiedNetWorth,
    string? Occupation,
    string? EmployerName,
    decimal? MonthlyIncome,
    string? Currency,
    decimal? GuaranteeLimit,
    bool IsUnlimited,
    DateTime? GuaranteeStartDate,
    DateTime? GuaranteeEndDate,
    Guid? BureauReportId,
    int? CreditScore,
    string? CreditScoreGrade,
    DateTime? CreditCheckDate,
    bool HasCreditIssues,
    string? CreditIssuesSummary,
    int ExistingGuaranteeCount,
    decimal? TotalExistingGuarantees,
    bool HasSignedGuaranteeAgreement,
    DateTime? AgreementSignedDate,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    string? RejectionReason,
    List<GuarantorDocumentDto> Documents
);

public record GuarantorSummaryDto(
    Guid Id,
    string GuarantorReference,
    string Type,
    string Status,
    string FullName,
    string? BVN,
    int? CreditScore,
    string? CreditScoreGrade,
    bool HasCreditIssues,
    decimal? GuaranteeLimit,
    bool IsUnlimited,
    DateTime CreatedAt
);

public record GuarantorDocumentDto(
    Guid Id,
    string DocumentType,
    string FileName,
    long FileSizeBytes,
    bool IsVerified,
    DateTime UploadedAt
);

public record AddIndividualGuarantorRequest(
    Guid LoanApplicationId,
    string FullName,
    string? BVN,
    string GuaranteeType,
    string? RelationshipToApplicant,
    string? Email,
    string? Phone,
    string? Address,
    decimal? GuaranteeLimit,
    string? Currency,
    bool IsDirector,
    bool IsShareholder,
    decimal? ShareholdingPercentage,
    decimal? DeclaredNetWorth,
    string? Occupation,
    string? EmployerName,
    decimal? MonthlyIncome
);

public record AddCorporateGuarantorRequest(
    Guid LoanApplicationId,
    string CompanyName,
    string RegistrationNumber,
    string? TaxId,
    string GuaranteeType,
    string? Email,
    string? Phone,
    string? Address,
    decimal? GuaranteeLimit,
    string? Currency
);

public record GuarantorCreditCheckResultDto(
    Guid GuarantorId,
    Guid BureauReportId,
    int? CreditScore,
    string? CreditScoreGrade,
    bool HasCreditIssues,
    string? CreditIssuesSummary,
    int ExistingGuaranteeCount,
    decimal? TotalExistingGuarantees
);
