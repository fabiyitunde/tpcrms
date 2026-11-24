using System.Text.RegularExpressions;
using CRMS.Application.Advisory.Interfaces;
using CRMS.Application.Billing;
using CRMS.Application.CreditBureau;
using CRMS.Application.CustomerCheck;
using CRMS.Application.PartyExposure;
using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.Services;

/// <summary>
/// The company credit check: one registration number in, seven sections out. Built like
/// <see cref="CustomerCheckService"/>: each section runs in turn with its own time budget and its own
/// try/catch, sequentially because the CRMS lookup shares this scope's DbContext.
/// <para>
/// Core banking cannot search by registration number, so the bank sections start from the account
/// numbers given and keep only a customer whose incorporation number matches the one checked.
/// </para>
/// </summary>
public class CompanyCheckService : ICompanyCheckService
{
    private static readonly Regex LongDigitRuns = new(@"\d{10,}", RegexOptions.Compiled);

    private readonly ISmartComplyProvider _smartComply;
    private readonly ICoreBankingService _coreBanking;
    private readonly IFineractDirectService _fineract;
    private readonly IAIAdvisoryService _advisory;
    private readonly CRMSDbContext _db;
    private readonly CustomerCheckSettings _settings;
    private readonly ILogger<CompanyCheckService> _logger;

