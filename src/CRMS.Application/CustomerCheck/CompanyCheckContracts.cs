namespace CRMS.Application.CustomerCheck;

// The company credit check: what is known about a company by its registration number. Same
// conventions as the customer (individual) check: every section is always present and carries its
// own status, one section failing never blanks another, and nothing the CRMS cannot supply is faked.
// The bureau, fraud, bank and advisory sections reuse the individual check's shapes.

public static class CompanyCheckSections
{
    public const string CompanyRegistration = "companyRegistration";
    public const string CreditBureau = CustomerCheckSections.CreditBureau;
    public const string FraudCheck = CustomerCheckSections.FraudCheck;
    public const string BankAccounts = CustomerCheckSections.BankAccounts;
    public const string BankLoans = CustomerCheckSections.BankLoans;
    public const string CrmsFacilities = CustomerCheckSections.CrmsFacilities;
    public const string AiAdvisory = CustomerCheckSections.AiAdvisory;

    public static readonly IReadOnlyList<string> All =
        [CompanyRegistration, CreditBureau, FraudCheck, BankAccounts, BankLoans, CrmsFacilities, AiAdvisory];

    /// <summary>Canonical spelling for a name given in any case; null when unknown.</summary>
    public static string? Canonical(string? name) =>
        All.FirstOrDefault(s => string.Equals(s, name?.Trim(), StringComparison.OrdinalIgnoreCase));
}

// ── Request ──────────────────────────────────────────────────────────────────────────────────────

/// <summary>A request for one company credit check.</summary>
/// <param name="RequestId">Your own id for this check, up to 100 characters, unique per requester. Required.</param>
/// <param name="RcNumber">The company's registration number, with its prefix: <c>RC</c> (company), <c>BN</c> (business name) or <c>IT</c> (incorporated trustees), e.g. <c>RC123456</c>. A number with no prefix is read as <c>RC</c>. Required.</param>
/// <param name="CompanyName">The company's registered name. Required: the Corporate Affairs Commission lookup and the fraud check both need it.</param>
/// <param name="Nubans">Optional. Up to 5 of the company's account numbers at the bank, 10 digits each. Core banking finds a company only by account number, so without one the bank sections are not run.</param>
/// <param name="Sections">Optional. The sections to run: <c>companyRegistration</c>, <c>creditBureau</c>, <c>fraudCheck</c>, <c>bankAccounts</c>, <c>bankLoans</c>, <c>crmsFacilities</c>, <c>aiAdvisory</c>. Defaults to all seven.</param>
/// <param name="Options">Optional settings for the check.</param>
/// <param name="CallbackUrl">Optional. Where to post the result, as for the customer check.</param>
public record CompanyCheckRequest(
    string? RequestId,
    string? RcNumber,
    string? CompanyName,
    List<string>? Nubans,
    List<string>? Sections,
    CompanyCheckOptions? Options,
    string? CallbackUrl = null);

/// <summary>Optional settings for a company check.</summary>
/// <param name="ActivityWindowMonths">How many months of transaction activity to read on the accounts, 1 to 24. Default 12.</param>
/// <param name="RunAmlCheck">Add an AML screen to the fraud check. Default false.</param>
/// <param name="LoanAmount">The loan being considered, in Naira. With <c>tenorMonths</c> it is what the fraud check and the advisory's repayment capacity are measured against.</param>
/// <param name="TenorMonths">The proposed tenor in months, 1 to 120.</param>
/// <param name="AnnualRevenue">The company's annual revenue in Naira, if known. The fraud check's financial analysis uses it.</param>
/// <param name="LoanPurpose">What the loan is for, in a few words.</param>
public record CompanyCheckOptions(
    int? ActivityWindowMonths,
    bool? RunAmlCheck,
    decimal? LoanAmount,
    int? TenorMonths,
    decimal? AnnualRevenue,
    string? LoanPurpose);

/// <summary>A company request after validation: the registration number carries its prefix and no
/// separators, section names are canonical.</summary>
public record CompanyCheckQuery(
    string RequestId,
    string RcNumber,
    string CompanyType,
    string CompanyName,
    IReadOnlyList<string> Nubans,
    IReadOnlyList<string> Sections,
    int ActivityWindowMonths,
    bool RunAmlCheck,
    decimal? LoanAmount,
    int? TenorMonths,
    decimal? AnnualRevenue,
    string? LoanPurpose,
    string? CallbackUrl = null)
{
    public bool Wants(string section) => Sections.Contains(section);

    /// <summary>The number without its prefix, as the registry and most case records hold it.</summary>
    public string RcDigits => RcNumber[CompanyType.Length..];
}

// ── Response ─────────────────────────────────────────────────────────────────────────────────────

