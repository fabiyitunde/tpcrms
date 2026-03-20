using System.Text.Json.Serialization;

namespace CRMS.Infrastructure.ExternalServices.FineractDirect;

// POST /authentication response
public class FineractAuthResponse
{
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("userId")]
    public int UserId { get; set; }

    [JsonPropertyName("base64EncodedAuthenticationKey")]
    public string? Base64EncodedAuthenticationKey { get; set; }

    [JsonPropertyName("authenticated")]
    public bool Authenticated { get; set; }

    [JsonPropertyName("roles")]
    public List<FineractRoleInfo>? Roles { get; set; }
}

public class FineractRoleInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

// POST /loans?command=calculateLoanSchedule response
public class FineractScheduleResponse
{
    [JsonPropertyName("currency")]
    public FineractCurrency? Currency { get; set; }

    [JsonPropertyName("loanTermInDays")]
    public int LoanTermInDays { get; set; }

    [JsonPropertyName("totalPrincipalDisbursed")]
    public decimal TotalPrincipalDisbursed { get; set; }

    [JsonPropertyName("totalPrincipalExpected")]
    public decimal TotalPrincipalExpected { get; set; }

    [JsonPropertyName("totalInterestCharged")]
    public decimal TotalInterestCharged { get; set; }

    [JsonPropertyName("totalFeeChargesCharged")]
    public decimal TotalFeeChargesCharged { get; set; }

    [JsonPropertyName("totalPenaltyChargesCharged")]
    public decimal TotalPenaltyChargesCharged { get; set; }

    [JsonPropertyName("totalRepaymentExpected")]
    public decimal TotalRepaymentExpected { get; set; }

    [JsonPropertyName("totalOutstanding")]
    public decimal TotalOutstanding { get; set; }

    [JsonPropertyName("periods")]
    public List<FineractSchedulePeriodDto>? Periods { get; set; }
}

public class FineractCurrency
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("decimalPlaces")]
    public int DecimalPlaces { get; set; }

    [JsonPropertyName("displaySymbol")]
    public string? DisplaySymbol { get; set; }
}

public class FineractSchedulePeriodDto
{
    [JsonPropertyName("period")]
    public int? Period { get; set; }

    [JsonPropertyName("fromDate")]
    public List<int>? FromDate { get; set; }

    [JsonPropertyName("dueDate")]
    public List<int>? DueDate { get; set; }

    [JsonPropertyName("daysInPeriod")]
    public int DaysInPeriod { get; set; }

    [JsonPropertyName("principalDisbursed")]
    public decimal PrincipalDisbursed { get; set; }

    [JsonPropertyName("principalLoanBalanceOutstanding")]
    public decimal PrincipalLoanBalanceOutstanding { get; set; }

    [JsonPropertyName("principalDue")]
    public decimal PrincipalDue { get; set; }

    [JsonPropertyName("principalOriginalDue")]
    public decimal PrincipalOriginalDue { get; set; }

    [JsonPropertyName("interestDue")]
    public decimal InterestDue { get; set; }

    [JsonPropertyName("interestOriginalDue")]
    public decimal InterestOriginalDue { get; set; }

    [JsonPropertyName("feeChargesDue")]
    public decimal FeeChargesDue { get; set; }

    [JsonPropertyName("penaltyChargesDue")]
    public decimal PenaltyChargesDue { get; set; }

    [JsonPropertyName("totalDueForPeriod")]
    public decimal TotalDueForPeriod { get; set; }

    [JsonPropertyName("totalOriginalDueForPeriod")]
    public decimal TotalOriginalDueForPeriod { get; set; }

    [JsonPropertyName("totalOutstandingForPeriod")]
    public decimal TotalOutstandingForPeriod { get; set; }

    [JsonPropertyName("complete")]
    public bool Complete { get; set; }
}

// GET /clients/{clientId}/accounts response
public class FineractClientAccountsResponse
{
    [JsonPropertyName("loanAccounts")]
    public List<FineractLoanAccountDto>? LoanAccounts { get; set; }

    [JsonPropertyName("savingsAccounts")]
    public List<FineractSavingsAccountDto>? SavingsAccounts { get; set; }

    [JsonPropertyName("shareAccounts")]
    public List<object>? ShareAccounts { get; set; }
}

public class FineractLoanAccountDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("accountNo")]
    public string? AccountNo { get; set; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("productId")]
    public int ProductId { get; set; }

    [JsonPropertyName("productName")]
    public string? ProductName { get; set; }

    [JsonPropertyName("shortProductName")]
    public string? ShortProductName { get; set; }

    [JsonPropertyName("status")]
    public FineractStatusDto? Status { get; set; }

    [JsonPropertyName("loanType")]
    public FineractEnumDto? LoanType { get; set; }

    [JsonPropertyName("loanCycle")]
    public int LoanCycle { get; set; }

    [JsonPropertyName("inArrears")]
    public bool InArrears { get; set; }
}

