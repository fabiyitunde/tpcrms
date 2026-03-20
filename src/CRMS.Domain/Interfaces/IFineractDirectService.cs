using CRMS.Domain.Common;

namespace CRMS.Domain.Interfaces;

public interface IFineractDirectService
{
    /// <summary>
    /// Calculate a proposed repayment schedule.
    /// If FineractProductId is set in the request, calls Fineract API for exact schedule.
    /// If not set, uses in-house financial math calculation.
    /// </summary>
    Task<Result<ProposedRepaymentSchedule>> CalculateRepaymentScheduleAsync(
        ScheduleCalculationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Get all accounts (savings, loans, shares) for a Fineract client.
    /// The clientId is the clientDetails.id from the middleware fulldetailsbynuban response.
    /// </summary>
    Task<Result<ClientAccountSummary>> GetClientAccountsAsync(
        long clientId, CancellationToken ct = default);

    /// <summary>
    /// Get full loan details including repayment schedule for a specific loan.
    /// </summary>
    Task<Result<FineractLoanDetail>> GetLoanDetailAsync(
        long loanId, CancellationToken ct = default);

    /// <summary>
    /// Get the total existing credit exposure for a client by aggregating all active loans.
    /// Combines GetClientAccountsAsync + GetLoanDetailAsync for each active loan.
    /// </summary>
    Task<Result<CustomerExposure>> GetCustomerExposureAsync(
        long clientId, string accountNumber, string customerName, CancellationToken ct = default);
}

// Schedule Calculation
public record ScheduleCalculationRequest(
    int ProductId,
    decimal Principal,
    int NumberOfRepayments,
    int RepaymentEvery,
    int RepaymentFrequencyType,       // 0=Days, 1=Weeks, 2=Months
    decimal InterestRatePerPeriod,
    int InterestRateFrequencyType,    // 2=Per Month, 3=Per Year
    int AmortizationType,             // 0=Equal Principal, 1=Equal Installments (EMI)
    int InterestType,                 // 0=Declining Balance, 1=Flat
    int InterestCalculationPeriodType,// 0=Daily, 1=Same as Repayment Period
    DateTime ExpectedDisbursementDate,
    int TransactionProcessingStrategyId = 1,
    string LoanType = "individual",
    int? GraceOnPrincipalPayment = null,
    int? GraceOnInterestPayment = null
);

public record ProposedRepaymentSchedule(
    decimal TotalPrincipal,
    decimal TotalInterest,
    decimal TotalFees,
    decimal TotalRepayment,
    IReadOnlyList<ProposedInstallment> Installments
);

public record ProposedInstallment(
    int PeriodNumber,
    DateTime FromDate,
    DateTime DueDate,
    decimal PrincipalDue,
    decimal InterestDue,
    decimal FeesDue,
    decimal TotalDue,
    decimal OutstandingBalance
);

// Client Account Summary
public record ClientAccountSummary(
    long ClientId,
    IReadOnlyList<ClientLoanAccountSummary> LoanAccounts,
    IReadOnlyList<ClientSavingsAccountSummary> SavingsAccounts
);

public record ClientLoanAccountSummary(
    long Id,
    string AccountNo,
    string ProductName,
    int ProductId,
    string Status,
    int StatusCode,
    string LoanType
);

public record ClientSavingsAccountSummary(
    long Id,
    string AccountNo,
    string ProductName,
    string Status,
    decimal AccountBalance
);

// Loan Detail
public record FineractLoanDetail(
    long Id,
    string AccountNo,
    string ProductName,
    string Status,
    int StatusCode,
    decimal Principal,
    decimal ApprovedPrincipal,
    decimal InterestRate,
    int NumberOfRepayments,
    DateTime? DisbursementDate,
    DateTime? MaturityDate,
    FineractLoanSummary Summary,
    IReadOnlyList<FineractSchedulePeriod> RepaymentSchedule
);

public record FineractLoanSummary(
    decimal TotalExpectedRepayment,
    decimal TotalRepayment,
    decimal TotalOutstanding,
    decimal PrincipalDisbursed,
    decimal PrincipalPaid,
    decimal PrincipalOutstanding,
    decimal InterestCharged,
    decimal InterestPaid,
    decimal InterestOutstanding,
    decimal FeeChargesCharged,
    decimal FeeChargesPaid,
    decimal FeeChargesOutstanding,
    decimal PenaltyChargesCharged,
    decimal PenaltyChargesPaid,
    decimal PenaltyChargesOutstanding
);

public record FineractSchedulePeriod(
    int Period,
    DateTime FromDate,
    DateTime DueDate,
    decimal PrincipalDue,
    decimal PrincipalPaid,
    decimal PrincipalOutstanding,
    decimal InterestDue,
    decimal InterestPaid,
    decimal InterestOutstanding,
    decimal FeeChargesDue,
    decimal PenaltyChargesDue,
    decimal TotalDue,
    decimal TotalPaid,
    decimal TotalOutstanding,
    bool Complete
);
