namespace CRMS.Application.CreditBureau.DTOs;

public record BureauReportDto(
    Guid Id,
    Guid? LoanApplicationId,
    string Provider,
    string SubjectType,
    string Status,
    string? BVN,
    string? RegistryId,
    string SubjectName,
    int? CreditScore,
    string? ScoreGrade,
    DateTime? ReportDate,
    int TotalAccounts,
    int PerformingAccounts,
    int NonPerformingAccounts,
    int ClosedAccounts,
    decimal TotalOutstandingBalance,
    decimal TotalCreditLimit,
    int MaxDelinquencyDays,
    bool HasLegalActions,
    string? RequestReference,
    DateTime RequestedAt,
    DateTime? CompletedAt,
    string? ErrorMessage,
    List<BureauAccountDto> Accounts,
    List<BureauScoreFactorDto> ScoreFactors
);

public record BureauReportSummaryDto(
    Guid Id,
    string Provider,
    string SubjectName,
    string Status,
    int? CreditScore,
    string? ScoreGrade,
    DateTime RequestedAt,
    DateTime? CompletedAt
);

public record BureauAccountDto(
    Guid Id,
    string AccountNumber,
    string? CreditorName,
    string? AccountType,
    string Status,
    string DelinquencyLevel,
    decimal CreditLimit,
    decimal Balance,
    DateTime? DateOpened,
    DateTime? LastPaymentDate,
    string? PaymentProfile,
    string LegalStatus
);

public record BureauScoreFactorDto(
    Guid Id,
    string FactorCode,
    string FactorDescription,
    string Impact,
    int? Rank
);

public record BureauSearchResultDto(
    bool Found,
    string? RegistryId,
    string? FullName,
    string? BVN,
    string? DateOfBirth,
    string? Gender,
    string? Phone,
    string? Email,
    string? Address
);