/// <summary>The result of a company check: one object per section, each with its own status.</summary>
/// <param name="RequestId">The request id you sent.</param>
/// <param name="DataAsOf">When the check ran (UTC).</param>
/// <param name="Requester">Who the check ran for.</param>
/// <param name="Subject">The company checked.</param>
/// <param name="SectionsRun">The sections that actually ran.</param>
/// <param name="CompanyRegistration">The company's record at the Corporate Affairs Commission, with its directors and shareholders.</param>
/// <param name="CreditBureau">What the credit bureau holds on the company.</param>
/// <param name="FraudCheck">The fraud risk assessment of the company.</param>
/// <param name="BankAccounts">The company's accounts at the bank, from the account numbers given.</param>
/// <param name="BankLoans">The company's loans at the bank.</param>
/// <param name="CrmsFacilities">Every CRMS case on this registration number.</param>
/// <param name="AiAdvisory">The advisory: scores, rating, recommendation and narrative.</param>
/// <param name="Coverage">Per section, one sentence on what the answer does and does not cover.</param>
/// <param name="Warnings">Things worth a second look, in plain words.</param>
public record CompanyCheckResponse(
    string RequestId,
    DateTime DataAsOf,
    string Requester,
    CompanySubject Subject,
    IReadOnlyList<string> SectionsRun,
    CompanyRegistrationSection CompanyRegistration,
    CreditBureauSection CreditBureau,
    FraudCheckSection FraudCheck,
    BankAccountsSection BankAccounts,
    BankLoansSection BankLoans,
    CrmsFacilitiesSection CrmsFacilities,
    AiAdvisorySection AiAdvisory,
    IReadOnlyDictionary<string, string> Coverage,
    IReadOnlyList<string> Warnings);

/// <summary>The company checked.</summary>
/// <param name="RcNumber">The registration number, with its prefix.</param>
/// <param name="CompanyName">The name given in the request.</param>
/// <param name="NubansChecked">The account numbers given, masked to their last four digits.</param>
public record CompanySubject(string RcNumber, string CompanyName, IReadOnlyList<string> NubansChecked);

/// <summary>The company's record at the Corporate Affairs Commission.</summary>
/// <param name="Status">The section's outcome: <c>OK</c>, <c>NO_RECORD</c> (the registry holds no such company), <c>UNAVAILABLE</c>, <c>NOT_REQUESTED</c> or <c>NOT_ALLOWED</c>.</param>
/// <param name="Reason">Why, when the status is not <c>OK</c>; otherwise a note. Null when there is nothing to say.</param>
/// <param name="RegistrationStatus">The registry's own status word for the company, e.g. ACTIVE.</param>
/// <param name="NameMatches">False when the registered name differs from the name given.</param>
/// <param name="Affiliates">Directors, shareholders and other officers on the record.</param>
public record CompanyRegistrationSection(
    string Status,
    string? Reason,
    string? Source,
    string? CompanyName,
    string? RcNumber,
    string? CompanyType,
    string? RegistrationStatus,
    DateOnly? RegistrationDate,
    string? Address,
    string? State,
    string? Email,
    string? NatureOfBusiness,
    decimal? ShareCapital,
    bool? NameMatches,
    IReadOnlyList<CompanyAffiliate> Affiliates)
{
    public static CompanyRegistrationSection Empty(string status, string? reason = null) =>
        new(status, reason, null, null, null, null, null, null, null, null, null, null, null, null, []);

    /// <summary>True when the registry reports the company as active; null when it was not read or
    /// gave no status.</summary>
    public bool? IsActive => Status != SectionStatuses.Ok || string.IsNullOrWhiteSpace(RegistrationStatus)
        ? null
        : RegistrationStatus.Contains("active", StringComparison.OrdinalIgnoreCase)
          && !RegistrationStatus.Contains("inactive", StringComparison.OrdinalIgnoreCase);
}

/// <summary>One person or company named on the registry record.</summary>
/// <param name="Role">The registry's word for the role, e.g. DIRECTOR, SHAREHOLDER, SECRETARY.</param>
/// <param name="IsCorporate">True when the affiliate is itself a company.</param>
/// <param name="Shares">Shares allotted, when the registry lists them.</param>
public record CompanyAffiliate(
    string Name,
    string? Role,
    string? Status,
    bool IsChairman,
    bool IsCorporate,
    DateOnly? AppointedOn,
    string? Nationality,
    string? Occupation,
    long? Shares,
    string? TypeOfShares);

/// <summary>A search of your past company checks. Every field is optional.</summary>
/// <param name="From">Checks queued on or after this date.</param>
/// <param name="To">Checks queued on or before this date.</param>
/// <param name="Status"><c>QUEUED</c>, <c>PROCESSING</c>, <c>COMPLETED</c> or <c>FAILED</c>.</param>
/// <param name="RequestId">An exact request id.</param>
/// <param name="RcNumber">A registration number, e.g. <c>RC123456</c>. Matched on its keyed hash.</param>
/// <param name="Page">Page number, from 1. Default 1.</param>
/// <param name="PageSize">Items per page, at most 100. Default 50.</param>
public record CompanyCheckHistoryRequest(
    DateOnly? From,
    DateOnly? To,
    string? Status,
    string? RequestId,
    string? RcNumber,
    int? Page,
    int? PageSize);

/// <summary>One past company check, without its result.</summary>
/// <param name="JobId">The job's id; fetch the result with it.</param>
/// <param name="RcNumberLast4">The last four digits of the registration number checked.</param>
public record CompanyCheckHistoryItem(
    string JobId,
    string RequestId,
    string Status,
    DateTime QueuedAt,
    DateTime? CompletedAt,
    IReadOnlyList<string> Sections,
    string RcNumberLast4,
    CustomerCheckCallbackState Callback);

/// <summary>A page of past company checks, newest first.</summary>
/// <param name="Total">How many checks match in all.</param>
public record CompanyCheckHistoryPage(int Page, int PageSize, int Total, IReadOnlyList<CompanyCheckHistoryItem> Items);

/// <summary>What the company check hands back: the reply, plus what billing and the history need.</summary>
public record CompanyCheckOutcome(
    CompanyCheckResponse Response,
    string? BureauUsed,
    Domain.Aggregates.CustomerCheck.BilledCallCounts Billed)
{
    public int BilledCalls => Billed.Total;
}