    public CompanyCheckService(
        ISmartComplyProvider smartComply,
        ICoreBankingService coreBanking,
        IFineractDirectService fineract,
        IAIAdvisoryService advisory,
        CRMSDbContext db,
        IOptions<CustomerCheckSettings> settings,
        ILogger<CompanyCheckService> logger)
    {
        _smartComply = smartComply;
        _coreBanking = coreBanking;
        _fineract = fineract;
        _advisory = advisory;
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<CompanyCheckOutcome> CheckAsync(CustomerCheckRequester requester, CompanyCheckQuery query, CancellationToken ct = default)
    {
        using var _ = BillingContext.Begin(requester.BillingFlow, null, requester.BillingReference, "Company", requester.TriggeredBy);
        var now = DateTime.UtcNow;
        var state = new RunState();

        var registration = Gate(requester, query, CompanyCheckSections.CompanyRegistration) is { } gr
            ? CompanyRegistrationSection.Empty(gr)
            : await RegistrationAsync(query, state, ct);

        var bureau = Gate(requester, query, CompanyCheckSections.CreditBureau) is { } gb
            ? CreditBureauSection.Empty(gb)
            : await BureauAsync(query, state, now, ct);

        var fraud = Gate(requester, query, CompanyCheckSections.FraudCheck) is { } gf
            ? FraudCheckSection.Empty(gf)
            : await FraudAsync(query, state, ct);

        BankAccountsSection accounts;
        BankLoansSection loans;
        var ga = Gate(requester, query, CompanyCheckSections.BankAccounts);
        var gl = Gate(requester, query, CompanyCheckSections.BankLoans);
        if (ga is not null && gl is not null)
        {
            accounts = BankAccountsSection.Empty(ga);
            loans = BankLoansSection.Empty(gl);
        }
        else
        {
            (accounts, loans) = await BankAsync(query, state, now, ct);
            if (ga is not null) accounts = BankAccountsSection.Empty(ga);
            if (gl is not null) loans = BankLoansSection.Empty(gl);
        }

        var crms = Gate(requester, query, CompanyCheckSections.CrmsFacilities) is { } gc
            ? CrmsFacilitiesSection.Empty(gc)
            : await CrmsAsync(query, ct);

        var advisory = Gate(requester, query, CompanyCheckSections.AiAdvisory) is { } gv
            ? AiAdvisorySection.Empty(gv)
            : await AdvisoryAsync(query, registration, bureau, fraud, accounts, loans, crms, state, now, ct);

        var sectionsRun = new List<string>();
        if (Ran(registration.Status)) sectionsRun.Add(CompanyCheckSections.CompanyRegistration);
        if (Ran(bureau.Status)) sectionsRun.Add(CompanyCheckSections.CreditBureau);
        if (Ran(fraud.Status)) sectionsRun.Add(CompanyCheckSections.FraudCheck);
        if (Ran(accounts.Status)) sectionsRun.Add(CompanyCheckSections.BankAccounts);
        if (Ran(loans.Status)) sectionsRun.Add(CompanyCheckSections.BankLoans);
        if (Ran(crms.Status)) sectionsRun.Add(CompanyCheckSections.CrmsFacilities);
        if (Ran(advisory.Status)) sectionsRun.Add(CompanyCheckSections.AiAdvisory);

        var response = new CompanyCheckResponse(
            query.RequestId, now, requester.Name,
            new CompanySubject(query.RcNumber, query.CompanyName, query.Nubans.Select(CustomerCheckRules.MaskLast4).ToList()),
            sectionsRun, registration, bureau, fraud, accounts, loans, crms, advisory,
            new Dictionary<string, string>(), []);
        response = response with
        {
            Coverage = CompanyCheckRules.Coverage(response),
            Warnings = CompanyCheckRules.Warnings(response),
        };

        return new CompanyCheckOutcome(response, state.BureauUsed, state.Billed);
    }

    private static string? Gate(CustomerCheckRequester requester, CompanyCheckQuery query, string section)
        => !query.Wants(section) ? SectionStatuses.NotRequested
            : !requester.Allows(section) ? SectionStatuses.NotAllowed
            : null;

    private static bool Ran(string status) => status is not (SectionStatuses.NotRequested or SectionStatuses.NotAllowed);

    // ── Registration (CAC) ───────────────────────────────────────────────────────────────────────

    private async Task<CompanyRegistrationSection> RegistrationAsync(CompanyCheckQuery query, RunState state, CancellationToken ct)
    {
        using var budget = Budget(_settings.BureauTimeoutMs, ct);
        try
        {
            var result = await _smartComply.VerifyCacAdvancedAsync(query.RcNumber, query.CompanyName, query.CompanyType, budget.Token);
            state.IdentityCalls++;
            if (result.IsSuccess)
            {
                state.Cac = result.Value;
                return CompanyCheckRules.MapRegistration(result.Value, query.CompanyName);
            }
            if (CompanyCheckRules.IsRegistryNoRecord(result.Error))
                return CompanyRegistrationSection.Empty(SectionStatuses.NoRecord, "The Corporate Affairs Commission holds no company with this registration number and name.");
            _logger.LogWarning("Company credit check: CAC lookup failed: {Error}", Safe(result.Error));
            return CompanyRegistrationSection.Empty(SectionStatuses.Unavailable, Safe(result.Error));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return CompanyRegistrationSection.Empty(SectionStatuses.Unavailable, "The registry did not answer in time.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Company credit check: registration section failed.");
            return CompanyRegistrationSection.Empty(SectionStatuses.Unavailable, "The registry could not be reached.");
        }
    }

    // ── Business credit bureau ───────────────────────────────────────────────────────────────────

    private async Task<CreditBureauSection> BureauAsync(CompanyCheckQuery query, RunState state, DateTime now, CancellationToken ct)
    {
        // CRC, then First Central: the same chain every loan track uses for a company.
        using var budget = Budget(_settings.BureauTimeoutMs * 2, ct);
        try
        {
            var pull = await BureauFallback.GetBusinessReportAsync(_smartComply, query.RcNumber, budget.Token);
            var bureau = pull.Bureau == CreditBureauProvider.FirstCentral ? Bureaus.FirstCentral : Bureaus.Crc;
            var skipped = pull.Bureau == CreditBureauProvider.FirstCentral ? new List<string> { Bureaus.Crc } : [];
            if (pull.Result.IsSuccess)
            {
                state.BureauCalls += 1 + skipped.Count;
                state.BureauUsed = bureau;
                state.Report = pull.Result.Value;
                return CompanyCheckRules.MapBusinessBureau(pull.Result.Value, bureau, now, skipped);
            }
            if (CustomerCheckRules.IsNoRecord(pull.Result.Error))
            {
                state.BureauCalls += 2;
                state.BureauUsed = bureau;
                return new CreditBureauSection(
                    SectionStatuses.NoRecord, "Neither CRC nor First Central holds a business record for this registration number.",
                    Bureaus.Label(bureau), [Bureaus.Label(Bureaus.Crc)], false, null, null, null, [], []);
            }
            _logger.LogWarning("Company credit check: business bureau failed: {Error}", Safe(pull.Result.Error));
            return CreditBureauSection.Empty(SectionStatuses.Unavailable, Safe(pull.Result.Error), [Bureaus.Label(Bureaus.Crc), Bureaus.Label(Bureaus.FirstCentral)]);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return CreditBureauSection.Empty(SectionStatuses.Unavailable, "The bureaus did not answer in time.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Company credit check: bureau section failed.");
            return CreditBureauSection.Empty(SectionStatuses.Unavailable, "The bureaus could not be reached.");
        }
    }

    // ── Fraud ────────────────────────────────────────────────────────────────────────────────────

    private async Task<FraudCheckSection> FraudAsync(CompanyCheckQuery query, RunState state, CancellationToken ct)
    {
        using var budget = Budget(_settings.BureauTimeoutMs, ct);
        try
        {
            var supplied = query.LoanAmount is not null && query.TenorMonths is not null && query.AnnualRevenue is > 0;
            var request = new SmartComplyBusinessLoanRequest(
                BusinessName: state.Cac?.CompanyName ?? state.Report?.Name ?? query.CompanyName,
                BusinessAddress: state.Cac?.Address ?? state.Report?.Address,
                RcNumber: query.RcNumber,
                City: state.Cac?.City,
                Country: "Nigeria",
                PhoneNumber: state.Report?.Phone,
                EmailAddress: state.Cac?.Email ?? state.Report?.Email,
                AnnualRevenue: query.AnnualRevenue ?? 0,
                BankName: null,
                AccountNumber: query.Nubans.FirstOrDefault(),
                LoanAmountRequested: query.LoanAmount ?? _settings.FraudDefaultLoanAmount,
                PurposeOfLoan: query.LoanPurpose ?? "Credit assessment",
                LoanRepaymentDurationType: "months",
                LoanRepaymentDurationValue: query.TenorMonths ?? _settings.FraudDefaultTenorMonths,
                CollateralRequired: false,
                CollateralValue: null,
                RunAmlCheck: query.RunAmlCheck);

            var result = await _smartComply.CheckBusinessLoanFraudAsync(request, budget.Token);
            if (result.IsFailure)
            {
                _logger.LogWarning("Company credit check: fraud check failed: {Error}", Safe(result.Error));
                return FraudCheckSection.Empty(SectionStatuses.Unavailable, Safe(result.Error));
            }
            state.FraudCalls++;
            var mapped = CustomerCheckRules.MapFraud(result.Value, supplied, query.RunAmlCheck);
            return supplied || mapped.FinancialAnalysis is null ? mapped : mapped with
            {
                FinancialAnalysis = mapped.FinancialAnalysis with
                {
                    Reason = "No loan amount, tenor or annual revenue given; defaults were used, so these sub-scores are not meaningful.",
                },
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return FraudCheckSection.Empty(SectionStatuses.Unavailable, "The fraud-check provider did not answer in time.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Company credit check: fraud section failed.");
            return FraudCheckSection.Empty(SectionStatuses.Unavailable, "The fraud-check provider could not be reached.");
        }
    }

    // ── Bank accounts and loans ──────────────────────────────────────────────────────────────────

    private async Task<(BankAccountsSection, BankLoansSection)> BankAsync(CompanyCheckQuery query, RunState state, DateTime now, CancellationToken ct)
    {
        if (query.Nubans.Count == 0)
            return Both(SectionStatuses.NotPossible,
                "No account number was given. Core banking finds a company only by one of its account numbers.");

        using var section = Budget(_settings.BankSectionTimeoutMs, ct);
        var notes = new List<string>();
        var clients = new List<FineractClientMatch>();
        var unreachable = 0;

        foreach (var nuban in query.Nubans)
        {
            var masked = CustomerCheckRules.MaskLast4(nuban);
            try
            {
                Result<IReadOnlyList<FineractClientMatch>> found;
                using (var call = Budget(_settings.CoreBankingTimeoutMs, section.Token))
                    found = await _fineract.FindClientsAsync(new FineractClientSearch(Nuban: nuban), true, call.Token);
                if (found.IsFailure) { unreachable++; notes.Add($"Account {masked} could not be looked up: {Safe(found.Error)}"); continue; }
                if (found.Value.Count == 0) { notes.Add($"Account {masked} is not held by any customer in core banking."); continue; }

                // Only a customer registered under the number being checked counts. Core banking keeps
                // the incorporation number on the account's customer record.
                var registered = await RegistrationOfAsync(nuban, section.Token, ct);
                if (registered is null)
                    notes.Add($"The registration number on account {masked} could not be read, so it is listed unconfirmed.");
                else if (!SameRegistration(registered, query))
                {
                    notes.Add($"Account {masked} is registered to a different company ({registered}) and is not reported.");
                    continue;
                }

                foreach (var client in found.Value.Where(c => clients.All(k => k.Id != c.Id)))
                    clients.Add(client);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                unreachable++;
                notes.Add($"Core banking did not answer in time for account {masked}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Company credit check: core banking search failed.");
                unreachable++;
                notes.Add($"Core banking could not be reached for account {masked}.");
            }
        }

        if (clients.Count == 0)
        {
            var status = unreachable == query.Nubans.Count ? SectionStatuses.Unavailable : SectionStatuses.NoRecord;
            return Both(status, string.Join(" ", notes));
        }

        var savings = new List<(ClientSavingsAccountSummary Account, string? Nuban)>();
        var bankLoans = new List<BankLoan>();
        var errors = new List<string>();
        foreach (var client in clients)
        {
            Result<ClientAccountSummary> summary;
            try
            {
                using var call = Budget(_settings.CoreBankingTimeoutMs, section.Token);
                summary = await _fineract.GetClientAccountsAsync(client.Id, call.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { summary = Result.Failure<ClientAccountSummary>("Core banking did not answer in time."); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Company credit check: client accounts read failed.");
                summary = Result.Failure<ClientAccountSummary>("Core banking could not be reached.");
            }

            if (summary.IsFailure)
            {
                errors.Add(Safe(summary.Error));
                savings.AddRange(client.SavingsAccounts.Select(s => (s, NubanOf(s))));
                continue;
            }
            savings.AddRange(summary.Value.SavingsAccounts.Select(s => (s, NubanOf(s))));
            foreach (var l in summary.Value.LoanAccounts.Where(l => CustomerCheckRules.IsBookedLoan(l.StatusCode)))
                bankLoans.Add(await LoanAsync(l, now, section.Token, ct));
        }

        var activityFor = savings.Where(s => s.Nuban is not null)
            .OrderByDescending(s => query.Nubans.Contains(s.Nuban!))
            .ThenByDescending(s => CustomerCheckRules.SavingsStatus(s.Account.Status) == CheckAccountStatuses.Active)
            .ThenByDescending(s => s.Account.AccountBalance)
            .Take(Math.Max(0, _settings.MaxActivityAccounts))
            .Select(s => s.Nuban!)
            .ToHashSet();
        if (savings.Count(s => s.Nuban is not null) > activityFor.Count)
            notes.Add($"Transaction activity was read for {activityFor.Count} of {savings.Count} accounts.");

        var accounts = new List<BankAccount>();
        foreach (var (s, nuban) in savings)
        {
            var activity = nuban is not null && activityFor.Contains(nuban)
                ? await ActivityAsync(nuban, query.ActivityWindowMonths, s.ActivatedOn, now, notes, state, section.Token, ct)
                : null;
            accounts.Add(new BankAccount(
                CustomerCheckRules.Last4(nuban ?? s.AccountNo), s.ProductName, (s.AccountType ?? "Savings").ToUpperInvariant(),
                CustomerCheckRules.SavingsStatus(s.Status), string.IsNullOrWhiteSpace(s.Status) ? null : s.Status,
                CustomerCheckRules.Date(s.ActivatedOn), Math.Round(s.AccountBalance, 2), "NGN",
                CustomerCheckRules.Date(s.LastActiveTransactionDate) ?? activity?.LastTransaction, activity));
        }

        var primary = clients[0];
        if (clients.Count > 1)
            notes.Add($"The account numbers given belong to {clients.Count} customer records; accounts and loans from all of them are listed.");
        var remarks = notes.Concat(errors).ToList();
        var accountsSection = new BankAccountsSection(
            SectionStatuses.Ok, remarks.Count == 0 ? null : string.Join(" ", remarks),
            null, primary.Id.ToString(), primary.Status, primary.KycLevel?.ToString(),
            CustomerCheckRules.Date(primary.ActivatedOn), accounts);
        var loansSection = errors.Count > 0 && bankLoans.Count == 0
            ? BankLoansSection.Empty(SectionStatuses.Unavailable, string.Join("; ", errors))
            : new BankLoansSection(SectionStatuses.Ok, errors.Count == 0 ? null : string.Join("; ", errors), bankLoans);
        return (accountsSection, loansSection);

        static (BankAccountsSection, BankLoansSection) Both(string status, string why)
            => (BankAccountsSection.Empty(status, why), BankLoansSection.Empty(status, why));
    }

    /// <summary>The incorporation number on the account's customer record, or null when it could
    /// not be read.</summary>
    private async Task<string?> RegistrationOfAsync(string nuban, CancellationToken section, CancellationToken ct)
    {
        try
        {
            using var call = Budget(_settings.CoreBankingTimeoutMs, section);
            var info = await _coreBanking.GetCorporateInfoAsync(nuban, call.Token);
            return info.IsSuccess && !string.IsNullOrWhiteSpace(info.Value.RegistrationNumber) ? info.Value.RegistrationNumber.Trim() : null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return null; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Company credit check: corporate details read failed.");
            return null;
        }
    }

    private static bool SameRegistration(string recorded, CompanyCheckQuery query)
    {
        var normalised = CompanyCheckRules.NormaliseRegistrationNumber(recorded);
        if (normalised is null)
            return Identifiers.Digits(recorded) == query.RcDigits;
        // A record without a prefix was read as RC; compare digits when either side had none.
        return normalised.Value.Number == query.RcNumber || normalised.Value.Number[normalised.Value.Type.Length..] == query.RcDigits;
    }

    private static string? NubanOf(ClientSavingsAccountSummary s)
    {
        var ext = Identifiers.Digits(s.ExternalId);
        if (ext is { Length: 10 } && ext.All(char.IsAsciiDigit)) return ext;
        var no = Identifiers.Digits(s.AccountNo);
        return no is { Length: 10 } && no.All(char.IsAsciiDigit) ? no : null;
    }

    private async Task<AccountActivity?> ActivityAsync(string nuban, int windowMonths, DateTime? openedDate, DateTime now, List<string> notes, RunState state, CancellationToken section, CancellationToken ct)
    {
        var masked = CustomerCheckRules.MaskLast4(nuban);
        try
        {
            using var call = Budget(_settings.CoreBankingTimeoutMs, section);
            var statement = await _coreBanking.GetStatementAsync(nuban, now.AddMonths(-windowMonths), now, call.Token);
            if (statement.IsFailure)
            {
                notes.Add($"The statement for account {masked} could not be read, so its activity is not reported.");
                return null;
            }
            state.Statements.Add(statement.Value.Transactions);
            return CustomerCheckRules.Activity(statement.Value.Transactions, windowMonths, openedDate, now);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            notes.Add($"The statement for account {masked} did not arrive in time, so its activity is not reported.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Company credit check: statement read failed.");
            notes.Add($"The statement for account {masked} could not be read, so its activity is not reported.");
            return null;
        }
    }

    private async Task<BankLoan> LoanAsync(ClientLoanAccountSummary l, DateTime now, CancellationToken section, CancellationToken ct)
    {
        Result<FineractLoanDetail>? detail = null;
        try
        {
            using var call = Budget(_settings.CoreBankingTimeoutMs, section);
            detail = await _fineract.GetLoanDetailAsync(l.Id, call.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
        catch (Exception ex) { _logger.LogError(ex, "Company credit check: loan detail read failed."); }

        if (detail is null || detail.IsFailure)
            return new BankLoan(
                CustomerCheckRules.Last4(l.AccountNo), l.ProductName, 0, null, null, null,
                CustomerCheckRules.FineractLoanStatus(l.StatusCode, l.InArrears ? 1 : null), l.Status,
                CustomerCheckRules.Date(l.DisbursedOn), CustomerCheckRules.Date(l.MaturityOn), null, null);

        var d = detail.Value;
        var (overdue, dpd, nextDue, nextAmount) = CustomerCheckRules.ScheduleState(
            d.RepaymentSchedule.Select(p => (p.DueDate, p.TotalOutstanding, p.Complete)), now);
        var principal = d.Summary.PrincipalDisbursed > 0 ? d.Summary.PrincipalDisbursed : d.ApprovedPrincipal > 0 ? d.ApprovedPrincipal : d.Principal;
        return new BankLoan(
            CustomerCheckRules.Last4(d.AccountNo), string.IsNullOrWhiteSpace(d.ProductName) ? l.ProductName : d.ProductName,
            Math.Round(principal, 2), Math.Round(d.Summary.TotalOutstanding, 2), overdue, dpd,
            CustomerCheckRules.FineractLoanStatus(d.StatusCode, overdue), d.Status,
            CustomerCheckRules.Date(d.DisbursementDate ?? l.DisbursedOn), CustomerCheckRules.Date(d.MaturityDate ?? l.MaturityOn),
            CustomerCheckRules.Date(nextDue), nextAmount);
    }

    // ── CRMS cases on this registration number ───────────────────────────────────────────────────

    private async Task<CrmsFacilitiesSection> CrmsAsync(CompanyCheckQuery query, CancellationToken ct)
    {
        using var budget = Budget(_settings.CrmsLookupTimeoutMs, ct);
        try
        {
            var spellings = CompanyCheckRules.StoredSpellings(query.RcNumber, query.CompanyType);
            var found = new List<CrmsFacility>();
            var token = budget.Token;

            var corporate = await _db.LoanApplications.AsNoTracking()
                .Where(a => a.RegistrationNumber != null && spellings.Contains(a.RegistrationNumber))
                .Select(a => new { a.ApplicationNumber, a.Status, a.CustomerName, Approved = a.ApprovedAmount == null ? (decimal?)null : a.ApprovedAmount.Amount, Requested = a.RequestedAmount.Amount })
                .ToListAsync(token);
            found.AddRange(corporate.Select(a => Facility(RelationshipTypes.Borrower, PartyExposureService.TrackCorporate, a.ApplicationNumber,
                "CORPORATE_LOAN", a.CustomerName, PartyExposureRules.CorporateStatus(a.Status), a.Approved ?? a.Requested, null)));

            var namp = await _db.NampApplications.AsNoTracking()
                .Where(a => a.RcNumber != null && spellings.Contains(a.RcNumber))
                .Select(a => new { a.ApplicationNumber, a.Status, a.CompanyName, a.ApplicantName, a.LoanAmount, a.FineractDisbursedOn })
                .ToListAsync(token);
            found.AddRange(namp.Select(a => Facility(RelationshipTypes.Borrower, PartyExposureService.TrackNamp, a.ApplicationNumber,
                "EQUIPMENT_FINANCE", a.CompanyName ?? a.ApplicantName, PartyExposureRules.NampStatus(a.Status, a.FineractDisbursedOn), a.LoanAmount, null)));

            var rhshf = await _db.RhshfCreditProfiles.AsNoTracking()
                .Where(p => spellings.Contains(p.RcNumber))
                .Select(p => new { p.Reference, p.Status, p.InternalStage, p.CompanyName, p.ApprovedAmount, p.TotalEopValue })
                .ToListAsync(token);
            found.AddRange(rhshf.Select(p => Facility(RelationshipTypes.Borrower, PartyExposureService.TrackRhshf, p.Reference,
                "DRY_SEASON_INPUTS", p.CompanyName, PartyExposureRules.RhshfStatus(p.Status, p.InternalStage), p.ApprovedAmount ?? p.TotalEopValue, null)));

            // The company as a corporate guarantor on someone else's facility.
            var corporateGuarantees = await _db.Guarantors.AsNoTracking()
                .Where(g => g.RegistrationNumber != null && spellings.Contains(g.RegistrationNumber))
                .Join(_db.LoanApplications.AsNoTracking(), g => g.LoanApplicationId, a => a.Id,
                    (g, a) => new { a.ApplicationNumber, a.Status, a.CustomerName, Limit = g.GuaranteeLimit == null ? (decimal?)null : g.GuaranteeLimit.Amount, Approved = a.ApprovedAmount == null ? (decimal?)null : a.ApprovedAmount.Amount })
                .ToListAsync(token);
            found.AddRange(corporateGuarantees.Select(g => Facility(RelationshipTypes.Guarantor, PartyExposureService.TrackCorporate, g.ApplicationNumber,
                "CORPORATE_LOAN", g.CustomerName, PartyExposureRules.CorporateStatus(g.Status), g.Approved, g.Limit)));

            var nampGuarantees = await _db.NampGuarantors.AsNoTracking()
                .Where(g => g.CompanyRegistrationNumber != null && spellings.Contains(g.CompanyRegistrationNumber))
                .Join(_db.NampApplications.AsNoTracking(), g => g.NampApplicationId, a => a.Id,
                    (g, a) => new { a.ApplicationNumber, a.Status, a.CompanyName, a.ApplicantName, a.LoanAmount, a.FineractDisbursedOn, g.GuaranteeLimit })
                .ToListAsync(token);
            found.AddRange(nampGuarantees.Select(g => Facility(RelationshipTypes.Guarantor, PartyExposureService.TrackNamp, g.ApplicationNumber,
                "EQUIPMENT_FINANCE", g.CompanyName ?? g.ApplicantName, PartyExposureRules.NampStatus(g.Status, g.FineractDisbursedOn), g.LoanAmount, g.GuaranteeLimit)));

            var rhshfGuarantees = await _db.RhshfGuarantors.AsNoTracking()
                .Where(g => g.RcNumber != null && spellings.Contains(g.RcNumber))
                .Join(_db.RhshfCreditProfiles.AsNoTracking(), g => g.RhshfCreditProfileId, p => p.Id,
                    (g, p) => new { p.Reference, p.Status, p.InternalStage, p.CompanyName, p.ApprovedAmount, p.TotalEopValue, g.GuaranteeAmount })
                .ToListAsync(token);
            found.AddRange(rhshfGuarantees.Select(g => Facility(RelationshipTypes.Guarantor, PartyExposureService.TrackRhshf, g.Reference,
                "DRY_SEASON_INPUTS", g.CompanyName, PartyExposureRules.RhshfStatus(g.Status, g.InternalStage), g.ApprovedAmount ?? g.TotalEopValue, g.GuaranteeAmount)));

            var direct = found.Count(f => f.Role == RelationshipTypes.Borrower);
            var summary = new CrmsFacilitySummary(direct, found.Count - direct, null, false);
            return new CrmsFacilitiesSection(
                found.Count == 0 ? SectionStatuses.NoRecord : SectionStatuses.Ok,
                found.Count == 0 ? "No CRMS case carries this registration number." : null,
                found.Count == 0 ? MatchStatuses.NoMatch : MatchStatuses.Match, summary,
                found.OrderBy(f => f.Role != RelationshipTypes.Borrower).ThenBy(f => f.Track).ThenBy(f => f.CaseReference).ToList());
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return CrmsFacilitiesSection.Empty(SectionStatuses.Unavailable, "The CRMS lookup did not finish in time.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Company credit check: CRMS facilities section failed.");
            return CrmsFacilitiesSection.Empty(SectionStatuses.Unavailable, "The CRMS lookup failed.");
        }

        static CrmsFacility Facility(string role, string track, string reference, string type, string obligor, string status, decimal? approved, decimal? guarantee)
            => new(role, track, reference, type, obligor, status, approved, guarantee, null, null, BalanceSources.NotRequested);
    }

    // ── Advisory ─────────────────────────────────────────────────────────────────────────────────

    private async Task<AiAdvisorySection> AdvisoryAsync(
        CompanyCheckQuery query, CompanyRegistrationSection registration, CreditBureauSection bureau, FraudCheckSection fraud,
        BankAccountsSection accounts, BankLoansSection loans, CrmsFacilitiesSection crms, RunState state, DateTime now, CancellationToken ct)
    {
        using var budget = Budget(_settings.AdvisoryTimeoutMs, ct);
        try
        {
            var cashflow = CustomerCheckRules.CashflowInput(state.Statements, query.ActivityWindowMonths, accounts.Accounts.Sum(a => a.Balance ?? 0), now);
            var input = CompanyCheckRules.AdvisoryInput(Guid.NewGuid(), query, _settings, registration, bureau, fraud, accounts, loans, crms, cashflow, now);
            if (!CustomerCheckRules.AdvisoryHasBasis(input))
                return AiAdvisorySection.Empty(SectionStatuses.Unavailable, "Nothing to assess: no registry, bureau or bank data was returned.");

            var result = await _advisory.GenerateCustomerAdvisoryAsync(input, budget.Token);
            if (!result.Success)
                return AiAdvisorySection.Empty(SectionStatuses.Unavailable, Safe(result.ErrorMessage));
            state.AdvisoryCalls++;
            var mapped = CustomerCheckRules.MapAdvisory(result, input, now);
            return mapped with { Basis = mapped.Basis?.Replace("identity check", "CAC registration check") };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return AiAdvisorySection.Empty(SectionStatuses.Unavailable, "The advisory did not finish in time.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Company credit check: advisory section failed.");
            return AiAdvisorySection.Empty(SectionStatuses.Unavailable, "The advisory could not be produced.");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    private static CancellationTokenSource Budget(int ms, CancellationToken ct)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(ms);
        return cts;
    }

    private static string Safe(string? error) =>
        string.IsNullOrWhiteSpace(error) ? "The provider returned no detail." : LongDigitRuns.Replace(error, "****");

    private sealed class RunState
    {
        public SmartComplyCacResult? Cac;
        public SmartComplyBusinessCreditReport? Report;
        public string? BureauUsed;
        public int IdentityCalls, BureauCalls, FraudCalls, AdvisoryCalls;
        public readonly List<IReadOnlyList<StatementTransaction>> Statements = [];
        public BilledCallCounts Billed => new(IdentityCalls, BureauCalls, 0, FraudCalls, AdvisoryCalls);
    }
}
