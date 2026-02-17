using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface ICreditBureauProvider
{
    CreditBureauProvider ProviderType { get; }
    
    Task<Result<BureauSearchResult>> SearchByBVNAsync(string bvn, CancellationToken ct = default);
    Task<Result<BureauSearchResult>> SearchByNameAsync(string firstName, string lastName, DateTime? dateOfBirth, CancellationToken ct = default);
    Task<Result<BureauSearchResult>> SearchByTaxIdAsync(string taxId, CancellationToken ct = default);
    Task<Result<BureauCreditReport>> GetCreditReportAsync(string registryId, bool includePdf = false, CancellationToken ct = default);
    Task<Result<BureauCreditScore>> GetCreditScoreAsync(string registryId, CancellationToken ct = default);
}

public record BureauSearchResult(
    bool Found,
    string? RegistryId,
    string? FullName,
    string? BVN,
    string? DateOfBirth,
    string? Gender,
    string? Phone,
    string? Email,
    string? Address,
    SubjectType SubjectType
);

public record BureauCreditReport(
    string RegistryId,
    string FullName,
    int? CreditScore,
    string? ScoreGrade,
    DateTime ReportDate,
    string? RawJson,
    string? PdfBase64,
    BureauReportSummary Summary,
    List<BureauAccountData> Accounts,
    List<BureauScoreFactorData> ScoreFactors
);

public record BureauReportSummary(
    int TotalAccounts,
    int PerformingAccounts,
    int NonPerformingAccounts,
    int ClosedAccounts,
    int WrittenOffAccounts,
    decimal TotalOutstandingBalance,
    decimal TotalCreditLimit,
    int MaxDelinquencyDays,
    bool HasLegalActions,
    int EnquiriesLast30Days,
    int EnquiriesLast90Days
);

public record BureauAccountData(
    string AccountNumber,
    string? CreditorName,
    string? AccountType,
    string Status,
    int DelinquencyDays,
    decimal CreditLimit,
    decimal Balance,
    decimal? MinimumPayment,
    DateTime? DateOpened,
    DateTime? DateClosed,
    DateTime? LastPaymentDate,
    decimal? LastPaymentAmount,
    string? PaymentProfile,
    string? LegalStatus,
    DateTime? LegalStatusDate,
    string? Currency,
    DateTime LastUpdated
);

public record BureauScoreFactorData(
    string FactorCode,
    string Description,
    string Impact,
    int? Rank
);

public record BureauCreditScore(
    string RegistryId,
    int Score,
    string? Grade,
    DateTime GeneratedDate,
    List<BureauScoreFactorData> Factors
);
