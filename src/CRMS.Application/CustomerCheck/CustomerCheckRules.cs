using System.Globalization;
using System.Text.RegularExpressions;
using CRMS.Application.Advisory.Interfaces;
using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.CustomerCheck;

/// <summary>
/// The pure rules of the customer check: validation, the shared status words, masking, and the
/// mapping from each provider's result into the reply. No I/O, so every rule is unit-testable.
/// </summary>
public static class CustomerCheckRules
{
    // ── Validation ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Where this check's result goes: the request's own URL when it names one, else the
    /// consumer's registered URL, else nowhere (the consumer polls). A per-request URL must sit on
    /// a host the bank has allowed for the consumer, and be https unless the deployment says
    /// otherwise, so a leaked key cannot send a person's credit data to an arbitrary host.</summary>
    public static (string? Url, string? Error) ResolveCallback(string? requested, ApiConsumer consumer, bool requireHttps)
    {
        if (string.IsNullOrWhiteSpace(requested))
            return (consumer.CallbackUrl, null);

        var text = requested.Trim();
        if (text.Length > 500)
            return (null, "callbackUrl must be 500 characters or fewer.");
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return (null, "callbackUrl must be an absolute http or https URL.");
        if (requireHttps && uri.Scheme != Uri.UriSchemeHttps)
            return (null, "callbackUrl must use https.");
        if (!string.IsNullOrEmpty(uri.UserInfo))
            return (null, "callbackUrl must not carry credentials.");
        if (!consumer.AllowsCallbackHost(uri.Host))
            return (null, "callbackUrl is not on a host the CRMS allows for this consumer.");
        return (uri.ToString(), null);
    }

    private static readonly string[] JobStatuses =
    [
        CustomerCheckJob.Queued, CustomerCheckJob.Processing, CustomerCheckJob.Completed, CustomerCheckJob.Failed,
    ];

    /// <summary>The history search, checked and turned into a repository filter scoped to the
    /// consumer. A BVN filter is hashed here and never kept.</summary>
    public static (CustomerCheckJobFilter? Filter, List<string> Errors) ValidateHistory(
        CustomerCheckHistoryRequest request, Guid consumerId, Func<string, string> hash, CustomerCheckSettings settings)
    {
        var errors = new List<string>();

        DateTime? from = request.From?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime? to = request.To?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        if (from is not null && to is not null && from >= to)
            errors.Add("from must be on or before to.");

        string? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            status = JobStatuses.FirstOrDefault(s => string.Equals(s, request.Status.Trim(), StringComparison.OrdinalIgnoreCase));
            if (status is null)
                errors.Add($"status must be one of {string.Join(", ", JobStatuses)}.");
        }

        var requestId = string.IsNullOrWhiteSpace(request.RequestId) ? null : request.RequestId.Trim();
        if (requestId is { Length: > 100 })
            errors.Add("requestId must be 100 characters or fewer.");

        string? bvnHash = null;
        if (!string.IsNullOrWhiteSpace(request.Bvn))
        {
            var bvn = Identifiers.Digits(request.Bvn);
            if (bvn is null || bvn.Length != 11 || !bvn.All(char.IsAsciiDigit))
                errors.Add("bvn must be 11 digits.");
            else
                bvnHash = hash(bvn);
        }

        var page = request.Page ?? 1;
        if (page < 1)
            errors.Add("page must be 1 or more.");
        var pageSize = request.PageSize ?? Math.Min(50, settings.HistoryMaxPageSize);
        if (pageSize < 1 || pageSize > settings.HistoryMaxPageSize)
            errors.Add($"pageSize must be between 1 and {settings.HistoryMaxPageSize}.");

