using System.Text.RegularExpressions;
using CRMS.Application.Advisory.Interfaces;
using CRMS.Domain.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.CustomerCheck;

/// <summary>
/// The pure rules of the company credit check: validation, registration-number handling, and the
/// mapping from the registry, business bureau and fraud results into the reply. No I/O.
/// </summary>
public static class CompanyCheckRules
{
    private static readonly Regex RegistrationNumber = new(@"^(RC|BN|IT)?(\d{1,10})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// A registration number as the bureaus want it: the prefix kept (upper-cased), separators
    /// dropped, and RC added to a bare number. Null when it is not a registration number at all.
    /// "RC 123456", "rc-123456" and "123456" all become "RC123456"; "BN 998877" stays a business name.
    /// </summary>
    public static (string Number, string Type)? NormaliseRegistrationNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var compact = new string(value.Where(c => !char.IsWhiteSpace(c) && c is not ('-' or '/' or '.' or ':')).ToArray());
        var match = RegistrationNumber.Match(compact);
        if (!match.Success) return null;
        var type = match.Groups[1].Success ? match.Groups[1].Value.ToUpperInvariant() : "RC";
        return (type + match.Groups[2].Value, type);
    }

    /// <summary>The spellings a case record may hold the same number under, for an exact-match
    /// search: with and without the prefix, and with the separators people type.</summary>
    public static IReadOnlyList<string> StoredSpellings(string number, string type)
    {
        var digits = number[type.Length..];
        return new[] { number, digits, $"{type} {digits}", $"{type}-{digits}", $"{type}/{digits}", $"{type.ToLowerInvariant()}{digits}" }
            .Distinct().ToList();
    }

    public static (CompanyCheckQuery? Query, List<string> Errors) Validate(CompanyCheckRequest? request, CustomerCheckSettings settings)
    {
        var errors = new List<string>();
        if (request is null)
        {
            errors.Add("A request body is required.");
            return (null, errors);
        }

        var requestId = request.RequestId?.Trim() ?? string.Empty;
        if (requestId.Length == 0)
            errors.Add("requestId is required.");
        else if (requestId.Length > 100)
            errors.Add("requestId must be 100 characters or fewer.");

        var rc = NormaliseRegistrationNumber(request.RcNumber);
        if (string.IsNullOrWhiteSpace(request.RcNumber))
            errors.Add("rcNumber is required.");
        else if (rc is null)
            errors.Add("rcNumber must be a registration number such as RC123456, BN123456 or IT123456.");

        var name = request.CompanyName?.Trim() ?? string.Empty;
        if (name.Length == 0)
            errors.Add("companyName is required.");
        else if (name.Length > 200)
            errors.Add("companyName must be 200 characters or fewer.");

        var nubans = new List<string>();
        if (request.Nubans is { Count: > 0 })
        {
            if (request.Nubans.Count > settings.MaxNubans)
                errors.Add($"nubans holds at most {settings.MaxNubans} entries.");
            for (var i = 0; i < request.Nubans.Count; i++)
            {
                var n = Identifiers.Digits(request.Nubans[i]);
                if (n is null || n.Length != 10 || !n.All(char.IsAsciiDigit))
                    errors.Add($"nubans[{i}] must be 10 digits.");
                else if (!nubans.Contains(n))
                    nubans.Add(n);
            }
        }

        var sections = new List<string>();
        if (request.Sections is null || request.Sections.Count == 0)
            sections.AddRange(CompanyCheckSections.All);
        else
            for (var i = 0; i < request.Sections.Count; i++)
            {
                var canonical = CompanyCheckSections.Canonical(request.Sections[i]);
                if (canonical is null)
                    errors.Add($"sections[{i}] is not a known section. Known: {string.Join(", ", CompanyCheckSections.All)}.");
                else if (!sections.Contains(canonical))
                    sections.Add(canonical);
            }

        var options = request.Options;
        var window = options?.ActivityWindowMonths ?? settings.DefaultActivityWindowMonths;
        if (window < 1 || window > settings.MaxActivityWindowMonths)
            errors.Add($"options.activityWindowMonths must be between 1 and {settings.MaxActivityWindowMonths}.");
        if (options?.LoanAmount is <= 0)
            errors.Add("options.loanAmount must be greater than zero.");
        if (options?.TenorMonths is < 1 or > 120)
            errors.Add("options.tenorMonths must be between 1 and 120.");
        if (options?.AnnualRevenue is < 0)
            errors.Add("options.annualRevenue cannot be negative.");
        var purpose = options?.LoanPurpose?.Trim();
        if (purpose is { Length: > 200 })
            errors.Add("options.loanPurpose must be 200 characters or fewer.");

        if (errors.Count > 0)
            return (null, errors);

        return (new CompanyCheckQuery(
            requestId, rc!.Value.Number, rc.Value.Type, name, nubans, sections, window,
            options?.RunAmlCheck ?? false, options?.LoanAmount, options?.TenorMonths, options?.AnnualRevenue,
            string.IsNullOrWhiteSpace(purpose) ? null : purpose), errors);
    }

