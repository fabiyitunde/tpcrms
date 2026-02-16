namespace CRMS.Application.CoreBanking.DTOs;

public record CustomerInfoDto(
    string CustomerId,
    string FullName,
    string CustomerType,
    string? Email,
    string? PhoneNumber,
    string? BVN,
    DateTime? DateOfBirth,
    string? Address
);

public record CorporateInfoDto(
    string CorporateId,
    string CompanyName,
    string? RegistrationNumber,
    string? Industry,
    DateTime? IncorporationDate,
    string? RegisteredAddress,
    string? TaxIdentificationNumber
);

public record DirectorInfoDto(
    string DirectorId,
    string FullName,
    string? BVN,
    string? Email,
    string? PhoneNumber,
    string? Address,
    DateTime? DateOfBirth,
    string? Nationality,
    decimal? ShareholdingPercent
);

public record SignatoryInfoDto(
    string SignatoryId,
    string FullName,
    string? BVN,
    string? Email,
    string? PhoneNumber,
    string MandateType,
    string? Designation
);

public record AccountInfoDto(
    string AccountNumber,
    string AccountName,
    string AccountType,
    string Currency,
    decimal CurrentBalance,
    decimal AvailableBalance,
    string Status,
    DateTime OpenedDate
);

public record AccountStatementDto(
    string AccountNumber,
    DateTime FromDate,
    DateTime ToDate,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal TotalCredits,
    decimal TotalDebits,
    List<StatementTransactionDto> Transactions
);

public record StatementTransactionDto(
    string TransactionId,
    DateTime Date,
    string Description,
    decimal Amount,
    string Type,
    decimal RunningBalance,
    string? Reference
);

public record CorporateAccountDataDto(
    CustomerInfoDto Customer,
    CorporateInfoDto Corporate,
    AccountInfoDto Account,
    List<DirectorInfoDto> Directors,
    List<SignatoryInfoDto> Signatories,
    AccountStatementDto? Statement
);
