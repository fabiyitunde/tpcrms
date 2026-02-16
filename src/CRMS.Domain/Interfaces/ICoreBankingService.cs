using CRMS.Domain.Common;

namespace CRMS.Domain.Interfaces;

public interface ICoreBankingService
{
    // Customer Operations
    Task<Result<CustomerInfo>> GetCustomerByAccountNumberAsync(string accountNumber, CancellationToken ct = default);
    Task<Result<CustomerInfo>> GetCustomerByIdAsync(string customerId, CancellationToken ct = default);
    Task<Result<string>> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct = default);
    
    // Corporate Operations
    Task<Result<CorporateInfo>> GetCorporateInfoAsync(string accountNumber, CancellationToken ct = default);
    Task<Result<IReadOnlyList<DirectorInfo>>> GetDirectorsAsync(string corporateId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<SignatoryInfo>>> GetSignatoriesAsync(string accountNumber, CancellationToken ct = default);
    
    // Account Operations
    Task<Result<AccountInfo>> GetAccountInfoAsync(string accountNumber, CancellationToken ct = default);
    Task<Result<AccountStatement>> GetStatementAsync(string accountNumber, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<Result<decimal>> GetAccountBalanceAsync(string accountNumber, CancellationToken ct = default);
    
    // Loan Operations
    Task<Result<string>> CreateLoanAsync(CreateLoanRequest request, CancellationToken ct = default);
    Task<Result> ApproveLoanAsync(string loanId, CancellationToken ct = default);
    Task<Result> DisburseLoanAsync(DisbursementRequest request, CancellationToken ct = default);
    Task<Result<LoanInfo>> GetLoanInfoAsync(string loanId, CancellationToken ct = default);
    Task<Result<RepaymentSchedule>> GetRepaymentScheduleAsync(string loanId, CancellationToken ct = default);
    Task<Result<LoanStatus>> GetLoanStatusAsync(string loanId, CancellationToken ct = default);
}

// Customer Models
public record CustomerInfo(
    string CustomerId,
    string FullName,
    CustomerType CustomerType,
    string? Email,
    string? PhoneNumber,
    string? BVN,
    DateTime? DateOfBirth,
    string? Address
);

public enum CustomerType
{
    Individual,
    Corporate
}

public record CreateCustomerRequest(
    string FullName,
    CustomerType CustomerType,
    string? Email,
    string? PhoneNumber,
    string? BVN,
    DateTime? DateOfBirth,
    string? Address
);

// Corporate Models
public record CorporateInfo(
    string CorporateId,
    string CompanyName,
    string? RegistrationNumber,
    string? Industry,
    DateTime? IncorporationDate,
    string? RegisteredAddress,
    string? TaxIdentificationNumber
);

public record DirectorInfo(
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

public record SignatoryInfo(
    string SignatoryId,
    string FullName,
    string? BVN,
    string? Email,
    string? PhoneNumber,
    string MandateType,
    string? Designation
);

// Account Models
public record AccountInfo(
    string AccountNumber,
    string AccountName,
    string AccountType,
    string Currency,
    decimal CurrentBalance,
    decimal AvailableBalance,
    string Status,
    DateTime OpenedDate
);

public record AccountStatement(
    string AccountNumber,
    DateTime FromDate,
    DateTime ToDate,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal TotalCredits,
    decimal TotalDebits,
    IReadOnlyList<StatementTransaction> Transactions
);

public record StatementTransaction(
    string TransactionId,
    DateTime Date,
    string Description,
    decimal Amount,
    TransactionType Type,
    decimal RunningBalance,
    string? Reference
);

public enum TransactionType
{
    Credit,
    Debit
}

// Loan Models
public record CreateLoanRequest(
    string CustomerId,
    string AccountNumber,
    string ProductCode,
    decimal PrincipalAmount,
    int TenorMonths,
    decimal InterestRatePerAnnum,
    DateTime ExpectedDisbursementDate,
    string RepaymentFrequency,
    string IdempotencyKey
);

public record DisbursementRequest(
    string LoanId,
    string DisbursementAccountNumber,
    decimal Amount,
    DateTime DisbursementDate,
    string IdempotencyKey
);

public record LoanInfo(
    string LoanId,
    string CustomerId,
    string AccountNumber,
    decimal PrincipalAmount,
    decimal OutstandingBalance,
    decimal InterestRate,
    int TenorMonths,
    DateTime? DisbursementDate,
    DateTime? MaturityDate,
    LoanStatus Status
);

public enum LoanStatus
{
    PendingApproval,
    Approved,
    Active,
    Closed,
    WrittenOff,
    Rejected
}

public record RepaymentSchedule(
    string LoanId,
    IReadOnlyList<RepaymentInstallment> Installments
);

public record RepaymentInstallment(
    int InstallmentNumber,
    DateTime DueDate,
    decimal PrincipalAmount,
    decimal InterestAmount,
    decimal TotalAmount,
    decimal OutstandingAfter,
    InstallmentStatus Status
);

public enum InstallmentStatus
{
    Pending,
    Paid,
    Overdue,
    PartiallyPaid
}
