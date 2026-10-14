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

    /// <summary>
    /// List all loan products configured in Fineract (GET /loanproducts).
    /// When activeOnly=true, filters out products whose closeDate is in the past.
    /// </summary>
    Task<Result<IReadOnlyList<FineractLoanProduct>>> GetLoanProductsAsync(
        bool activeOnly = true, CancellationToken ct = default);

    /// <summary>
    /// Get client details by clientId (GET /clients/{clientId}).
    /// Returns officeId and officeName, which map to a CRMS branch/location.
    /// Used at NAMP webhook ingest to resolve which branch the applicant belongs to.
    /// </summary>
    Task<Result<FineractClientInfo>> GetClientByIdAsync(long clientId, CancellationToken ct = default);

    /// <summary>
    /// Automates the booking, approval, and disbursement of an approved loan in Fineract.
    /// Returns Success for a partial booking too (loan created but approve or disburse failed), with
    /// <see cref="FineractBookingResult.Disbursed"/> false — callers MUST branch on Disbursed, never on
    /// IsSuccess or Booked, before recording money as having moved. Pass
    /// <see cref="FineractLoanBookingRequest.ExternalId"/> so a retry resumes the existing loan.
    /// </summary>
    Task<Result<FineractBookingResult>> BookApprovedLoanAsync(
        FineractLoanBookingRequest request, CancellationToken ct = default);

    /// <summary>
    /// Looks up a BOA savings account by its external ID to retrieve the Fineract clientId.
    /// Endpoint: GET /tp/savingsaccounts/byexternalId/{boaAccountNumber}
    /// Used at NAMP webhook ingest and recall to resolve the Fineract client for the applicant.
    /// </summary>
    Task<Result<NampBoaAccountInfo>> GetNampBoaAccountAsync(string boaAccountNumber, CancellationToken ct = default);
}

public record NampBoaAccountInfo(long ClientId, string AccountStatus, long SavingsAccountId, string? SavingsAccountNo);

public record FineractClientInfo(long Id, int OfficeId, string OfficeName, string DisplayName);

// Loan Booking and Disbursement
public record FineractLoanBookingRequest(
    long ClientId,
    int ProductId,
    decimal Principal,
    int TenorMonths,
    decimal InterestRatePerAnnum,
    DateTime ValueDate,
    string RepaymentAccountNumber,
    bool DisburseToSavings,
    // When true (default), Fineract auto-creates a savings -> loan repayment standing instruction
    // at disbursement so the linked savings account (RepaymentAccountNumber) is auto-debited for
    // loan dues. This is independent of DisburseToSavings: the loan can disburse to a vendor while
    // repayments are still auto-collected from the applicant's savings.
    bool CreateRepaymentStandingInstruction = true,
    // A stable, unique CRMS reference for the loan (application number / case reference + cycle).
    // Sent as the core banking loan's externalId, which core banking stores and enforces uniqueness on.
    // This is what makes booking resumable: if a previous attempt created the loan but failed to
    // approve or disburse it, the next attempt finds that loan by externalId and continues from the
    // step that failed instead of creating a second loan for the same application.
    string? ExternalId = null
);

public record FineractBookingResult(
    long LoanId,
    string LoanAccountNumber,
    bool Booked,
    bool Approved,
    bool Disbursed,
    string Status,
    // True when this call did not create the loan but picked up one an earlier attempt had already
    // created (matched on ExternalId) — for logging and officer-facing wording, nothing else.
    bool Resumed = false
);

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

// Loan Products
public record FineractLoanProduct(
    int Id,
    string Name,
    string ShortName,
    string? Description,
    string CurrencyCode,
    string CurrencySymbol,
    decimal MinPrincipal,
    decimal DefaultPrincipal,
    decimal MaxPrincipal,
    decimal DefaultInterestRatePerPeriod,
    decimal? MinInterestRatePerPeriod,
    decimal? MaxInterestRatePerPeriod,
    decimal AnnualInterestRate,
    string InterestRateFrequencyType,  // "Per month", "Per year"
    int DefaultNumberOfRepayments,
    int? MinNumberOfRepayments,
    int? MaxNumberOfRepayments,
    int RepaymentEvery,
    string RepaymentFrequencyType,     // "Months", "Weeks", "Days"
    string AmortizationType,           // "Equal installments", "Equal principal payments"
    string InterestType,               // "Declining Balance", "Flat"
    int TransactionProcessingStrategyId,
    DateTime? StartDate,
    DateTime? CloseDate,
    bool IsActive,
    /// <summary>
    /// Fees configured on the product, which core banking applies at booking. CRMS sends no charges
    /// when it books — it sends principal, tenor and rate — so these are the authoritative statement
    /// of what the borrower will actually be charged, and the only safe source for the fee lines on
    /// an offer letter.
    /// <para>
    /// Null and empty both mean "none defined"; read it through <see cref="ProductCharges"/> rather
    /// than directly. Optional so the many callers that only want principal and rate bounds are
    /// unaffected.
    /// </para>
    /// </summary>
    IReadOnlyList<FineractLoanProductCharge>? Charges = null
)
{
    /// <summary>The product's fees, never null.</summary>
    public IReadOnlyList<FineractLoanProductCharge> ProductCharges => Charges ?? [];
}

/// <summary>
/// One fee configured on a loan product. <paramref name="CalculationType"/> is what tells a reader
/// whether <paramref name="Amount"/> is naira or a percentage ("Flat" versus "% Approved Amount"),
/// so the two must always be presented together — an amount of 1 means a flat ₦1 or 1% depending on
/// it.
/// </summary>
public record FineractLoanProductCharge(
    int Id,
    string Name,
    decimal Amount,
    string CalculationType,
    string TimeType,
    string? CurrencyCode)
{
    /// <summary>True when Amount is a percentage rather than a naira figure.</summary>
    public bool IsPercentage =>
        CalculationType.Contains('%', StringComparison.Ordinal)
        || CalculationType.Contains("percent", StringComparison.OrdinalIgnoreCase);

    /// <summary>The fee as it should read on a document: "1% of approved amount" or "NGN 5,000.00".</summary>
    public string Display => IsPercentage
        ? $"{Amount:0.##}% ({CalculationType})"
        : $"{CurrencyCode ?? "NGN"} {Amount:N2}";
}