    /// <summary>The company history search: the individual rules for dates, status, paging and
    /// request id, scoped to company checks, with an optional registration number matched on its
    /// keyed hash.</summary>
    public static (CustomerCheckJobFilter? Filter, List<string> Errors) ValidateHistory(
        CompanyCheckHistoryRequest request, Guid consumerId, Func<string, string> hash, CustomerCheckSettings settings)
    {
        var (filter, errors) = CustomerCheckRules.ValidateHistory(
            new CustomerCheckHistoryRequest(request.From, request.To, request.Status, request.RequestId, null, request.Page, request.PageSize),
            consumerId, hash, settings);

        string? rcHash = null;
        if (!string.IsNullOrWhiteSpace(request.RcNumber))
        {
            var rc = NormaliseRegistrationNumber(request.RcNumber);
            if (rc is null)
                errors.Add("rcNumber must be a registration number such as RC123456, BN123456 or IT123456.");
            else
                rcHash = hash(HashInput(rc.Value.Number));
        }

        if (filter is null || errors.Count > 0)
            return (null, errors);
        return (filter with { BvnHash = rcHash, SubjectType = Domain.Aggregates.CustomerCheck.CustomerCheckJob.Company }, errors);
    }

    /// <summary>What a company is hashed under, in the job table and the staff checks alike.
    /// Prefixed so it can never collide with a BVN's hash.</summary>
    public static string HashInput(string rcNumber) => "RC:" + rcNumber;

    // ── Mapping ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>The registry's "not found" answer, as the provider words it.</summary>
    public static bool IsRegistryNoRecord(string? error) =>
        error is not null && (error.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || error.Contains("no record", StringComparison.OrdinalIgnoreCase)
            || error.Contains("does not exist", StringComparison.OrdinalIgnoreCase));

