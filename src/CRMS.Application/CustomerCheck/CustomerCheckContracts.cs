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

public record CustomerCheckRequest(
    string? RequestId,
    string? Bvn,
    List<string>? Nubans,
    List<string>? Sections,
    CustomerCheckOptions? Options,
    string? CallbackUrl = null);

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

public record CheckSubject(string BvnLast4, IReadOnlyList<string> NubansChecked);

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

public record AccountActivity(
    int WindowMonths,
    DateOnly? FirstTransaction,
    DateOnly? LastTransaction,
    int TransactionsInWindow,
    decimal CreditsInWindow,
    decimal DebitsInWindow,
    int? MonthsSinceOpened);

public record BankLoansSection(string Status, string? Reason, IReadOnlyList<BankLoan> Loans)
{
    public static BankLoansSection Empty(string status, string? reason = null) => new(status, reason, []);
}

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
