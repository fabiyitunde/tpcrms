using System.Text.Json;
using System.Text.RegularExpressions;
using CRMS.Application.Advisory.Interfaces;
using CRMS.Application.Billing;
using CRMS.Application.CustomerCheck;
using CRMS.Domain.Aggregates.Billing;
using CRMS.Application.PartyExposure;
using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Common;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.ExternalServices.Rhshf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.Services;

/// <summary>
/// The customer credit check (docs/Customer_Credit_Check.md): one BVN in, six sections out. Each section runs in
/// turn with its own time budget and its own try/catch, so a provider that fails or stalls costs
/// its own section and nothing else. Sequential on purpose: the party-exposure lookup and the
/// bureau store share this scope's DbContext.
/// </summary>
public class CustomerCheckService : ICustomerCheckService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Regex LongDigitRuns = new(@"\d{10,}", RegexOptions.Compiled);

    private readonly ISmartComplyProvider _smartComply;
    private readonly ICoreBankingService _coreBanking;
    private readonly IFineractDirectService _fineract;
    private readonly IPartyExposureService _partyExposure;
    private readonly IAIAdvisoryService _advisory;
    private readonly ICustomerCheckRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CustomerCheckSettings _settings;
    private readonly string _hashKey;
    private readonly ILogger<CustomerCheckService> _logger;
    private readonly IBillingLedger? _billing;

    public CustomerCheckService(
        ISmartComplyProvider smartComply,
        ICoreBankingService coreBanking,
        IFineractDirectService fineract,
        IPartyExposureService partyExposure,
        IAIAdvisoryService advisory,
        ICustomerCheckRepository repository,
        IUnitOfWork unitOfWork,
        IOptions<CustomerCheckSettings> settings,
        IOptions<RhshfSettings> rhshf,
        ILogger<CustomerCheckService> logger,
        IBillingLedger? billing = null)
    {
        _billing = billing;
        _smartComply = smartComply;
        _coreBanking = coreBanking;
        _fineract = fineract;
        _partyExposure = partyExposure;
        _advisory = advisory;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _settings = settings.Value;
        _hashKey = ResolveHashKey(_settings, rhshf.Value);
        _logger = logger;
    }

    public static string ResolveHashKey(CustomerCheckSettings settings, RhshfSettings rhshf)
        => !string.IsNullOrWhiteSpace(settings.HashKey) ? settings.HashKey
            : !string.IsNullOrWhiteSpace(rhshf.CallbackSigningSecret) ? rhshf.CallbackSigningSecret
            : "crms-customer-check-local";

    public string HashIdentifier(string value) => PartyExposureRules.KeyedHash(value, _hashKey);

    public async Task RecordAsync(CustomerCheckLog log, CancellationToken ct = default)
    {
        await _repository.AddLogAsync(log, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // The check itself is a billable item to the bank, on top of the third-party calls it made,
        // which the SmartComply wrapper recorded one by one under the same reference.
        if (_billing is not null)
            await _billing.RecordAsync(new BillingUsageDraft(BillingItems.PartnerCheck, log.Error is null, log.Error, null, log.DurationMs,
                new BillingScope(BillingFlow.PartnerApi, log.Id, PartnerReference(log.ConsumerName, log.RequestId), "Customer")), ct);
    }

    public static string PartnerReference(string consumerName, string requestId) => $"{consumerName} · {requestId}";

    public async Task<CustomerCheckOutcome> CheckAsync(ApiConsumer consumer, CustomerCheckQuery query, CancellationToken ct = default)
    {
        using var _ = BillingContext.Begin(BillingFlow.PartnerApi, null, PartnerReference(consumer.Name, query.RequestId), "Customer");
        var now = DateTime.UtcNow;
        var state = new RunState();

        var identity = Gate(consumer, query, CustomerCheckSections.Identity) is { } gi
            ? IdentitySection.Empty(gi)
            : await IdentityAsync(query, state, ct);

        var bureau = Gate(consumer, query, CustomerCheckSections.CreditBureau) is { } gb
            ? CreditBureauSection.Empty(gb)
            : await BureauAsync(consumer, query, state, now, ct);

        var fraud = Gate(consumer, query, CustomerCheckSections.FraudCheck) is { } gf
            ? FraudCheckSection.Empty(gf)
            : await FraudAsync(query, state, ct);

        BankAccountsSection accounts;
        BankLoansSection loans;
        var ga = Gate(consumer, query, CustomerCheckSections.BankAccounts);
        var gl = Gate(consumer, query, CustomerCheckSections.BankLoans);
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

        var crms = Gate(consumer, query, CustomerCheckSections.CrmsFacilities) is { } gc
            ? CrmsFacilitiesSection.Empty(gc)
            : await CrmsAsync(query, ct);

        // Last, because it scores what the others found.
        var advisory = Gate(consumer, query, CustomerCheckSections.AiAdvisory) is { } gv
            ? AiAdvisorySection.Empty(gv)
            : await AdvisoryAsync(query, identity, bureau, fraud, accounts, loans, crms, state, now, ct);

        var sectionsRun = new List<string>();
        if (Ran(identity.Status)) sectionsRun.Add(CustomerCheckSections.Identity);
        if (Ran(bureau.Status)) sectionsRun.Add(CustomerCheckSections.CreditBureau);
        if (Ran(fraud.Status)) sectionsRun.Add(CustomerCheckSections.FraudCheck);
        if (Ran(accounts.Status)) sectionsRun.Add(CustomerCheckSections.BankAccounts);
        if (Ran(loans.Status)) sectionsRun.Add(CustomerCheckSections.BankLoans);
        if (Ran(crms.Status)) sectionsRun.Add(CustomerCheckSections.CrmsFacilities);
        if (Ran(advisory.Status)) sectionsRun.Add(CustomerCheckSections.AiAdvisory);

        var response = new CustomerCheckResponse(
            query.RequestId, now, consumer.Name,
            new CheckSubject(CustomerCheckRules.Last4(query.Bvn), query.Nubans.Select(CustomerCheckRules.MaskLast4).ToList()),
            sectionsRun, identity, bureau, fraud, accounts, loans, crms, advisory,
            new Dictionary<string, string>(), []);
        response = response with
        {
            Coverage = CustomerCheckRules.Coverage(response),
            Warnings = CustomerCheckRules.Warnings(response),
        };

        return new CustomerCheckOutcome(response, state.BureauUsed, state.BureauReused, state.Billed);
    }

    // ── Gates ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Null when the section should run; otherwise the status that explains why not.</summary>
    private static string? Gate(ApiConsumer consumer, CustomerCheckQuery query, string section)
        => !query.Wants(section) ? SectionStatuses.NotRequested
            : !consumer.Allows(section) ? SectionStatuses.NotAllowed
            : null;

    private static bool Ran(string status) => status is not (SectionStatuses.NotRequested or SectionStatuses.NotAllowed);

    // ── Identity ─────────────────────────────────────────────────────────────────────────────────

    private async Task<IdentitySection> IdentityAsync(CustomerCheckQuery query, RunState state, CancellationToken ct)
    {
        using var budget = Budget(_settings.BureauTimeoutMs, ct);
        try
        {
            var advanced = await _smartComply.VerifyBvnAdvancedAsync(query.Bvn, budget.Token);
            state.IdentityCalls++;
            if (advanced.IsSuccess)
            {
                state.BvnData = advanced.Value;
                return CustomerCheckRules.MapIdentity(advanced.Value);
            }
            _logger.LogWarning("Customer credit check: BVN advanced verification failed: {Error}", Safe(advanced.Error));

            var basic = await _smartComply.VerifyBvnAsync(query.Bvn, budget.Token);
            state.IdentityCalls++;
            if (basic.IsSuccess)
            {
                state.BvnBasic = basic.Value;
                return CustomerCheckRules.MapIdentity(basic.Value);
            }
            return IdentitySection.Empty(SectionStatuses.Unavailable, Safe(basic.Error));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return IdentitySection.Empty(SectionStatuses.Unavailable, "The identity provider did not answer in time.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Customer credit check: identity section failed.");
            return IdentitySection.Empty(SectionStatuses.Unavailable, "The identity provider could not be reached.");
        }
    }

    // ── Credit bureau ────────────────────────────────────────────────────────────────────────────

    private async Task<CreditBureauSection> BureauAsync(ApiConsumer consumer, CustomerCheckQuery query, RunState state, DateTime now, CancellationToken ct)
    {
        var order = _settings.BureauOrder.Select(Bureaus.Canonical).Where(b => b is not null).Distinct().ToList();
        if (order.Count == 0)
            order = Bureaus.Known.ToList();

        var skipped = new List<string>();
        var bvnHash = HashIdentifier(query.Bvn);

        foreach (var bureau in order)
        {
            // Reuse first: a stored report inside the consumer's window costs nothing.
            if (consumer.ReuseBureauReports)
            {
                var stored = await TryFindStoredAsync(bvnHash, bureau!, now.AddDays(-consumer.BureauReuseMaxAgeDays), ct);
                if (stored is not null && TryDeserialize<SmartComplyIndividualCreditReport>(stored.ReportJson) is { } storedReport)
                {
                    var storedScore = stored.ScoreJson is null ? null : TryDeserialize<SmartComplyCreditScore>(stored.ScoreJson);
                    if (storedScore is null && query.IncludeScore)
                    {
                        storedScore = await ScoreAsync(bureau!, query.Bvn, state, ct);
                        if (storedScore is not null)
                            await TryAttachScoreAsync(stored, storedScore, ct);
                    }
                    state.Report = storedReport;
                    state.BureauUsed = bureau;
                    state.BureauReused = true;
                    return CustomerCheckRules.MapBureau(storedReport, bureau!, true, stored.FetchedAt, storedScore, skipped);
                }
            }

            Result<SmartComplyIndividualCreditReport> result;
            using (var budget = Budget(_settings.BureauTimeoutMs, ct))
            {
                try
                {
                    result = await FetchReportAsync(bureau!, query.Bvn, budget.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    result = Result.Failure<SmartComplyIndividualCreditReport>("Request timed out");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Customer credit check: {Bureau} call failed.", bureau);
                    result = Result.Failure<SmartComplyIndividualCreditReport>("Connection error");
                }
            }

            if (result.IsSuccess)
            {
                state.BureauCalls++;
                state.Report = result.Value;
                state.BureauUsed = bureau;
                var score = query.IncludeScore ? await ScoreAsync(bureau!, query.Bvn, state, ct) : null;
                await TryStoreAsync(bvnHash, bureau!, result.Value, score, consumer.Id, ct);
                return CustomerCheckRules.MapBureau(result.Value, bureau!, false, now, score, skipped);
            }

            if (CustomerCheckRules.IsNoRecord(result.Error))
            {
                // An answer, not an error: the person is not known to this bureau. Stop here rather
                // than bill the next bureau for the same news.
                state.BureauCalls++;
                state.BureauUsed = bureau;
                return new CreditBureauSection(
                    SectionStatuses.NoRecord, $"{Bureaus.Label(bureau!)} holds no record for this BVN.",
                    Bureaus.Label(bureau!), skipped.Select(Bureaus.Label).ToList(), false, null, null, null, [], []);
            }

            _logger.LogWarning("Customer credit check: {Bureau} skipped: {Error}", bureau, Safe(result.Error));
            skipped.Add(bureau!);
        }

        return CreditBureauSection.Empty(
            SectionStatuses.Unavailable,
            $"No bureau answered ({string.Join(", ", skipped.Select(Bureaus.Label))}).",
            skipped.Select(Bureaus.Label).ToList());
    }

    private Task<Result<SmartComplyIndividualCreditReport>> FetchReportAsync(string bureau, string bvn, CancellationToken ct) => bureau switch
    {
        Bureaus.FirstCentral => _smartComply.GetFirstCentralFullAsync(bvn, ct),
        Bureaus.Crc => _smartComply.GetCRCFullAsync(bvn, ct),
        Bureaus.CreditRegistry => _smartComply.GetCreditRegistryFullAsync(bvn, ct),
        _ => Task.FromResult(Result.Failure<SmartComplyIndividualCreditReport>($"Unknown bureau '{bureau}'.")),
    };

    /// <summary>Best effort: a score that fails leaves the section without one.</summary>
    private async Task<SmartComplyCreditScore?> ScoreAsync(string bureau, string bvn, RunState state, CancellationToken ct)
    {
        using var budget = Budget(_settings.BureauTimeoutMs, ct);
        try
        {
            var result = bureau switch
            {
                Bureaus.FirstCentral => await _smartComply.GetFirstCentralScoreAsync(bvn, budget.Token),
                Bureaus.Crc => await _smartComply.GetCRCScoreAsync(bvn, budget.Token),
                _ => null,
            };
            if (result is null) return null;
            if (result.IsSuccess) state.ScoreCalls++;
            else _logger.LogWarning("Customer credit check: {Bureau} score failed: {Error}", bureau, Safe(result.Error));
            return result.IsSuccess ? result.Value : null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return null; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Customer credit check: {Bureau} score call failed.", bureau);
            return null;
        }
    }

    private async Task<CustomerBureauReport?> TryFindStoredAsync(string bvnHash, string bureau, DateTime notBefore, CancellationToken ct)
    {
        try { return await _repository.FindBureauReportAsync(bvnHash, bureau, notBefore, ct); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Customer credit check: could not read the stored bureau reports.");
            return null;
        }
    }

    private async Task TryStoreAsync(string bvnHash, string bureau, SmartComplyIndividualCreditReport report, SmartComplyCreditScore? score, Guid consumerId, CancellationToken ct)
    {
        try
        {
            var stored = CustomerBureauReport.Store(
                bvnHash, bureau, JsonSerializer.Serialize(report, Json),
                score is null ? null : JsonSerializer.Serialize(score, Json), consumerId);
            await _repository.AddBureauReportAsync(stored, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Customer credit check: could not store the {Bureau} report for reuse.", bureau);
        }
    }

    private async Task TryAttachScoreAsync(CustomerBureauReport stored, SmartComplyCreditScore score, CancellationToken ct)
    {
        try
        {
            stored.AttachScore(JsonSerializer.Serialize(score, Json));
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Customer credit check: could not attach the score to the stored report.");
        }
    }

    private static T? TryDeserialize<T>(string json) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(json, Json); }
        catch (JsonException) { return null; }
    }

    // ── Fraud ────────────────────────────────────────────────────────────────────────────────────

    private async Task<FraudCheckSection> FraudAsync(CustomerCheckQuery query, RunState state, CancellationToken ct)
    {
        using var budget = Budget(_settings.BureauTimeoutMs, ct);
        try
        {
            // The fraud check needs the person's name and date of birth. Take them from whatever
            // ran before; when nothing did, verify the BVN now (billed) rather than refuse.
            if (state.BvnData is null && state.BvnBasic is null && state.Report is null)
            {
                var advanced = await _smartComply.VerifyBvnAdvancedAsync(query.Bvn, budget.Token);
                state.IdentityCalls++;
                if (advanced.IsSuccess) state.BvnData = advanced.Value;
            }

            var first = state.BvnData?.FirstName ?? state.BvnBasic?.FirstName ?? NamePart(state.Report?.Name, 0);
            var last = state.BvnData?.LastName ?? state.BvnBasic?.LastName ?? NamePart(state.Report?.Name, -1);
            var other = state.BvnData?.MiddleName ?? state.BvnBasic?.MiddleName ?? NamePart(state.Report?.Name, 1);
            var dob = CustomerCheckRules.ParseDate(state.BvnData?.DateOfBirth ?? state.BvnBasic?.DateOfBirth ?? state.Report?.DateOfBirth);
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last))
                return FraudCheckSection.Empty(SectionStatuses.Unavailable, "The person's name could not be established from the BVN or a bureau report.");
            if (dob is null)
                return FraudCheckSection.Empty(SectionStatuses.Unavailable, "The person's date of birth could not be established from the BVN or a bureau report.");

            var supplied = query.LoanAmount is not null && query.TenorMonths is not null;
            var request = new SmartComplyIndividualLoanRequest(
                FirstName: first,
                LastName: last,
                OtherName: other,
                DateOfBirth: dob.Value.ToString("yyyy-MM-dd"),
                Gender: state.BvnData?.Gender ?? state.Report?.Gender ?? "Male",
                Country: "Nigeria",
                City: state.BvnData?.LgaOfResidence,
                CurrentAddress: state.BvnData?.ResidentialAddress ?? state.Report?.Address,
                Bvn: query.Bvn,
                PhoneNumber: state.BvnData?.PhoneNumber1 ?? state.BvnBasic?.PhoneNumber ?? state.Report?.Phone,
                EmailAddress: state.Report?.Email,
                EmploymentType: null,
                JobRole: null,
                EmployerName: null,
                AnnualIncome: 0,
                BankName: null,
                AccountNumber: null,
                LoanAmountRequested: query.LoanAmount ?? _settings.FraudDefaultLoanAmount,
                PurposeOfLoan: "Credit assessment",
                LoanRepaymentDurationType: "months",
                LoanRepaymentDurationValue: query.TenorMonths ?? _settings.FraudDefaultTenorMonths,
                CollateralRequired: false,
                CollateralValue: null,
                RunAmlCheck: query.RunAmlCheck);

            var result = await _smartComply.CheckIndividualLoanFraudAsync(request, budget.Token);
            if (result.IsFailure)
            {
                _logger.LogWarning("Customer credit check: fraud check failed: {Error}", Safe(result.Error));
                return FraudCheckSection.Empty(SectionStatuses.Unavailable, Safe(result.Error));
            }
            state.FraudCalls++;
            return CustomerCheckRules.MapFraud(result.Value, supplied, query.RunAmlCheck);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return FraudCheckSection.Empty(SectionStatuses.Unavailable, "The fraud-check provider did not answer in time.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Customer credit check: fraud section failed.");
            return FraudCheckSection.Empty(SectionStatuses.Unavailable, "The fraud-check provider could not be reached.");
        }
    }

    private static string? NamePart(string? fullName, int index)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return null;
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return null;
        if (index == -1) return parts.Length > 1 ? parts[^1] : null;
        if (index == 1) return parts.Length > 2 ? string.Join(" ", parts[1..^1]) : null;
        return parts[0];
    }

    // ── Bank accounts and loans ──────────────────────────────────────────────────────────────────

    private async Task<(BankAccountsSection, BankLoansSection)> BankAsync(CustomerCheckQuery query, RunState state, DateTime now, CancellationToken ct)
    {
        // Each core banking call gets its own timeout; the section as a whole is capped so a
        // customer with many loans cannot run the check indefinitely.
        using var section = Budget(_settings.BankSectionTimeoutMs, ct);

        // Core banking is searched by BVN (the client's external id). The answer is every customer
        // record carrying it, each with its savings accounts attached (numbers and balances only).
        Result<IReadOnlyList<FineractClientMatch>> found;
        try
        {
            using var call = Budget(_settings.CoreBankingTimeoutMs, section.Token);
            found = await _fineract.FindClientsAsync(new FineractClientSearch(ExternalId: query.Bvn), true, call.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Both(SectionStatuses.Unavailable, "Core banking did not answer in time.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Customer credit check: core banking customer search failed.");
            return Both(SectionStatuses.Unavailable, "Core banking could not be reached.");
        }

        if (found.IsFailure)
            return Both(SectionStatuses.Unavailable, Safe(found.Error));
        if (found.Value.Count == 0)
            return Both(SectionStatuses.NoRecord, "No customer in core banking carries this BVN.");

        var clients = found.Value;
        var primary = clients[0];
        var notes = new List<string>();
        if (clients.Count > 1)
            notes.Add($"{clients.Count} customer records carry this BVN; accounts and loans from all of them are listed.");

        var savings = new List<(ClientSavingsAccountSummary Account, string? Nuban)>();
        var loans = new List<BankLoan>();
        var fineractErrors = new List<string>();

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
                _logger.LogError(ex, "Customer credit check: client accounts read failed.");
                summary = Result.Failure<ClientAccountSummary>("Core banking could not be reached.");
            }

            if (summary.IsFailure)
            {
                // The search already carried the savings accounts (number, product, balance; no
                // status or dates), so they are still listed. Loans only come from the full read.
                fineractErrors.Add(Safe(summary.Error));
                savings.AddRange(client.SavingsAccounts.Select(s => (s, NubanOf(s))));
                continue;
            }

            savings.AddRange(summary.Value.SavingsAccounts.Select(s => (s, NubanOf(s))));
            foreach (var l in summary.Value.LoanAccounts.Where(l => CustomerCheckRules.IsBookedLoan(l.StatusCode)))
                loans.Add(await LoanAsync(l, now, section.Token, ct));
        }

        foreach (var named in query.Nubans.Where(n => savings.All(s => s.Nuban != n)))
            notes.Add($"Account {CustomerCheckRules.MaskLast4(named)} is not held by this customer and is not reported.");

        // Statements are read for a bounded number of accounts: the ones the request named first,
        // then active accounts, then by balance.
        var withNuban = savings.Where(s => s.Nuban is not null).ToList();
        var activityFor = withNuban
            .OrderByDescending(s => query.Nubans.Contains(s.Nuban!))
            .ThenByDescending(s => CustomerCheckRules.SavingsStatus(s.Account.Status) == CheckAccountStatuses.Active)
            .ThenByDescending(s => s.Account.AccountBalance)
            .Take(Math.Max(0, _settings.MaxActivityAccounts))
            .Select(s => s.Nuban!)
            .ToHashSet();
        if (withNuban.Count > activityFor.Count)
            notes.Add($"Transaction activity was read for {activityFor.Count} of {savings.Count} accounts.");

        var accounts = new List<BankAccount>();
        foreach (var (s, nuban) in savings)
        {
            var activity = nuban is not null && activityFor.Contains(nuban)
                ? await ActivityAsync(nuban, query.ActivityWindowMonths, s.ActivatedOn, now, notes, state, section.Token, ct)
                : null;
            accounts.Add(new BankAccount(
                CustomerCheckRules.Last4(nuban ?? s.AccountNo), s.ProductName,
                (s.AccountType ?? "Savings").ToUpperInvariant(),
                CustomerCheckRules.SavingsStatus(s.Status), string.IsNullOrWhiteSpace(s.Status) ? null : s.Status,
                CustomerCheckRules.Date(s.ActivatedOn), Math.Round(s.AccountBalance, 2), "NGN",
                CustomerCheckRules.Date(s.LastActiveTransactionDate) ?? activity?.LastTransaction, activity));
        }

        var remarks = notes.Concat(fineractErrors).ToList();
        var accountsSection = new BankAccountsSection(
            SectionStatuses.Ok, remarks.Count == 0 ? null : string.Join(" ", remarks),
            BvnMatchResults.Match, primary.Id.ToString(), primary.Status, primary.KycLevel?.ToString(),
            CustomerCheckRules.Date(primary.ActivatedOn), accounts);

        var loansSection = fineractErrors.Count > 0 && loans.Count == 0
            ? BankLoansSection.Empty(SectionStatuses.Unavailable, string.Join("; ", fineractErrors))
            : new BankLoansSection(SectionStatuses.Ok, fineractErrors.Count == 0 ? null : string.Join("; ", fineractErrors), loans);

        return (accountsSection, loansSection);

        static (BankAccountsSection, BankLoansSection) Both(string status, string why)
            => (BankAccountsSection.Empty(status, why), BankLoansSection.Empty(status, why));

        // The NUBAN is the savings account's external id; the Fineract account number is a
        // different, shorter thing. Only a 10-digit value is treated as one.
        static string? NubanOf(ClientSavingsAccountSummary s)
        {
            var ext = Identifiers.Digits(s.ExternalId);
            if (ext is { Length: 10 } && ext.All(char.IsAsciiDigit)) return ext;
            var no = Identifiers.Digits(s.AccountNo);
            return no is { Length: 10 } && no.All(char.IsAsciiDigit) ? no : null;
        }
    }

    /// <summary>The account's activity from its statement, or null with the reason appended to
    /// <paramref name="notes"/> so the partner can tell a quiet account from an unread one.</summary>
    private async Task<AccountActivity?> ActivityAsync(string nuban, int windowMonths, DateTime? openedDate, DateTime now, List<string> notes, RunState state, CancellationToken section, CancellationToken ct)
    {
        var masked = CustomerCheckRules.MaskLast4(nuban);
        try
        {
            using var call = Budget(_settings.CoreBankingTimeoutMs, section);
            var statement = await _coreBanking.GetStatementAsync(nuban, now.AddMonths(-windowMonths), now, call.Token);
            if (statement.IsFailure)
            {
                _logger.LogWarning("Customer credit check: statement read failed: {Error}", Safe(statement.Error));
                notes.Add($"The statement for account {masked} could not be read, so its activity is not reported.");
                return null;
            }
            // Kept for the advisory's cashflow assessment; never written anywhere.
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
            _logger.LogError(ex, "Customer credit check: statement read failed.");
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
        catch (Exception ex) { _logger.LogError(ex, "Customer credit check: loan detail read failed."); }

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

    // ── CRMS facilities ──────────────────────────────────────────────────────────────────────────

    private async Task<CrmsFacilitiesSection> CrmsAsync(CustomerCheckQuery query, CancellationToken ct)
    {
        using var budget = Budget(_settings.CrmsLookupTimeoutMs, ct);
        try
        {
            var exposure = await _partyExposure.LookupAsync(
                query.RequestId, new ExposureQuery(query.Bvn, null, query.Nubans, true, 36, true), budget.Token);
            var summary = new CrmsFacilitySummary(
                exposure.Summary.DirectFacilities, exposure.Summary.IndirectFacilities,
                exposure.Summary.ActiveDirectOutstanding, exposure.Summary.BalancesComplete);
            var relationships = exposure.Relationships.Select(r => new CrmsFacility(
                r.RelationshipType, r.Track, r.CaseReference, r.FacilityType, r.Obligor.DisplayName, r.Status,
                r.Amounts.Approved, r.GuaranteeAmount, r.Amounts.Outstanding, r.DaysPastDue, r.BalanceSource)).ToList();
            return new CrmsFacilitiesSection(SectionStatuses.Ok, null, exposure.MatchStatus, summary, relationships);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return CrmsFacilitiesSection.Empty(SectionStatuses.Unavailable, "The CRMS lookup did not finish in time.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Customer credit check: CRMS facilities section failed.");
            return CrmsFacilitiesSection.Empty(SectionStatuses.Unavailable, "The CRMS lookup failed.");
        }
    }

    // ── Advisory ─────────────────────────────────────────────────────────────────────────────────

    private async Task<AiAdvisorySection> AdvisoryAsync(
        CustomerCheckQuery query, IdentitySection identity, CreditBureauSection bureau, FraudCheckSection fraud,
        BankAccountsSection accounts, BankLoansSection loans, CrmsFacilitiesSection crms, RunState state, DateTime now, CancellationToken ct)
    {
        using var budget = Budget(_settings.AdvisoryTimeoutMs, ct);
        try
        {
            var cashflow = CustomerCheckRules.CashflowInput(state.Statements, query.ActivityWindowMonths, accounts.Accounts.Sum(a => a.Balance ?? 0), now);
            var input = CustomerCheckRules.AdvisoryInput(Guid.NewGuid(), query, _settings, identity, bureau, fraud, accounts, loans, crms, cashflow, now);
            if (!CustomerCheckRules.AdvisoryHasBasis(input))
                return AiAdvisorySection.Empty(SectionStatuses.Unavailable, "Nothing to assess: no bureau, bank or identity data was returned.");

            var result = await _advisory.GenerateCustomerAdvisoryAsync(input, budget.Token);
            if (!result.Success)
                return AiAdvisorySection.Empty(SectionStatuses.Unavailable, Safe(result.ErrorMessage));
            state.AdvisoryCalls++;
            return CustomerCheckRules.MapAdvisory(result, input, now);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return AiAdvisorySection.Empty(SectionStatuses.Unavailable, "The advisory did not finish in time.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Customer credit check: advisory section failed.");
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

    /// <summary>Provider error text with any long digit run (a BVN or account number echoed back)
    /// blanked, so it can sit in a reply or a log line.</summary>
    private static string Safe(string? error) =>
        string.IsNullOrWhiteSpace(error) ? "The provider returned no detail." : LongDigitRuns.Replace(error, "****");

    /// <summary>What the sections learn from one another during a single check.</summary>
    private sealed class RunState
    {
        public SmartComplyBvnAdvancedResult? BvnData;
        public SmartComplyBvnResult? BvnBasic;
        public SmartComplyIndividualCreditReport? Report;
        public string? BureauUsed;
        public bool BureauReused;
        public int IdentityCalls, BureauCalls, ScoreCalls, FraudCalls, AdvisoryCalls;
        /// <summary>Statements read for account activity, kept in memory for the advisory only.</summary>
        public readonly List<IReadOnlyList<StatementTransaction>> Statements = [];
        public BilledCallCounts Billed => new(IdentityCalls, BureauCalls, ScoreCalls, FraudCalls, AdvisoryCalls);
    }
}
