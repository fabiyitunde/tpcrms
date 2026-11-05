namespace CRMS.Application.CustomerCheck;

// The customer check: what is known about a person by BVN, for third-party systems doing credit
// checks on the bank's behalf. The contract is docs/Customer_Check.md. Every section is always
// present and carries its own status; nothing the CRMS cannot supply is faked.

public static class CustomerCheckSections
{
    public const string Identity = "identity";
    public const string CreditBureau = "creditBureau";
    public const string FraudCheck = "fraudCheck";
    public const string BankAccounts = "bankAccounts";
    public const string BankLoans = "bankLoans";
    public const string CrmsFacilities = "crmsFacilities";
    public const string AiAdvisory = "aiAdvisory";

    public static readonly IReadOnlyList<string> All =
        [Identity, CreditBureau, FraudCheck, BankAccounts, BankLoans, CrmsFacilities, AiAdvisory];

    /// <summary>Canonical spelling for a name given in any case; null when unknown.</summary>
    public static string? Canonical(string? name) =>
        All.FirstOrDefault(s => string.Equals(s, name?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class SectionStatuses
{
    public const string Ok = "OK";
    public const string NoRecord = "NO_RECORD";
    public const string Unavailable = "UNAVAILABLE";
    public const string Refused = "REFUSED";
    public const string NotRequested = "NOT_REQUESTED";
    public const string NotAllowed = "NOT_ALLOWED";
    public const string NotPossible = "NOT_POSSIBLE";
}

public static class CheckLoanStatuses
{
    public const string Running = "RUNNING";
    public const string Overdue = "OVERDUE";
    public const string Paid = "PAID";
    public const string WrittenOff = "WRITTEN_OFF";
    public const string InProcess = "IN_PROCESS";
    public const string Unknown = "UNKNOWN";
}

public static class CheckAccountStatuses
{
    public const string Active = "ACTIVE";
    public const string Dormant = "DORMANT";
    public const string Closed = "CLOSED";
    public const string Inactive = "INACTIVE";
    public const string Unknown = "UNKNOWN";
}

public static class Bureaus
{
    public const string FirstCentral = "FirstCentral";
    public const string Crc = "CRC";
    public const string CreditRegistry = "CreditRegistry";

    public static readonly IReadOnlyList<string> Known = [FirstCentral, Crc, CreditRegistry];

    /// <summary>The name used in the reply.</summary>
    public static string Label(string bureau) => bureau switch
    {
        FirstCentral => "FIRST_CENTRAL",
        Crc => "CRC",
        CreditRegistry => "CREDIT_REGISTRY",
        _ => bureau.ToUpperInvariant(),
    };

    public static string? Canonical(string? name) =>
        Known.FirstOrDefault(b => string.Equals(b, name?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class BvnMatchResults
{
    public const string Match = "MATCH";
    public const string Mismatch = "MISMATCH";
    public const string NotOnRecord = "NOT_ON_RECORD";
}

// ── Settings ─────────────────────────────────────────────────────────────────────────────────────

public class CustomerCheckSettings
{
    public const string SectionName = "CustomerCheck";

    /// <summary>Bureaus in fallback order. A bureau that errors is skipped; one that answers "no
    /// record" ends the search.</summary>
    public List<string> BureauOrder { get; set; } = [Bureaus.FirstCentral, Bureaus.Crc, Bureaus.CreditRegistry];
    public int BureauTimeoutMs { get; set; } = 25_000;
    /// <summary>Per core banking call: the customer search, the account list, each loan detail,
    /// each statement.</summary>
    public int CoreBankingTimeoutMs { get; set; } = 20_000;
    /// <summary>Cap on the whole bank section. A customer with many loans is read until it runs
    /// out; what was not reached is reported without detail.</summary>
    public int BankSectionTimeoutMs { get; set; } = 120_000;
    public int CrmsLookupTimeoutMs { get; set; } = 10_000;
    /// <summary>The fraud check scores a proposed loan. These stand in when the request has none.</summary>
    public decimal FraudDefaultLoanAmount { get; set; } = 100_000m;
    public int FraudDefaultTenorMonths { get; set; } = 12;
    public int MaxNubans { get; set; } = 5;
    /// <summary>Statements are read for at most this many of the customer's accounts per check:
    /// the ones the request named first, then active ones, then by balance.</summary>
    public int MaxActivityAccounts { get; set; } = 5;
    public int DefaultActivityWindowMonths { get; set; } = 12;
    public int MaxActivityWindowMonths { get; set; } = 24;
    public int OverallLookupsPerMinute { get; set; } = 300;
    /// <summary>Cap on the advisory, scoring and narrative together.</summary>
    public int AdvisoryTimeoutMs { get; set; } = 60_000;
    /// <summary>Key for the audit and reuse hashes. Falls back to the RH-SHF callback signing
    /// secret when blank, so TEST works with what is already configured.</summary>
    public string HashKey { get; set; } = string.Empty;

    // ── Jobs and callbacks ───────────────────────────────────────────────────────────────────────

    /// <summary>Base64 of 32 random bytes: the AES key for stored requests, results and callback
    /// secrets. Derived from <see cref="HashKey"/> when blank; production must set it.</summary>
    public string DataKey { get; set; } = string.Empty;
    /// <summary>Checks run at the same time by one API instance.</summary>
    public int WorkerConcurrency { get; set; } = 4;
    /// <summary>Cap on one whole check, all sections.</summary>
    public int JobTimeoutMs { get; set; } = 300_000;
    /// <summary>A job PROCESSING for longer than this was interrupted and is claimed again.</summary>
    public int StaleProcessingMinutes { get; set; } = 10;
    /// <summary>Runs an interrupted job gets before it is failed.</summary>
    public int MaxRunAttempts { get; set; } = 2;
    /// <summary>A repeated requestId inside this window returns the existing job.</summary>
    public int IdempotencyWindowHours { get; set; } = 24;
    /// <summary>Per callback attempt.</summary>
    public int CallbackTimeoutMs { get; set; } = 15_000;
    /// <summary>Refuse a callback URL that is not https. Off only on a local or test box.</summary>
    public bool RequireHttpsCallbacks { get; set; } = true;
    /// <summary>How long the worker sleeps between looks at the queue when nothing nudges it.</summary>
    public int WorkerPollSeconds { get; set; } = 5;
    public int HistoryMaxPageSize { get; set; } = 100;
}

// ── Request ──────────────────────────────────────────────────────────────────────────────────────

/// <summary>A request for one customer check.</summary>
/// <param name="RequestId">Your own id for this check, up to 100 characters, unique per consumer. Required. It is the idempotency key: the same id sent again within 24 hours returns the existing job instead of running a second check.</param>
/// <param name="Bvn">The customer's BVN, 11 digits. Required. Spaces, hyphens and dots are ignored.</param>
/// <param name="Nubans">Optional. Up to 5 of the customer's account numbers, 10 digits each, to read transaction activity for first. Accounts are found by BVN, so none is needed.</param>
/// <param name="Sections">Optional. The sections to run: <c>identity</c>, <c>creditBureau</c>, <c>fraudCheck</c>, <c>bankAccounts</c>, <c>bankLoans</c>, <c>crmsFacilities</c>, <c>aiAdvisory</c>. Defaults to all seven. Bureau, fraud and advisory calls are billed, so ask only for what you need.</param>
/// <param name="Options">Optional settings for the check.</param>
/// <param name="CallbackUrl">Optional. Where to post the result. Must be on a host the bank has allowed for you, and <c>https</c> in production. When absent, your registered callback URL is used; when neither exists, fetch the result from the status URL.</param>
public record CustomerCheckRequest(
    string? RequestId,
    string? Bvn,
    List<string>? Nubans,
    List<string>? Sections,
    CustomerCheckOptions? Options,
    string? CallbackUrl = null);

/// <summary>Optional settings for a check.</summary>
/// <param name="ActivityWindowMonths">How many months of transaction activity to read on the accounts, 1 to 24. Default 12.</param>
/// <param name="IncludeScore">Also fetch the bureau's credit score, a separate billed call. Default true.</param>
/// <param name="RunAmlCheck">Add an AML screen to the fraud check. Default false.</param>
/// <param name="LoanAmount">The loan being considered, in Naira. Together with <c>tenorMonths</c> it is what the fraud check and the advisory's repayment capacity are measured against. Without both, a nominal amount and tenor stand in and the advisory says so.</param>
/// <param name="TenorMonths">The proposed tenor in months, 1 to 120.</param>
public record CustomerCheckOptions(
    int? ActivityWindowMonths,
    bool? IncludeScore,
    bool? RunAmlCheck,
    decimal? LoanAmount,
    int? TenorMonths);

/// <summary>A request after validation: identifiers are digits only and the right length, section
/// names canonical.</summary>
public record CustomerCheckQuery(
    string RequestId,
    string Bvn,
    IReadOnlyList<string> Nubans,
    IReadOnlyList<string> Sections,
    int ActivityWindowMonths,
    bool IncludeScore,
    bool RunAmlCheck,
    decimal? LoanAmount,
    int? TenorMonths,
    string? CallbackUrl = null)
{
    public bool Wants(string section) => Sections.Contains(section);
}

// ── Response ─────────────────────────────────────────────────────────────────────────────────────

/// <summary>The result of a check: one object per section, each with its own status, so one section failing never blanks another.</summary>
/// <param name="RequestId">The request id you sent.</param>
/// <param name="DataAsOf">When the check ran (UTC).</param>
/// <param name="Consumer">The consumer the check ran for.</param>
/// <param name="Subject">Who was checked, masked.</param>
/// <param name="SectionsRun">The sections that actually ran.</param>
/// <param name="Identity">Who the BVN belongs to.</param>
/// <param name="CreditBureau">What the credit bureau holds on the person.</param>
/// <param name="FraudCheck">The fraud risk assessment.</param>
/// <param name="BankAccounts">The person's accounts at the bank, with activity.</param>
/// <param name="BankLoans">The person's loans at the bank, with status.</param>
/// <param name="CrmsFacilities">Every CRMS case the person is tied to, in any role.</param>
/// <param name="AiAdvisory">The advisory: scores, rating, recommendation and narrative.</param>
/// <param name="Coverage">Per section, one sentence on what the answer does and does not cover.</param>
/// <param name="Warnings">Things worth a second look, in plain words.</param>
public record CustomerCheckResponse(
    string RequestId,
    DateTime DataAsOf,
    string Consumer,
    CheckSubject Subject,
    IReadOnlyList<string> SectionsRun,
    IdentitySection Identity,
    CreditBureauSection CreditBureau,
    FraudCheckSection FraudCheck,
    BankAccountsSection BankAccounts,
    BankLoansSection BankLoans,
    CrmsFacilitiesSection CrmsFacilities,
    AiAdvisorySection AiAdvisory,
    IReadOnlyDictionary<string, string> Coverage,
    IReadOnlyList<string> Warnings);

/// <summary>The person checked, masked.</summary>
/// <param name="BvnLast4">The last four digits of the BVN.</param>
/// <param name="NubansChecked">The account numbers named in the request, masked to their last four digits.</param>
public record CheckSubject(string BvnLast4, IReadOnlyList<string> NubansChecked);

/// <summary>Who the BVN belongs to, from the identity provider.</summary>
/// <param name="Status">The section's outcome: <c>OK</c> (answered), <c>NO_RECORD</c> (the provider holds nothing for this person), <c>UNAVAILABLE</c> (the provider failed or did not answer in time), <c>NOT_REQUESTED</c> or <c>NOT_ALLOWED</c> (the consumer is not entitled to it).</param>
/// <param name="Reason">Why, when the status is not <c>OK</c>; otherwise a note on anything left out. Null when there is nothing to say.</param>
/// <param name="Source">Which verification answered.</param>
/// <param name="FullName">Name on the BVN record.</param>
/// <param name="DateOfBirth">Date of birth on the BVN record.</param>
/// <param name="PhoneNumber">Phone number on the BVN record, partly masked.</param>
/// <param name="EnrollmentBank">The bank the BVN was enrolled at.</param>
/// <param name="RegistrationDate">When the BVN was registered.</param>
/// <param name="WatchListed">True when the BVN is watch-listed.</param>
public record IdentitySection(
    string Status,
    string? Reason,
    string? Source,
    string? FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? PhoneNumber,
    string? StateOfResidence,
    string? EnrollmentBank,
    DateOnly? RegistrationDate,
    bool? WatchListed)
{
    public static IdentitySection Empty(string status, string? reason = null) =>
        new(status, reason, null, null, null, null, null, null, null, null, null);
}

/// <summary>What the credit bureau holds on the person. Bureaus are tried in the bank's configured order; one that errors is skipped, and one that answers "no record" ends the search.</summary>
/// <param name="Status">The section's outcome: <c>OK</c> (answered), <c>NO_RECORD</c> (the provider holds nothing for this person), <c>UNAVAILABLE</c> (the provider failed or did not answer in time), <c>NOT_REQUESTED</c> or <c>NOT_ALLOWED</c> (the consumer is not entitled to it).</param>
/// <param name="Reason">Why, when the status is not <c>OK</c>; otherwise a note on anything left out. Null when there is nothing to say.</param>
/// <param name="Bureau">The bureau that answered.</param>
/// <param name="BureausSkipped">Bureaus that failed before one answered.</param>
/// <param name="Reused">True when a stored report was served instead of billing a new call.</param>
/// <param name="ReportDate">The report's date. For a reused report, its original date.</param>
/// <param name="Score">The bureau's credit score, when asked for and available.</param>
/// <param name="Summary">Totals across the loans on the report.</param>
/// <param name="Loans">Each loan on the report.</param>
/// <param name="RecentEnquiries">Recent credit enquiries made about the person.</param>
public record CreditBureauSection(
    string Status,
    string? Reason,
    string? Bureau,
    IReadOnlyList<string> BureausSkipped,
    bool Reused,
    DateTime? ReportDate,
    BureauScore? Score,
    BureauSummary? Summary,
    IReadOnlyList<BureauLoan> Loans,
    IReadOnlyList<BureauEnquiry> RecentEnquiries)
{
    public static CreditBureauSection Empty(string status, string? reason = null, IReadOnlyList<string>? skipped = null) =>
        new(status, reason, null, skipped ?? [], false, null, null, null, [], []);
}

public record BureauScore(int Value, string? Band, string Source);

public record BureauSummary(
    int TotalLoans,
    int Institutions,
    int ActiveLoans,
    int ClosedLoans,
    int PerformingLoans,
    int DelinquentLoans,
    decimal HighestLoanAmount,
    decimal TotalBorrowed,
    decimal TotalOutstanding,
    decimal TotalOverdue,
    int MaxDaysOverdue,
    string Currency);

/// <summary>One loan on the bureau report.</summary>
/// <param name="Status"><c>RUNNING</c>, <c>OVERDUE</c>, <c>PAID</c>, <c>WRITTEN_OFF</c>, <c>IN_PROCESS</c> or <c>UNKNOWN</c>.</param>
/// <param name="Performance">The bureau's own word for how the loan is performing.</param>
/// <param name="AccountNumberLast4">The last four digits of the loan account.</param>
public record BureauLoan(
    string? Lender,
    string? AccountNumberLast4,
    string? Type,
    decimal Amount,
    decimal Outstanding,
    decimal Overdue,
    string Status,
    string? Performance,
    DateOnly? DisbursedDate,
    DateOnly? MaturityDate,
    DateOnly? LastPaymentDate,
    string? Tenor,
    string? RepaymentFrequency);

public record BureauEnquiry(string? Institution, string? Reason, DateOnly? Date);

/// <summary>The fraud risk assessment.</summary>
/// <param name="Status">The section's outcome: <c>OK</c> (answered), <c>NO_RECORD</c> (the provider holds nothing for this person), <c>UNAVAILABLE</c> (the provider failed or did not answer in time), <c>NOT_REQUESTED</c> or <c>NOT_ALLOWED</c> (the consumer is not entitled to it).</param>
/// <param name="Reason">Why, when the status is not <c>OK</c>; otherwise a note on anything left out. Null when there is nothing to say.</param>
/// <param name="RiskScore">0 to 100; higher is riskier.</param>
/// <param name="RiskBand"><c>LOW</c> (below 30), <c>MEDIUM</c> (30 to 69) or <c>HIGH</c> (70 and above).</param>
/// <param name="Recommendation">The provider's recommendation.</param>
/// <param name="AmlChecked">True when an AML screen was included.</param>
/// <param name="FinancialAnalysis">Sub-scores on the proposed loan; assessed only when a loan amount and tenor were supplied.</param>
public record FraudCheckSection(
    string Status,
    string? Reason,
    int? RiskScore,
    string? RiskBand,
    string? Recommendation,
    bool AmlChecked,
    FraudFinancialAnalysis? FinancialAnalysis)
{
    public static FraudCheckSection Empty(string status, string? reason = null) =>
        new(status, reason, null, null, null, false, null);
}

public record FraudFinancialAnalysis(bool Assessed, string? Reason, IReadOnlyDictionary<string, FraudRiskItem>? Items);

public record FraudRiskItem(int RiskScore, string? Observation);

/// <summary>The person's accounts at the bank, found by BVN.</summary>
/// <param name="Status">The section's outcome: <c>OK</c> (answered), <c>NO_RECORD</c> (the provider holds nothing for this person), <c>UNAVAILABLE</c> (the provider failed or did not answer in time), <c>NOT_REQUESTED</c> or <c>NOT_ALLOWED</c> (the consumer is not entitled to it).</param>
/// <param name="Reason">Why, when the status is not <c>OK</c>; otherwise a note on anything left out. Null when there is nothing to say.</param>
/// <param name="BvnMatch"><c>MATCH</c> whenever the section is <c>OK</c>: the customer was found by this BVN.</param>
/// <param name="ClientId">The customer's id in core banking.</param>
/// <param name="ClientStatus">The customer record's status.</param>
/// <param name="KycLevel">The customer's KYC level.</param>
/// <param name="CustomerSince">When the customer record was activated.</param>
/// <param name="Accounts">Every account under the customer.</param>
public record BankAccountsSection(
    string Status,
    string? Reason,
    string? BvnMatch,
    string? ClientId,
    string? ClientStatus,
    string? KycLevel,
    DateOnly? CustomerSince,
    IReadOnlyList<BankAccount> Accounts)
{
    public static BankAccountsSection Empty(string status, string? reason = null, string? bvnMatch = null) =>
        new(status, reason, bvnMatch, null, null, null, null, []);
}

/// <summary>One account at the bank.</summary>
/// <param name="AccountNumberLast4">The last four digits of the account number.</param>
/// <param name="AccountType">The kind of account: <c>SAVINGS</c>, <c>FIXED DEPOSIT</c>, <c>RECURRING DEPOSIT</c>.</param>
/// <param name="Status"><c>ACTIVE</c>, <c>DORMANT</c>, <c>CLOSED</c>, <c>INACTIVE</c> or <c>UNKNOWN</c>.</param>
/// <param name="RawStatus">Core banking's own status word.</param>
/// <param name="OpenedDate">When the account was opened.</param>
/// <param name="Balance">Current balance.</param>
/// <param name="LastTransaction">Date of the last transaction.</param>
/// <param name="Activity">Transaction activity over the window asked for; null when the statement was not read for this account.</param>
public record BankAccount(
    string AccountNumberLast4,
    string? ProductName,
    string AccountType,
    string Status,
    string? RawStatus,
    DateOnly? OpenedDate,
    decimal? Balance,
    string Currency,
    DateOnly? LastTransaction,
    AccountActivity? Activity);

/// <summary>An account's transaction activity over the window.</summary>
/// <param name="WindowMonths">The window read, in months.</param>
/// <param name="TransactionsInWindow">How many transactions fell in the window.</param>
/// <param name="CreditsInWindow">Total credited in the window.</param>
/// <param name="DebitsInWindow">Total debited in the window.</param>
/// <param name="MonthsSinceOpened">How long the account has existed, in months.</param>
public record AccountActivity(
    int WindowMonths,
    DateOnly? FirstTransaction,
    DateOnly? LastTransaction,
    int TransactionsInWindow,
    decimal CreditsInWindow,
    decimal DebitsInWindow,
    int? MonthsSinceOpened);

/// <summary>The person's loans booked at the bank.</summary>
/// <param name="Status">The section's outcome: <c>OK</c> (answered), <c>NO_RECORD</c> (the provider holds nothing for this person), <c>UNAVAILABLE</c> (the provider failed or did not answer in time), <c>NOT_REQUESTED</c> or <c>NOT_ALLOWED</c> (the consumer is not entitled to it).</param>
/// <param name="Reason">Why, when the status is not <c>OK</c>; otherwise a note on anything left out. Null when there is nothing to say.</param>
/// <param name="Loans">Each booked loan.</param>
public record BankLoansSection(string Status, string? Reason, IReadOnlyList<BankLoan> Loans)
{
    public static BankLoansSection Empty(string status, string? reason = null) => new(status, reason, []);
}

/// <summary>One loan at the bank.</summary>
/// <param name="LoanAccountLast4">The last four digits of the loan account.</param>
/// <param name="Principal">Amount disbursed.</param>
/// <param name="Outstanding">What is still owed.</param>
/// <param name="OverdueAmount">What is owed on instalments already due.</param>
/// <param name="DaysPastDue">Age in days of the oldest unpaid instalment.</param>
/// <param name="Status"><c>RUNNING</c>, <c>OVERDUE</c>, <c>PAID</c>, <c>WRITTEN_OFF</c>, <c>IN_PROCESS</c> or <c>UNKNOWN</c>.</param>
/// <param name="RawStatus">Core banking's own status word.</param>
/// <param name="NextDueDate">When the next instalment falls due.</param>
/// <param name="NextDueAmount">The next instalment.</param>
public record BankLoan(
    string LoanAccountLast4,
    string? ProductName,
    decimal Principal,
    decimal? Outstanding,
    decimal? OverdueAmount,
    int? DaysPastDue,
    string Status,
    string? RawStatus,
    DateOnly? DisbursedDate,
    DateOnly? MaturityDate,
    DateOnly? NextDueDate,
    decimal? NextDueAmount);

/// <summary>Every CRMS case the person is tied to, in any role, across the three loan tracks.</summary>
/// <param name="Status">The section's outcome: <c>OK</c> (answered), <c>NO_RECORD</c> (the provider holds nothing for this person), <c>UNAVAILABLE</c> (the provider failed or did not answer in time), <c>NOT_REQUESTED</c> or <c>NOT_ALLOWED</c> (the consumer is not entitled to it).</param>
/// <param name="Reason">Why, when the status is not <c>OK</c>; otherwise a note on anything left out. Null when there is nothing to say.</param>
/// <param name="MatchStatus">Whether the CRMS holds anything for the identifiers given.</param>
/// <param name="Summary">Counts and totals.</param>
/// <param name="Relationships">Each case and the role the person plays in it.</param>
public record CrmsFacilitiesSection(
    string Status,
    string? Reason,
    string? MatchStatus,
    CrmsFacilitySummary? Summary,
    IReadOnlyList<CrmsFacility> Relationships)
{
    public static CrmsFacilitiesSection Empty(string status, string? reason = null) => new(status, reason, null, null, []);
}

public record CrmsFacilitySummary(int Direct, int Indirect, decimal? ActiveOutstanding, bool BalancesComplete);

/// <summary>One CRMS case the person is tied to.</summary>
/// <param name="Role"><c>BORROWER</c>, <c>GUARANTOR</c>, <c>DIRECTOR_OR_OWNER</c>, <c>ACCOUNT_SIGNATORY</c>, <c>NEXT_OF_KIN_OR_REFEREE</c> or <c>OTHER</c>.</param>
/// <param name="Track">The loan track: Corporate, NAMP or RHSHF.</param>
/// <param name="BalanceSource">Where the outstanding figure came from.</param>
public record CrmsFacility(
    string Role,
    string Track,
    string CaseReference,
    string FacilityType,
    string Obligor,
    string Status,
    decimal? AmountApproved,
    decimal? GuaranteeAmount,
    decimal? Outstanding,
    int? DaysPastDue,
    string BalanceSource);

/// <summary>The advisory: a score out of 100 per category and overall, a rating, a recommendation,
/// the reasons, and a narrative. Categories the data could not support are listed as not assessed
/// and carry no score; the overall score is weighted over the assessed ones only.</summary>
/// <param name="Status">The section's outcome: <c>OK</c> (answered), <c>NO_RECORD</c> (the provider holds nothing for this person), <c>UNAVAILABLE</c> (the provider failed or did not answer in time), <c>NOT_REQUESTED</c> or <c>NOT_ALLOWED</c> (the consumer is not entitled to it).</param>
/// <param name="Reason">Why, when the status is not <c>OK</c>; otherwise a note on anything left out. Null when there is nothing to say.</param>
/// <param name="OverallScore">0 to 100, weighted over the assessed categories only; higher is better.</param>
/// <param name="OverallRating">Risk rating: <c>VeryLow</c>, <c>Low</c>, <c>Medium</c>, <c>High</c> or <c>VeryHigh</c>.</param>
/// <param name="Recommendation"><c>StrongApprove</c>, <c>Approve</c>, <c>ApproveWithConditions</c>, <c>Refer</c> or <c>Decline</c>.</param>
/// <param name="Categories">The four categories, each with its score and reasons; a category the data could not support is marked not assessed.</param>
/// <param name="RedFlags">The findings that weigh against lending.</param>
/// <param name="Conditions">Conditions the rules attach to lending.</param>
/// <param name="Narrative">The written explanation.</param>
/// <param name="Proposal">The loan the capacity assessment was measured against, and what the rules would lend.</param>
/// <param name="Basis">What the scores were computed from.</param>
/// <param name="NarrativeSource"><c>LLM</c> when the language model wrote the narrative, <c>TEMPLATE</c> when it was assembled from the rules' own reasons.</param>
/// <param name="ModelVersion">The advisory engine version.</param>
/// <param name="GeneratedAt">When the advisory was produced (UTC).</param>
public record AiAdvisorySection(
    string Status,
    string? Reason,
    decimal? OverallScore,
    string? OverallRating,
    string? Recommendation,
    IReadOnlyList<AdvisoryCategory> Categories,
    IReadOnlyList<string> RedFlags,
    IReadOnlyList<string> Conditions,
    AdvisoryNarrative? Narrative,
    AdvisoryProposal? Proposal,
    string? Basis,
    string? NarrativeSource,
    string? ModelVersion,
    DateTime? GeneratedAt)
{
    public static AiAdvisorySection Empty(string status, string? reason = null) =>
        new(status, reason, null, null, null, [], [], [], null, null, null, null, null, null);
}

/// <summary>One scored category of the advisory.</summary>
/// <param name="Category"><c>CreditHistory</c>, <c>RepaymentCapacity</c>, <c>CashflowStability</c> or <c>IdentityAndFraud</c>.</param>
/// <param name="Assessed">False when there was no data to score the category from.</param>
/// <param name="Score">0 to 100; null when not assessed.</param>
/// <param name="Weight">The category's weight in the overall score.</param>
/// <param name="Rating">Risk rating for the category.</param>
/// <param name="Rationale">What the score rests on; for an unassessed category, why it could not be scored.</param>
/// <param name="Positives">Findings in the customer's favour.</param>
/// <param name="Concerns">Findings against.</param>
public record AdvisoryCategory(
    string Category,
    bool Assessed,
    decimal? Score,
    decimal Weight,
    string? Rating,
    string Rationale,
    IReadOnlyList<string> Positives,
    IReadOnlyList<string> Concerns);

public record AdvisoryNarrative(string Summary, string Strengths, string Weaknesses, string? MitigatingFactors, string? KeyRisks);

/// <summary>What the capacity assessment was measured against, and what the rules would lend.</summary>
/// <param name="Amount">The loan amount capacity was measured against.</param>
/// <param name="TenorMonths">The tenor it was measured against.</param>
/// <param name="Supplied">True when the request supplied the amount and tenor; false when stand-ins were used.</param>
/// <param name="RecommendedAmount">What the rules would lend; present only when a proposal was supplied.</param>
/// <param name="RecommendedTenorMonths">The tenor the rules would allow.</param>
public record AdvisoryProposal(decimal Amount, int TenorMonths, bool Supplied, decimal? RecommendedAmount, int? RecommendedTenorMonths);

/// <summary>What the service hands back: the reply, plus what the audit log needs to know.</summary>
public record CustomerCheckOutcome(
    CustomerCheckResponse Response,
    string? BureauUsed,
    bool BureauReused,
    Domain.Aggregates.CustomerCheck.BilledCallCounts Billed)
{
    public int BilledCalls => Billed.Total;
}
