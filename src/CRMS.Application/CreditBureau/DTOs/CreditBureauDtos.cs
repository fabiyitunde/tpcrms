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
    int ActiveLoans,
    int PerformingAccounts,
    int NonPerformingAccounts,
    int ClosedAccounts,
    decimal TotalOutstandingBalance,
    decimal TotalOverdue,
    decimal TotalCreditLimit,
    int MaxDelinquencyDays,
    bool HasLegalActions,
    string? RequestReference,
    DateTime RequestedAt,
    DateTime? CompletedAt,
    string? ErrorMessage,
    // Fraud check results (SmartComply)
    int? FraudRiskScore,
    string? FraudRecommendation,
    // Party linkage
    Guid? PartyId,
    string? PartyType,
    List<BureauAccountDto> Accounts,
    List<BureauScoreFactorDto> ScoreFactors
);

public record BureauReportSummaryDto(
    Guid Id,
    string Provider,
    string SubjectType,
    string SubjectName,
    string Status,
    int? CreditScore,
    string? ScoreGrade,
    int ActiveLoans,
    decimal TotalOutstandingBalance,
    decimal TotalOverdue,
    int MaxDelinquencyDays,
    bool HasLegalActions,
    // Fraud check results (SmartComply)
    int? FraudRiskScore,
    string? FraudRecommendation,
    // Party linkage
    Guid? PartyId,
    string? PartyType,
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