        if (errors.Count > 0)
            return (null, errors);
        return (new CustomerCheckJobFilter(consumerId, from, to, status, requestId, bvnHash, page, pageSize), errors);
    }

    public static (CustomerCheckQuery? Query, List<string> Errors) Validate(CustomerCheckRequest? request, CustomerCheckSettings settings)
    {
        var errors = new List<string>();
        if (request is null)
        {
            errors.Add("A request body is required.");
            return (null, errors);
        }

        // The request id is the idempotency key, so the caller must choose it.
        var requestId = request.RequestId?.Trim() ?? string.Empty;
        if (requestId.Length == 0)
            errors.Add("requestId is required.");
        else if (requestId.Length > 100)
            errors.Add("requestId must be 100 characters or fewer.");

        var bvn = Identifiers.Digits(request.Bvn);
        if (bvn is null)
            errors.Add("bvn is required.");
        else if (bvn.Length != 11 || !bvn.All(char.IsAsciiDigit))
            errors.Add("bvn must be 11 digits.");

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
            sections.AddRange(CustomerCheckSections.All);
        else
            for (var i = 0; i < request.Sections.Count; i++)
            {
                var canonical = CustomerCheckSections.Canonical(request.Sections[i]);
                if (canonical is null)
                    errors.Add($"sections[{i}] is not a known section. Known: {string.Join(", ", CustomerCheckSections.All)}.");
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

        if (errors.Count > 0)
            return (null, errors);

        return (new CustomerCheckQuery(
            requestId, bvn!, nubans, sections, window,
            options?.IncludeScore ?? true,
            options?.RunAmlCheck ?? false,
            options?.LoanAmount,
            options?.TenorMonths), errors);
    }

    // ── Masking ──────────────────────────────────────────────────────────────────────────────────

    public static string MaskLast4(string? value)
    {
        var digits = Identifiers.Digits(value) ?? string.Empty;
        return digits.Length >= 4 ? "****" + digits[^4..] : "****";
    }

    public static string Last4(string value) => value.Length >= 4 ? value[^4..] : value;

    /// <summary>Keeps the first four and last three digits of a phone number.</summary>
    public static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var p = phone.Trim();
        return p.Length <= 7 ? new string('*', p.Length) : p[..4] + new string('*', p.Length - 7) + p[^3..];
    }

    // ── Status words ─────────────────────────────────────────────────────────────────────────────

    /// <summary>One status word for a bureau loan, from the bureau's own status and performance
    /// words and the amounts. Overdue money wins over any word.</summary>
    public static string BureauLoanStatus(string? status, string? performance, decimal overdue, decimal outstanding)
    {
        var words = $"{status} {performance}".ToLowerInvariant();
        if (words.Contains("written") || words.Contains("write-off") || words.Contains("write off"))
            return CheckLoanStatuses.WrittenOff;
        if (overdue > 0)
            return CheckLoanStatuses.Overdue;
        if (ContainsAny(words, "delinquent", "lost", "doubtful", "substandard", "sub-standard", "non-performing", "nonperforming", "default", "overdue", "past due"))
            return CheckLoanStatuses.Overdue;
        if (ContainsAny(words, "closed", "paid", "settled", "fully repaid", "liquidated"))
            return CheckLoanStatuses.Paid;
        if (ContainsAny(words, "open", "active", "performing", "running", "current"))
            return CheckLoanStatuses.Running;
        if (outstanding > 0)
            return CheckLoanStatuses.Running;
        if (outstanding == 0 && !string.IsNullOrWhiteSpace(words.Trim()))
            return CheckLoanStatuses.Paid;
        return CheckLoanStatuses.Unknown;
    }

    /// <summary>Fineract loan status codes: 100 submitted, 200 approved, 300 active, 400 withdrawn,
    /// 500 rejected, 600 closed (obligations met), 601 written off, 602 rescheduled, 700 overpaid.</summary>
    public static string FineractLoanStatus(int statusCode, decimal? overdue) => statusCode switch
    {
        100 or 200 => CheckLoanStatuses.InProcess,
        300 => overdue > 0 ? CheckLoanStatuses.Overdue : CheckLoanStatuses.Running,
        600 or 700 => CheckLoanStatuses.Paid,
        601 => CheckLoanStatuses.WrittenOff,
        602 => CheckLoanStatuses.Paid,
        _ => CheckLoanStatuses.Unknown,
    };

    /// <summary>Withdrawn and rejected applications never became loans and are left out.</summary>
    public static bool IsBookedLoan(int statusCode) => statusCode is not (400 or 500);

    public static string SavingsStatus(string? raw)
    {
        var w = (raw ?? string.Empty).ToLowerInvariant();
        if (w.Contains("dormant")) return CheckAccountStatuses.Dormant;
        if (w.Contains("closed") || w.Contains("escheat") || w.Contains("withdrawn") || w.Contains("rejected")) return CheckAccountStatuses.Closed;
        // "inactive" contains "active", so it is checked first. A matured deposit no longer
        // transacts but may still hold a balance.
        if (w.Contains("pending") || w.Contains("submitted") || w.Contains("approved") || w.Contains("inactive") || w.Contains("matured")) return CheckAccountStatuses.Inactive;
        if (w.Contains("active")) return CheckAccountStatuses.Active;
        return CheckAccountStatuses.Unknown;
    }

    public static string RiskBand(int score) => score < 30 ? "LOW" : score < 70 ? "MEDIUM" : "HIGH";

    /// <summary>The provider reports "no record" as a failure with this wording (see
    /// SmartComplyProvider). It is an answer, not an error: the next bureau is not tried.</summary>
    public static bool IsNoRecord(string? error) =>
        error is not null && (error.Contains("not found in credit bureau", StringComparison.OrdinalIgnoreCase)
            || error.Contains("No credit data", StringComparison.OrdinalIgnoreCase));

    // ── Mapping ──────────────────────────────────────────────────────────────────────────────────

    public static CreditBureauSection MapBureau(
        SmartComplyIndividualCreditReport report, string bureau, bool reused, DateTime reportDate,
        SmartComplyCreditScore? score, IReadOnlyList<string> skipped)
    {
        var s = report.Summary;
        var summary = new BureauSummary(
            s.TotalNoOfLoans, s.TotalNoOfInstitutions, s.TotalNoOfActiveLoans, s.TotalNoOfClosedLoans,
            s.TotalNoOfPerformingLoans, s.TotalNoOfDelinquentFacilities, s.HighestLoanAmount, s.TotalBorrowed,
            s.TotalOutstanding, s.TotalOverdue, s.MaxNoOfDays, "NGN");

        // The history carries dates and the performance list carries status; the same facility
        // appears in both when the bureau has it. Merge on account number, history first.
        var loans = new List<BureauLoan>();
        var seen = new HashSet<string>();
        foreach (var h in report.LoanHistory)
        {
            var key = $"{h.LoanProvider}|{h.AccountNumber}";
            seen.Add(key);
            var perf = report.LoanPerformance.FirstOrDefault(p => p.LoanProvider == h.LoanProvider && p.AccountNumber == h.AccountNumber);
            loans.Add(new BureauLoan(
                h.LoanProvider, MaskLast4Or(h.AccountNumber), h.Type, h.LoanAmount, h.OutstandingBalance, h.OverdueAmount,
                BureauLoanStatus(h.Status ?? perf?.Status, h.PerformanceStatus ?? perf?.PerformanceStatus, h.OverdueAmount, h.OutstandingBalance),
                h.PerformanceStatus ?? perf?.PerformanceStatus ?? h.Status,
                Date(h.DisbursedDate), Date(h.MaturityDate), Date(h.LastPaymentDate), h.LoanDuration, perf?.RepaymentFrequency));
        }
        foreach (var p in report.LoanPerformance)
        {
            if (!seen.Add($"{p.LoanProvider}|{p.AccountNumber}")) continue;
            loans.Add(new BureauLoan(
                p.LoanProvider, MaskLast4Or(p.AccountNumber), p.Type, p.LoanAmount, p.OutstandingBalance, p.OverdueAmount,
                BureauLoanStatus(p.Status, p.PerformanceStatus, p.OverdueAmount, p.OutstandingBalance),
                p.PerformanceStatus ?? p.Status, Date(p.DateAccountOpened), null, null, p.LoanDuration, p.RepaymentFrequency));
        }

        var enquiries = report.CreditEnquiries
            .OrderByDescending(e => e.Date)
            .Take(10)
            .Select(e => new BureauEnquiry(e.LoanProvider, e.Reason, Date(e.Date)))
            .ToList();

        var scoreOut = score is null ? null : new BureauScore(score.Score, score.Grade, Bureaus.Label(bureau));

        return new CreditBureauSection(
            SectionStatuses.Ok, null, Bureaus.Label(bureau), skipped.Select(Bureaus.Label).ToList(), reused, reportDate,
            scoreOut, summary, loans, enquiries);
    }

    public static IdentitySection MapIdentity(SmartComplyBvnAdvancedResult r) => new(
        SectionStatuses.Ok, null, "SmartComply BVN Advanced",
        JoinName(r.FirstName, r.MiddleName, r.LastName), ParseDate(r.DateOfBirth), r.Gender,
        MaskPhone(r.PhoneNumber1), r.StateOfResidence, r.EnrollmentBank, ParseDate(r.RegistrationDate),
        ParseBool(r.WatchListed));

    public static IdentitySection MapIdentity(SmartComplyBvnResult r) => new(
        SectionStatuses.Ok, null, "SmartComply BVN",
        JoinName(r.FirstName, r.MiddleName, r.LastName), ParseDate(r.DateOfBirth), null,
        MaskPhone(r.PhoneNumber), null, null, null, null);

    public static FraudCheckSection MapFraud(SmartComplyLoanFraudResult r, bool amountAndTenorSupplied, bool amlChecked)
    {
        FraudFinancialAnalysis analysis;
        if (!amountAndTenorSupplied)
            analysis = new FraudFinancialAnalysis(false,
                "No loan amount, tenor or income supplied; defaults were used, so these sub-scores are not meaningful.", null);
        else if (r.FinancialAnalysis is null)
            analysis = new FraudFinancialAnalysis(false, "The provider returned no financial analysis.", null);
        else
        {
            var items = new Dictionary<string, FraudRiskItem>();
            Add(items, "incomeStability", r.FinancialAnalysis.IncomeStability);
            Add(items, "repaymentDuration", r.FinancialAnalysis.RepaymentDuration);
            Add(items, "collateralCoverage", r.FinancialAnalysis.CollateralCoverage);
            Add(items, "debtServiceability", r.FinancialAnalysis.DebtServiceability);
            Add(items, "debtToIncomeRatio", r.FinancialAnalysis.DebtToIncomeRatio);
            analysis = new FraudFinancialAnalysis(true,
                "Scored against the amount and tenor supplied. Income is not known to the CRMS, so income-based items are indicative only.", items);
        }
        return new FraudCheckSection(SectionStatuses.Ok, null, r.FraudRiskScore, RiskBand(r.FraudRiskScore), r.Recommendation, amlChecked, analysis);

        static void Add(Dictionary<string, FraudRiskItem> d, string key, SmartComplyRiskItem? item)
        {
            if (item is not null) d[key] = new FraudRiskItem(item.RiskScore, item.Observation);
        }
    }

    /// <summary>Activity on one account over the window, from its transactions.</summary>
    public static AccountActivity Activity(IEnumerable<StatementTransaction> transactions, int windowMonths, DateTime? openedDate, DateTime now)
    {
        var tx = transactions.ToList();
        var first = tx.Count == 0 ? (DateTime?)null : tx.Min(t => t.Date);
        var last = tx.Count == 0 ? (DateTime?)null : tx.Max(t => t.Date);
        var credits = tx.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount);
        var debits = tx.Where(t => t.Type == TransactionType.Debit).Sum(t => t.Amount);
        return new AccountActivity(windowMonths, Date(first), Date(last), tx.Count, credits, debits, MonthsBetween(openedDate, now));
    }

    public static int? MonthsBetween(DateTime? from, DateTime to)
    {
        if (from is null || from.Value == DateTime.MinValue || from > to) return null;
        var months = (to.Year - from.Value.Year) * 12 + to.Month - from.Value.Month;
        if (to.Day < from.Value.Day) months--;
        return Math.Max(0, months);
    }

    /// <summary>Overdue is what is still owed on instalments already due; days past due run from the
    /// oldest of those. The next due instalment is the first unpaid one not yet due.</summary>
    public static (decimal Overdue, int DaysPastDue, DateTime? NextDue, decimal? NextDueAmount) ScheduleState(
        IEnumerable<(DateTime DueDate, decimal Outstanding, bool Complete)> periods, DateTime now)
    {
        var open = periods.Where(p => !p.Complete && p.Outstanding > 0).OrderBy(p => p.DueDate).ToList();
        var due = open.Where(p => p.DueDate.Date < now.Date).ToList();
        var overdue = due.Sum(p => p.Outstanding);
        var dpd = due.Count == 0 ? 0 : (int)(now.Date - due[0].DueDate.Date).TotalDays;
        var next = open.FirstOrDefault(p => p.DueDate.Date >= now.Date);
        return (overdue, dpd, next == default ? null : next.DueDate, next == default ? null : next.Outstanding);
    }

    // ── Advisory ─────────────────────────────────────────────────────────────────────────────────

    private static readonly Regex FailedTransaction = new(@"\b(BOUNCE|INSUFFICIENT|FAILED|REVERS)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Monthly inflow and outflow across every statement read, over the window ending now.
    /// Months with no transactions count as zero, so a quiet account reads as quiet rather than
    /// being averaged over only its busy months. Null when nothing was read.</summary>
    public static CustomerCashflowInput? CashflowInput(IReadOnlyList<IReadOnlyList<StatementTransaction>> statements, int windowMonths, decimal totalBalance, DateTime now)
    {
        if (statements.Count == 0) return null;
        var months = Math.Max(1, windowMonths);
        var start = new DateTime(now.Year, now.Month, 1).AddMonths(-(months - 1));
        var inflow = new decimal[months];
        var outflow = new decimal[months];
        var count = new int[months];
        var bounced = 0;
        var negativeDays = new HashSet<DateTime>();

        foreach (var t in statements.SelectMany(s => s))
        {
            var index = (t.Date.Year - start.Year) * 12 + t.Date.Month - start.Month;
            if (index < 0 || index >= months) continue;
            count[index]++;
            if (t.Type == TransactionType.Credit) inflow[index] += t.Amount; else outflow[index] += t.Amount;
            if (!string.IsNullOrEmpty(t.Description) && FailedTransaction.IsMatch(t.Description)) bounced++;
            if (t.RunningBalance < 0) negativeDays.Add(t.Date.Date);
        }

        var avgIn = Math.Round(inflow.Average(), 2);
        var avgOut = Math.Round(outflow.Average(), 2);
        var volatility = avgIn > 0 ? Math.Round((decimal)Math.Sqrt(inflow.Select(v => (double)((v - avgIn) * (v - avgIn))).Average()) / avgIn, 2) : 0m;
        return new CustomerCashflowInput(
            months, statements.Count, count.Sum(), count.Count(c => c > 0), avgIn, avgOut, Math.Round(avgIn - avgOut, 2),
            volatility, bounced, negativeDays.Count, Math.Round(totalBalance, 2));
    }

    /// <summary>What the advisory is scored from, drawn from the other sections. Figures and flags
    /// only; nothing identifying is carried.</summary>
    public static CustomerAdvisoryRequest AdvisoryInput(
        Guid reference, CustomerCheckQuery query, CustomerCheckSettings settings,
        IdentitySection identity, CreditBureauSection bureau, FraudCheckSection fraud,
        BankAccountsSection accounts, BankLoansSection loans, CrmsFacilitiesSection crms,
        CustomerCashflowInput? cashflow, DateTime now)
    {
        CustomerBureauInput? bureauInput = null;
        if (bureau.Status == SectionStatuses.Ok && bureau.Summary is { } s)
            bureauInput = new CustomerBureauInput(
                bureau.Bureau ?? "bureau", bureau.Reused, bureau.Score?.Value, s.TotalLoans, s.ActiveLoans, s.ClosedLoans,
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

        var identityInput = new CustomerIdentityInput(
            identity.Status == SectionStatuses.Ok ? true : null,
            identity.WatchListed,
            fraud.Status == SectionStatuses.Ok ? fraud.RiskScore : null,
            fraud.Status == SectionStatuses.Ok ? fraud.RiskBand : null,
            accounts.Status == SectionStatuses.Ok ? true : accounts.Status == SectionStatuses.NoRecord ? false : null,
            accounts.KycLevel,
            accounts.CustomerSince is { } since ? MonthsBetween(since.ToDateTime(TimeOnly.MinValue), now) : null);

        var supplied = query.LoanAmount is > 0 && query.TenorMonths is > 0;
        return new CustomerAdvisoryRequest(
            reference, supplied ? query.LoanAmount!.Value : settings.FraudDefaultLoanAmount,
            supplied ? query.TenorMonths!.Value : settings.FraudDefaultTenorMonths, supplied,
            bureauInput, cashflow, exposure, identityInput);
    }

    /// <summary>True when at least one section produced something to score.</summary>
    public static bool AdvisoryHasBasis(CustomerAdvisoryRequest r) =>
        r.Bureau is not null || r.Cashflow is { AccountsRead: > 0 } || r.Exposure.BankDataAvailable
        || r.Identity.BvnVerified is not null || r.Identity.FraudRiskScore is not null;

    public static AiAdvisorySection MapAdvisory(CustomerAdvisoryResponse a, CustomerAdvisoryRequest r, DateTime now)
    {
        var categories = a.Scores
            .Select(s => new AdvisoryCategory(s.Category, true, Math.Round(s.Score, 1), Math.Round(s.Weight, 2), s.Rating, s.Rationale, s.PositiveIndicators, s.RedFlags))
            .Concat(a.NotAssessed.Select(g => new AdvisoryCategory(g.Category, false, null, Math.Round(g.Weight, 2), null, g.Reason, [], [])))
            .ToList();
        var basis = new List<string>();
        if (r.Bureau is not null) basis.Add($"{r.Bureau.Bureau} report");
        if (r.Cashflow is { AccountsRead: > 0 } c) basis.Add($"statements on {c.AccountsRead} account{(c.AccountsRead == 1 ? "" : "s")} over {c.WindowMonths} months");
        if (r.Exposure.BankDataAvailable) basis.Add("loans with the bank");
        if (r.Identity.BvnVerified is not null) basis.Add("identity check");
        if (r.Identity.FraudRiskScore is not null) basis.Add("fraud check");
        return new AiAdvisorySection(
            SectionStatuses.Ok, null, Math.Round(a.OverallScore, 1), a.OverallRating, a.Recommendation,
            categories, a.RedFlags, a.Conditions,
            new AdvisoryNarrative(a.ExecutiveSummary, a.StrengthsAnalysis, a.WeaknessesAnalysis, a.MitigatingFactors, a.KeyRisks),
            new AdvisoryProposal(r.ProposedAmount, r.ProposedTenorMonths, r.ProposalSupplied, a.RecommendedAmount, a.RecommendedTenorMonths),
            $"Scored from {string.Join(", ", basis)}. The overall score is weighted over the assessed categories only.",
            a.NarrativeFromLlm ? "LLM" : "TEMPLATE", a.ModelVersion, now);
    }

    // ── Warnings and coverage ────────────────────────────────────────────────────────────────────

    public static List<string> Warnings(CustomerCheckResponse r)
    {
        var w = new List<string>();
        if (r.AiAdvisory.Status == SectionStatuses.Ok && r.AiAdvisory.Recommendation == "Decline")
            w.Add("The advisory recommends declining.");
        if (r.Identity.WatchListed == true)
            w.Add("The BVN is watch-listed.");
        if (r.CreditBureau.Status == SectionStatuses.Ok && r.CreditBureau.Summary is { } s)
        {
            if (s.DelinquentLoans > 0)
                w.Add($"{s.DelinquentLoans} bureau facilit{(s.DelinquentLoans == 1 ? "y is" : "ies are")} delinquent.");
            var worst = r.CreditBureau.Loans.Where(l => l.Status == CheckLoanStatuses.Overdue).OrderByDescending(l => l.Overdue).FirstOrDefault();
            if (worst is not null && s.MaxDaysOverdue > 0)
                w.Add($"A bureau loan is {s.MaxDaysOverdue} days overdue ({worst.Lender}).");
            if (r.CreditBureau.Loans.Any(l => l.Status == CheckLoanStatuses.WrittenOff))
                w.Add("A bureau loan has been written off.");
        }
        if (r.FraudCheck.RiskBand == "HIGH")
            w.Add("The fraud check rates this person as high risk.");
        foreach (var l in r.BankLoans.Loans.Where(l => l.Status == CheckLoanStatuses.Overdue))
            w.Add($"Bank loan ****{l.LoanAccountLast4} is {l.DaysPastDue} days past due.");
        if (r.BankAccounts.BvnMatch == BvnMatchResults.Mismatch)
            w.Add("An account number supplied belongs to a different BVN. Nothing from it was returned.");
        if (r.CrmsFacilities.MatchStatus == "IDENTITY_CONFLICT")
            w.Add("CRMS records found by the identifiers given do not agree with each other.");
        return w;
    }

    public static Dictionary<string, string> Coverage(CustomerCheckResponse r)
    {
        var c = new Dictionary<string, string>();
        c[CustomerCheckSections.Identity] = r.Identity.Status == SectionStatuses.Ok
            ? $"Identity fields from {r.Identity.Source}."
            : "No identity data was returned.";
        c[CustomerCheckSections.CreditBureau] = r.CreditBureau.Status switch
        {
            SectionStatuses.Ok when r.CreditBureau.Reused => $"{r.CreditBureau.Bureau} report reused from {r.CreditBureau.ReportDate:yyyy-MM-dd}; no bureau was called.",
            SectionStatuses.Ok when r.CreditBureau.BureausSkipped.Count > 0 => $"{r.CreditBureau.Bureau} answered after {string.Join(" and ", r.CreditBureau.BureausSkipped)} failed.",
            SectionStatuses.Ok => $"{r.CreditBureau.Bureau} answered; no other bureau was called.",
            SectionStatuses.NoRecord => $"{r.CreditBureau.Bureau} holds no record for this person; no other bureau was called.",
            SectionStatuses.Unavailable => "No bureau answered. Nothing is known from the bureaus.",
            _ => "Not run.",
        };
        c[CustomerCheckSections.FraudCheck] = r.FraudCheck.Status == SectionStatuses.Ok
            ? (r.FraudCheck.FinancialAnalysis?.Assessed == true
                ? "Overall score and financial analysis. Income is not known to the CRMS."
                : "Overall score only. The financial analysis needs a loan amount, tenor and income.")
            : "Not run.";
        c[CustomerCheckSections.BankAccounts] = r.BankAccounts.Status switch
        {
            SectionStatuses.Ok => "Every account under the customer records core banking holds for this BVN. Transaction activity is read for a bounded number of them; reason says how many.",
            SectionStatuses.NoRecord => "Core banking holds no customer with this BVN.",
            SectionStatuses.Unavailable => "Core banking did not answer. Nothing is known about accounts.",
            _ => "Not run.",
        };
        c[CustomerCheckSections.BankLoans] = r.BankLoans.Status switch
        {
            SectionStatuses.Ok => "Loans booked in core banking for this customer. Days past due is computed from the repayment schedule.",
            SectionStatuses.NoRecord => "Core banking holds no customer with this BVN.",
            SectionStatuses.Unavailable => "Core banking did not answer. Nothing is known about loans.",
            _ => "Not run.",
        };
        c[CustomerCheckSections.CrmsFacilities] = "Every CRMS case the BVN appears on, in any role. No prudential classification is held.";
        c[CustomerCheckSections.AiAdvisory] = r.AiAdvisory.Status switch
        {
            SectionStatuses.Ok => r.AiAdvisory.Basis ?? "Scored from the other sections.",
            SectionStatuses.Unavailable => "No advisory. " + (r.AiAdvisory.Reason ?? ""),
            _ => "Not run.",
        };
        return c;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    public static DateOnly? Date(DateTime? d) => d is null || d.Value == DateTime.MinValue ? null : DateOnly.FromDateTime(d.Value);

    private static readonly string[] DateFormats =
        ["yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "yyyy-MM-ddTHH:mm:ss", "dd-MMM-yyyy", "yyyy/MM/dd", "MM/dd/yyyy"];

    public static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        if (DateTime.TryParseExact(v, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return DateOnly.FromDateTime(dt);
        return DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt) ? DateOnly.FromDateTime(dt) : null;
    }

    private static bool? ParseBool(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        "true" or "yes" or "1" => true,
        "false" or "no" or "0" => false,
        _ => null,
    };

    private static string? JoinName(params string?[] parts)
    {
        var joined = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
        return joined.Length == 0 ? null : joined;
    }

    private static string? MaskLast4Or(string? account) => string.IsNullOrWhiteSpace(account) ? null : Last4(account.Trim());

    private static bool ContainsAny(string haystack, params string[] needles) => needles.Any(haystack.Contains);
}