public class FineractSavingsAccountDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("accountNo")]
    public string? AccountNo { get; set; }

    [JsonPropertyName("productId")]
    public int ProductId { get; set; }

    [JsonPropertyName("productName")]
    public string? ProductName { get; set; }

    [JsonPropertyName("status")]
    public FineractStatusDto? Status { get; set; }

    [JsonPropertyName("accountBalance")]
    public decimal AccountBalance { get; set; }
}

public class FineractStatusDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("closed")]
    public bool Closed { get; set; }
}

public class FineractEnumDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

// GET /loans/{loanId}?associations=repaymentSchedule,transactions response
public class FineractLoanDetailResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("accountNo")]
    public string? AccountNo { get; set; }

    [JsonPropertyName("status")]
    public FineractStatusDto? Status { get; set; }

    [JsonPropertyName("productName")]
    public string? ProductName { get; set; }

    [JsonPropertyName("principal")]
    public decimal Principal { get; set; }

    [JsonPropertyName("approvedPrincipal")]
    public decimal ApprovedPrincipal { get; set; }

    [JsonPropertyName("annualInterestRate")]
    public decimal AnnualInterestRate { get; set; }

    [JsonPropertyName("numberOfRepayments")]
    public int NumberOfRepayments { get; set; }

    [JsonPropertyName("timeline")]
    public FineractLoanTimeline? Timeline { get; set; }

    [JsonPropertyName("summary")]
    public FineractLoanSummaryDto? Summary { get; set; }

    [JsonPropertyName("repaymentSchedule")]
    public FineractRepaymentScheduleDto? RepaymentSchedule { get; set; }
}

public class FineractLoanTimeline
{
    [JsonPropertyName("submittedOnDate")]
    public List<int>? SubmittedOnDate { get; set; }

    [JsonPropertyName("approvedOnDate")]
    public List<int>? ApprovedOnDate { get; set; }

    [JsonPropertyName("expectedDisbursementDate")]
    public List<int>? ExpectedDisbursementDate { get; set; }

    [JsonPropertyName("actualDisbursementDate")]
    public List<int>? ActualDisbursementDate { get; set; }

    [JsonPropertyName("expectedMaturityDate")]
    public List<int>? ExpectedMaturityDate { get; set; }
}

public class FineractLoanSummaryDto
{
    [JsonPropertyName("totalExpectedRepayment")]
    public decimal TotalExpectedRepayment { get; set; }

    [JsonPropertyName("totalRepayment")]
    public decimal TotalRepayment { get; set; }

    [JsonPropertyName("totalOutstanding")]
    public decimal TotalOutstanding { get; set; }

    [JsonPropertyName("principalDisbursed")]
    public decimal PrincipalDisbursed { get; set; }

    [JsonPropertyName("principalPaid")]
    public decimal PrincipalPaid { get; set; }

    [JsonPropertyName("principalOutstanding")]
    public decimal PrincipalOutstanding { get; set; }

    [JsonPropertyName("interestCharged")]
    public decimal InterestCharged { get; set; }

    [JsonPropertyName("interestPaid")]
    public decimal InterestPaid { get; set; }

    [JsonPropertyName("interestOutstanding")]
    public decimal InterestOutstanding { get; set; }

    [JsonPropertyName("feeChargesCharged")]
    public decimal FeeChargesCharged { get; set; }

    [JsonPropertyName("feeChargesPaid")]
    public decimal FeeChargesPaid { get; set; }

    [JsonPropertyName("feeChargesOutstanding")]
    public decimal FeeChargesOutstanding { get; set; }

    [JsonPropertyName("penaltyChargesCharged")]
    public decimal PenaltyChargesCharged { get; set; }

    [JsonPropertyName("penaltyChargesPaid")]
    public decimal PenaltyChargesPaid { get; set; }

    [JsonPropertyName("penaltyChargesOutstanding")]
    public decimal PenaltyChargesOutstanding { get; set; }
}

public class FineractRepaymentScheduleDto
{
    [JsonPropertyName("currency")]
    public FineractCurrency? Currency { get; set; }

    [JsonPropertyName("loanTermInDays")]
    public int LoanTermInDays { get; set; }

    [JsonPropertyName("totalPrincipalDisbursed")]
    public decimal TotalPrincipalDisbursed { get; set; }

    [JsonPropertyName("totalPrincipalExpected")]
    public decimal TotalPrincipalExpected { get; set; }

    [JsonPropertyName("totalInterestCharged")]
    public decimal TotalInterestCharged { get; set; }

    [JsonPropertyName("totalFeeChargesCharged")]
    public decimal TotalFeeChargesCharged { get; set; }

    [JsonPropertyName("totalRepaymentExpected")]
    public decimal TotalRepaymentExpected { get; set; }

    [JsonPropertyName("totalOutstanding")]
    public decimal TotalOutstanding { get; set; }

    [JsonPropertyName("periods")]
    public List<FineractSchedulePeriodDto>? Periods { get; set; }
}