    public static CompanyRegistrationSection MapRegistration(SmartComplyCacResult r, string requestedName)
    {
        var affiliates = r.Directors
            .Select(d => new CompanyAffiliate(
                Name: d.FullName ?? string.Join(" ", new[] { d.FirstName, d.OtherName, d.Surname }.Where(p => !string.IsNullOrWhiteSpace(p))),
                Role: Blank(d.AffiliateType),
                Status: Blank(d.Status),
                IsChairman: d.IsChairman == true,
                IsCorporate: d.IsCorporate == true,
                AppointedOn: CustomerCheckRules.ParseDate(d.DateOfAppointment),
                Nationality: Blank(d.Nationality),
                Occupation: Blank(d.Occupation),
                Shares: d.NumSharesAlloted,
                TypeOfShares: Blank(d.TypeOfShares)))
            .Where(a => !string.IsNullOrWhiteSpace(a.Name))
            .OrderByDescending(a => a.IsChairman)
            .ThenBy(a => a.Role)
            .ThenBy(a => a.Name)
            .ToList();

        bool? nameMatches = string.IsNullOrWhiteSpace(r.CompanyName) ? null : SameName(r.CompanyName, requestedName);
        return new CompanyRegistrationSection(
            SectionStatuses.Ok,
            nameMatches == false ? $"The registered name is \"{r.CompanyName}\", not the name given." : null,
            "SmartComply CAC", Blank(r.CompanyName), Blank(r.RcNumber), Blank(r.CompanyType), Blank(r.Status),
            CustomerCheckRules.ParseDate(r.RegistrationDate),
            string.Join(", ", new[] { r.Address, r.City }.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim())) is { Length: > 0 } a ? a : null,
            Blank(r.State), Blank(r.Email), Blank(r.NatureOfBusiness), r.ShareCapital, nameMatches, affiliates);
    }

    /// <summary>Registered names differ in punctuation and in "Limited"/"Ltd"; compare on letters and
    /// digits with the common suffixes reduced.</summary>
    public static bool SameName(string a, string b) => Key(a) == Key(b);

    private static string Key(string name)
    {
        var lower = " " + name.ToLowerInvariant() + " ";
        foreach (var (from, to) in new[] { (" limited ", " ltd "), (" plc ", " plc "), (" company ", " co "), (" nigeria ", " nig "), (" enterprises ", " ent "), ("&", " and ") })
            lower = lower.Replace(from, to);
        return new string(lower.Where(char.IsLetterOrDigit).ToArray());
    }

    public static CreditBureauSection MapBusinessBureau(SmartComplyBusinessCreditReport report, string bureau, DateTime reportDate, IReadOnlyList<string> skipped)
    {
        var s = report.Summary;
        var summary = new BureauSummary(
            s.TotalNoOfLoans, s.TotalNoOfInstitutions, s.TotalNoOfActiveLoans, s.TotalNoOfClosedLoans,
            s.TotalNoOfPerformingLoans, s.TotalNoOfDelinquentFacilities, s.HighestLoanAmount, s.TotalBorrowed,
            s.TotalOutstanding, s.TotalOverdue, s.MaxNoOfDays, "NGN");

        var history = report.LoanHistory ?? [];
        var performance = report.LoanPerformance ?? [];
        var loans = new List<BureauLoan>();
        var seen = new HashSet<string>();
        foreach (var h in history)
        {
            seen.Add($"{h.LoanProvider}|{h.AccountNumber}");
            var perf = performance.FirstOrDefault(p => p.LoanProvider == h.LoanProvider && p.AccountNumber == h.AccountNumber);
            loans.Add(new BureauLoan(
                h.LoanProvider, Last4Or(h.AccountNumber), h.Type, h.LoanAmount, h.OutstandingBalance, h.OverdueAmount,
                CustomerCheckRules.BureauLoanStatus(h.Status ?? perf?.Status, h.PerformanceStatus ?? perf?.PerformanceStatus, h.OverdueAmount, h.OutstandingBalance),
                h.PerformanceStatus ?? perf?.PerformanceStatus ?? h.Status,
                CustomerCheckRules.Date(h.DisbursedDate), CustomerCheckRules.Date(h.MaturityDate), CustomerCheckRules.Date(h.LastPaymentDate),
                h.LoanDuration, perf?.RepaymentFrequency));
        }
        foreach (var p in performance)
        {
            if (!seen.Add($"{p.LoanProvider}|{p.AccountNumber}")) continue;
            loans.Add(new BureauLoan(
                p.LoanProvider, Last4Or(p.AccountNumber), p.Type, p.LoanAmount, p.OutstandingBalance, p.OverdueAmount,
                CustomerCheckRules.BureauLoanStatus(p.Status, p.PerformanceStatus, p.OverdueAmount, p.OutstandingBalance),
                p.PerformanceStatus ?? p.Status, CustomerCheckRules.Date(p.DateAccountOpened), null, null, p.LoanDuration, p.RepaymentFrequency));
        }

        // Business reports carry no credit score and no enquiry list.
        return new CreditBureauSection(
            SectionStatuses.Ok, null, Bureaus.Label(bureau), skipped.Select(Bureaus.Label).ToList(), false, reportDate,
            null, summary, loans, []);
    }

    // ── Advisory ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>The company's figures in the advisory's shape. The identity slot carries "CAC record
    /// found and active" where a person's carries "BVN verified"; there is no watch-list for a company.</summary>
    public static CustomerAdvisoryRequest AdvisoryInput(
        Guid reference, CompanyCheckQuery query, CustomerCheckSettings settings,
        CompanyRegistrationSection registration, CreditBureauSection bureau, FraudCheckSection fraud,
        BankAccountsSection accounts, BankLoansSection loans, CrmsFacilitiesSection crms,
        CustomerCashflowInput? cashflow, DateTime now)
    {
        CustomerBureauInput? bureauInput = null;
        if (bureau.Status == SectionStatuses.Ok && bureau.Summary is { } s)
            bureauInput = new CustomerBureauInput(
                bureau.Bureau ?? "bureau", false, null, s.TotalLoans, s.ActiveLoans, s.ClosedLoans,
                s.PerformingLoans, s.DelinquentLoans, bureau.Loans.Count(l => l.Status == CheckLoanStatuses.WrittenOff),
                s.TotalOutstanding, s.TotalOverdue, s.MaxDaysOverdue, bureau.RecentEnquiries.Count);

        var bankKnown = loans.Status is SectionStatuses.Ok or SectionStatuses.NoRecord;
        var open = loans.Loans.Where(l => l.Status is CheckLoanStatuses.Running or CheckLoanStatuses.Overdue).ToList();
        var exposure = new CustomerExposureInput(
            bankKnown,
            loans.Loans.Count(l => l.Status == CheckLoanStatuses.Running),
            loans.Loans.Count(l => l.Status == CheckLoanStatuses.Overdue),
            loans.Loans.Count(l => l.Status == CheckLoanStatuses.Paid),
            loans.Loans.Count(l => l.Status == CheckLoanStatuses.WrittenOff),
            open.Sum(l => l.Outstanding ?? 0), open.Sum(l => l.OverdueAmount ?? 0), open.Max(l => (int?)l.DaysPastDue) ?? 0,
            open.Sum(l => l.NextDueAmount ?? 0),
            crms.Summary?.Direct ?? 0, crms.Summary?.Indirect ?? 0, crms.Summary?.ActiveOutstanding);

        var registered = registration.Status == SectionStatuses.NoRecord ? false : registration.IsActive;
        var identity = new CustomerIdentityInput(
            registered, null,
            fraud.Status == SectionStatuses.Ok ? fraud.RiskScore : null,
            fraud.Status == SectionStatuses.Ok ? fraud.RiskBand : null,
            accounts.Status == SectionStatuses.Ok ? true : accounts.Status == SectionStatuses.NoRecord ? false : null,
            null,
            accounts.CustomerSince is { } since ? CustomerCheckRules.MonthsBetween(since.ToDateTime(TimeOnly.MinValue), now) : null);

        var supplied = query.LoanAmount is > 0 && query.TenorMonths is > 0;
        return new CustomerAdvisoryRequest(
            reference, supplied ? query.LoanAmount!.Value : settings.FraudDefaultLoanAmount,
            supplied ? query.TenorMonths!.Value : settings.FraudDefaultTenorMonths, supplied,
            bureauInput, cashflow, exposure, identity, IsCompany: true);
    }

    // ── Warnings and coverage ────────────────────────────────────────────────────────────────────

    public static List<string> Warnings(CompanyCheckResponse r)
    {
        var w = new List<string>();
        if (r.AiAdvisory.Status == SectionStatuses.Ok && r.AiAdvisory.Recommendation == "Decline")
            w.Add("The advisory recommends declining.");
        if (r.CompanyRegistration.Status == SectionStatuses.NoRecord)
            w.Add("The Corporate Affairs Commission holds no company with this registration number.");
        if (r.CompanyRegistration.IsActive == false)
            w.Add($"The company is not active at the Corporate Affairs Commission ({r.CompanyRegistration.RegistrationStatus}).");
        if (r.CompanyRegistration.NameMatches == false)
            w.Add($"The registered name is \"{r.CompanyRegistration.CompanyName}\", which differs from the name given.");
        if (r.CreditBureau.Status == SectionStatuses.Ok && r.CreditBureau.Summary is { } s)
        {
            if (s.DelinquentLoans > 0)
                w.Add($"{s.DelinquentLoans} bureau facilit{(s.DelinquentLoans == 1 ? "y is" : "ies are")} delinquent.");
            if (s.MaxDaysOverdue > 0 && r.CreditBureau.Loans.Any(l => l.Status == CheckLoanStatuses.Overdue))
                w.Add($"A bureau loan is {s.MaxDaysOverdue} days overdue.");
            if (r.CreditBureau.Loans.Any(l => l.Status == CheckLoanStatuses.WrittenOff))
                w.Add("A bureau loan has been written off.");
        }
        if (r.FraudCheck.RiskBand == "HIGH")
            w.Add("The fraud check rates this company as high risk.");
        foreach (var l in r.BankLoans.Loans.Where(l => l.Status == CheckLoanStatuses.Overdue))
            w.Add($"Bank loan ****{l.LoanAccountLast4} is {l.DaysPastDue} days past due.");
        if (r.BankAccounts.Reason?.Contains("registered to a different", StringComparison.OrdinalIgnoreCase) == true)
            w.Add("An account number given belongs to a different company. Nothing from it was returned.");
        return w;
    }

    public static Dictionary<string, string> Coverage(CompanyCheckResponse r)
    {
        var c = new Dictionary<string, string>();
        c[CompanyCheckSections.CompanyRegistration] = r.CompanyRegistration.Status switch
        {
            SectionStatuses.Ok => "The company's record at the Corporate Affairs Commission, with the officers and shareholders it lists.",
            SectionStatuses.NoRecord => "The registry holds no company with this number.",
            SectionStatuses.Unavailable => "The registry did not answer. Nothing is known about the registration.",
            _ => "Not run.",
        };
        c[CompanyCheckSections.CreditBureau] = r.CreditBureau.Status switch
        {
            SectionStatuses.Ok when r.CreditBureau.BureausSkipped.Count > 0 => $"{r.CreditBureau.Bureau} answered after {string.Join(" and ", r.CreditBureau.BureausSkipped)} had no record or failed.",
            SectionStatuses.Ok => $"{r.CreditBureau.Bureau} business report. Business reports carry no credit score.",
            SectionStatuses.NoRecord => "No bureau holds a business record for this registration number.",
            SectionStatuses.Unavailable => "No bureau answered. Nothing is known from the bureaus.",
            _ => "Not run.",
        };
        c[CompanyCheckSections.FraudCheck] = r.FraudCheck.Status == SectionStatuses.Ok
            ? (r.FraudCheck.FinancialAnalysis?.Assessed == true
                ? "Overall score and financial analysis against the amount, tenor and revenue given."
                : "Overall score only. The financial analysis needs a loan amount, tenor and annual revenue.")
            : "Not run.";
        c[CompanyCheckSections.BankAccounts] = r.BankAccounts.Status switch
        {
            SectionStatuses.Ok => "The accounts of the company customer behind the account numbers given. Core banking finds a company only by account number.",
            SectionStatuses.NoRecord => "Core banking holds no customer behind the account numbers given.",
            SectionStatuses.NotPossible => "No account number was given, and core banking cannot find a company by registration number.",
            SectionStatuses.Unavailable => "Core banking did not answer. Nothing is known about accounts.",
            _ => "Not run.",
        };
        c[CompanyCheckSections.BankLoans] = r.BankLoans.Status switch
        {
            SectionStatuses.Ok => "Loans booked in core banking for the company customer. Days past due is computed from the repayment schedule.",
            SectionStatuses.NoRecord => "Core banking holds no customer behind the account numbers given.",
            SectionStatuses.NotPossible => "No account number was given, so loans with the bank could not be looked up.",
            SectionStatuses.Unavailable => "Core banking did not answer. Nothing is known about loans.",
            _ => "Not run.",
        };
        c[CompanyCheckSections.CrmsFacilities] = "CRMS cases (Corporate, NAMP, RH-SHF) recorded against this registration number. Balances are not read live.";
        c[CompanyCheckSections.AiAdvisory] = r.AiAdvisory.Status switch
        {
            SectionStatuses.Ok => r.AiAdvisory.Basis ?? "Scored from the other sections.",
            SectionStatuses.Unavailable => "No advisory. " + (r.AiAdvisory.Reason ?? ""),
            _ => "Not run.",
        };
        return c;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Last4Or(string? account) => string.IsNullOrWhiteSpace(account) ? null : CustomerCheckRules.Last4(account.Trim());
}
