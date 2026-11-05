using CRMS.Application.Advisory.Interfaces;
using CRMS.Application.CustomerCheck;
using CRMS.Application.PartyExposure;
using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Common;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.ExternalServices.Rhshf;
using CRMS.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>The customer check (docs/Customer_Check.md): bureau fallback, reuse, customer found by BVN,
/// accounts and loans, section isolation, and the gates.</summary>
public class CustomerCheckServiceTests
{
    private const string Bvn = "22212345678";
    private const string Nuban = "0123456789";
    private static readonly DateTime Now = DateTime.UtcNow;

    private readonly Mock<ISmartComplyProvider> _smartComply = new(MockBehavior.Strict);
    private readonly Mock<ICoreBankingService> _coreBanking = new(MockBehavior.Strict);
    private readonly Mock<IFineractDirectService> _fineract = new(MockBehavior.Strict);
    private readonly Mock<IPartyExposureService> _partyExposure = new();
    private readonly Mock<IAIAdvisoryService> _advisory = new();
    private readonly Mock<ICustomerCheckRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly CustomerCheckSettings _settings = new();

    public CustomerCheckServiceTests()
    {
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _repository.Setup(r => r.FindBureauReportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerBureauReport?)null);
        _partyExposure.Setup(p => p.LookupAsync(It.IsAny<string>(), It.IsAny<ExposureQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyExposure());
        _advisory.Setup(a => a.GenerateCustomerAdvisoryAsync(It.IsAny<CustomerAdvisoryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerAdvisoryRequest r, CancellationToken _) => SampleAdvisory(r));
    }

    private static CustomerAdvisoryResponse SampleAdvisory(CustomerAdvisoryRequest r) => new(
        true, null,
        [new RiskScoreOutput("CreditHistory", 72m, 0.40m, "Low", "sample", [], ["ok"])],
        [new CustomerAdvisoryGap("RepaymentCapacity", 0.25m, "No statement was read.")],
        72m, "Low", "Approve", r.ProposalSupplied ? r.ProposedAmount : null, null, ["c1"], [], "summary", "s", "w", null, null, false, "test");

    private CustomerCheckService Service() => new(
        _smartComply.Object, _coreBanking.Object, _fineract.Object, _partyExposure.Object, _advisory.Object,
        _repository.Object, _unitOfWork.Object, Options.Create(_settings),
        Options.Create(new RhshfSettings { CallbackSigningSecret = "secret" }),
        NullLogger<CustomerCheckService>.Instance);

    private static ApiConsumer Consumer(IEnumerable<string>? sections = null, bool reuse = false, int reuseDays = 30)
        => ApiConsumer.Create("AgriPay", ApiKeys.Hash("k"), "crms_abc", sections ?? CustomerCheckSections.All, 30, reuse, reuseDays, null, Guid.NewGuid()).Value;

    private static CustomerCheckQuery Query(IEnumerable<string>? sections = null, params string[] nubans)
        => new("req-1", Bvn, nubans, (sections ?? CustomerCheckSections.All).ToList(), 12, true, false, null, null);

    // ── Bureau fallback ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Bureau_that_errors_is_skipped_and_the_next_one_answers()
    {
        _smartComply.Setup(s => s.GetFirstCentralFullAsync(Bvn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SmartComplyIndividualCreditReport>("Connection error: boom"));
        _smartComply.Setup(s => s.GetCRCFullAsync(Bvn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Report()));
        _smartComply.Setup(s => s.GetCRCScoreAsync(Bvn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new SmartComplyCreditScore(640, "Good", "CRC", null)));

        var outcome = await Service().CheckAsync(Consumer(), Query([CustomerCheckSections.CreditBureau]));
        var bureau = outcome.Response.CreditBureau;

        Assert.Equal(SectionStatuses.Ok, bureau.Status);
        Assert.Equal("CRC", bureau.Bureau);
        Assert.Equal(["FIRST_CENTRAL"], bureau.BureausSkipped);
        Assert.False(bureau.Reused);
        Assert.Equal(640, bureau.Score!.Value);
        Assert.Equal(Bureaus.Crc, outcome.BureauUsed);
        Assert.Equal(2, outcome.BilledCalls);
        _smartComply.Verify(s => s.GetCreditRegistryFullAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.AddBureauReportAsync(It.Is<CustomerBureauReport>(b => b.Bureau == Bureaus.Crc), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Bureau_with_no_record_is_an_answer_and_stops_the_search()
    {
        _smartComply.Setup(s => s.GetFirstCentralFullAsync(Bvn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SmartComplyIndividualCreditReport>("Subject not found in credit bureau"));

        var outcome = await Service().CheckAsync(Consumer(), Query([CustomerCheckSections.CreditBureau]));
        var bureau = outcome.Response.CreditBureau;

        Assert.Equal(SectionStatuses.NoRecord, bureau.Status);
        Assert.Equal("FIRST_CENTRAL", bureau.Bureau);
        Assert.Empty(bureau.BureausSkipped);
        _smartComply.Verify(s => s.GetCRCFullAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains("no record", outcome.Response.Coverage[CustomerCheckSections.CreditBureau]);
    }

    [Fact]
    public async Task Every_bureau_failing_leaves_the_section_unavailable_and_names_them()
    {
        _smartComply.Setup(s => s.GetFirstCentralFullAsync(Bvn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SmartComplyIndividualCreditReport>("Request timed out"));
        _smartComply.Setup(s => s.GetCRCFullAsync(Bvn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SmartComplyIndividualCreditReport>("API error 500"));
        _smartComply.Setup(s => s.GetCreditRegistryFullAsync(Bvn, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));

        var outcome = await Service().CheckAsync(Consumer(), Query([CustomerCheckSections.CreditBureau]));
        var bureau = outcome.Response.CreditBureau;

        Assert.Equal(SectionStatuses.Unavailable, bureau.Status);
        Assert.Equal(["FIRST_CENTRAL", "CRC", "CREDIT_REGISTRY"], bureau.BureausSkipped);
        Assert.Null(outcome.BureauUsed);
        Assert.Equal(0, outcome.BilledCalls);
    }

    [Fact]
    public async Task Stored_report_is_reused_when_the_consumer_allows_it()
    {
        var stored = CustomerBureauReport.Store("hash", Bureaus.FirstCentral,
            System.Text.Json.JsonSerializer.Serialize(Report(), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
            null, Guid.NewGuid());
        _repository.Setup(r => r.FindBureauReportAsync(It.IsAny<string>(), Bureaus.FirstCentral, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);
        // The stored copy has no score, so the score is still fetched and attached.
        _smartComply.Setup(s => s.GetFirstCentralScoreAsync(Bvn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new SmartComplyCreditScore(612, "Fair", "FC", null)));

        var outcome = await Service().CheckAsync(Consumer(reuse: true), Query([CustomerCheckSections.CreditBureau]));
        var bureau = outcome.Response.CreditBureau;

        Assert.Equal(SectionStatuses.Ok, bureau.Status);
        Assert.True(bureau.Reused);
        Assert.True(outcome.BureauReused);
        Assert.Equal(stored.FetchedAt, bureau.ReportDate);
        Assert.Equal(612, bureau.Score!.Value);
        Assert.NotNull(stored.ScoreJson);
        Assert.Equal(1, outcome.BilledCalls);
        _smartComply.Verify(s => s.GetFirstCentralFullAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains("reused", outcome.Response.Coverage[CustomerCheckSections.CreditBureau]);
    }

    [Fact]
    public async Task Stored_report_is_ignored_when_reuse_is_off()
    {
        _smartComply.Setup(s => s.GetFirstCentralFullAsync(Bvn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Report()));
        _smartComply.Setup(s => s.GetFirstCentralScoreAsync(Bvn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SmartComplyCreditScore>("no score"));

        var outcome = await Service().CheckAsync(Consumer(reuse: false), Query([CustomerCheckSections.CreditBureau]));

        Assert.False(outcome.Response.CreditBureau.Reused);
        Assert.Null(outcome.Response.CreditBureau.Score);
        _repository.Verify(r => r.FindBureauReportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Gates and isolation ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sections_not_requested_or_not_allowed_are_marked_and_never_run()
    {
        var consumer = Consumer([CustomerCheckSections.Identity]);
        _smartComply.Setup(s => s.VerifyBvnAdvancedAsync(Bvn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(BvnData()));

        var outcome = await Service().CheckAsync(consumer, Query([CustomerCheckSections.Identity, CustomerCheckSections.CreditBureau]));
        var r = outcome.Response;

        Assert.Equal(SectionStatuses.Ok, r.Identity.Status);
        Assert.Equal("ADEBAYO MUSA OKORO", r.Identity.FullName);
        Assert.Equal("0803****567", r.Identity.PhoneNumber);
        Assert.Equal(SectionStatuses.NotAllowed, r.CreditBureau.Status);
        Assert.Equal(SectionStatuses.NotRequested, r.FraudCheck.Status);
        Assert.Equal(SectionStatuses.NotRequested, r.BankAccounts.Status);
        Assert.Equal(SectionStatuses.NotRequested, r.CrmsFacilities.Status);
        // Only sections that actually ran are listed; a section the consumer may not ask for did not.
        Assert.Equal([CustomerCheckSections.Identity], r.SectionsRun);
        Assert.Equal("5678", r.Subject.BvnLast4);
        _smartComply.Verify(s => s.VerifyBvnAdvancedAsync(Bvn, It.IsAny<CancellationToken>()), Times.Once);
        _smartComply.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task One_section_failing_does_not_blank_the_others()
    {
        _smartComply.Setup(s => s.VerifyBvnAdvancedAsync(Bvn, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("identity provider exploded"));
        _smartComply.Setup(s => s.GetFirstCentralFullAsync(Bvn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Report()));
        _smartComply.Setup(s => s.GetFirstCentralScoreAsync(Bvn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SmartComplyCreditScore>("none"));
        _smartComply.Setup(s => s.CheckIndividualLoanFraudAsync(It.IsAny<SmartComplyIndividualLoanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Fraud(82)));

        var outcome = await Service().CheckAsync(Consumer(),
            Query([CustomerCheckSections.Identity, CustomerCheckSections.CreditBureau, CustomerCheckSections.FraudCheck]));
        var r = outcome.Response;

        Assert.Equal(SectionStatuses.Unavailable, r.Identity.Status);
        Assert.Equal(SectionStatuses.Ok, r.CreditBureau.Status);
        Assert.Equal(SectionStatuses.Ok, r.FraudCheck.Status);
        Assert.Equal("HIGH", r.FraudCheck.RiskBand);
        Assert.False(r.FraudCheck.FinancialAnalysis!.Assessed);
        Assert.Contains("high risk", string.Join(" ", r.Warnings));
    }

    [Fact]
    public async Task Fraud_check_takes_its_identity_from_the_bureau_report_and_uses_the_defaults()
    {
        SmartComplyIndividualLoanRequest? sent = null;
        _smartComply.Setup(s => s.GetFirstCentralFullAsync(Bvn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Report()));
        _smartComply.Setup(s => s.GetFirstCentralScoreAsync(Bvn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SmartComplyCreditScore>("none"));
        _smartComply.Setup(s => s.CheckIndividualLoanFraudAsync(It.IsAny<SmartComplyIndividualLoanRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SmartComplyIndividualLoanRequest, CancellationToken>((req, _) => sent = req)
            .ReturnsAsync(Result.Success(Fraud(20)));

        await Service().CheckAsync(Consumer(), Query([CustomerCheckSections.CreditBureau, CustomerCheckSections.FraudCheck]));

        Assert.NotNull(sent);
        Assert.Equal("ADEBAYO", sent!.FirstName);
        Assert.Equal("OKORO", sent.LastName);
        Assert.Equal("MUSA", sent.OtherName);
        Assert.Equal("1985-03-14", sent.DateOfBirth);
        Assert.Equal(_settings.FraudDefaultLoanAmount, sent.LoanAmountRequested);
        Assert.Equal(_settings.FraudDefaultTenorMonths, sent.LoanRepaymentDurationValue);
        _smartComply.Verify(s => s.VerifyBvnAdvancedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Fraud_check_without_a_date_of_birth_is_unavailable_not_guessed()
    {
        _smartComply.Setup(s => s.VerifyBvnAdvancedAsync(Bvn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SmartComplyBvnAdvancedResult>("nope"));

        var outcome = await Service().CheckAsync(Consumer(), Query([CustomerCheckSections.FraudCheck]));

        Assert.Equal(SectionStatuses.Unavailable, outcome.Response.FraudCheck.Status);
        _smartComply.Verify(s => s.CheckIndividualLoanFraudAsync(It.IsAny<SmartComplyIndividualLoanRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Bank accounts and loans ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Bank_sections_find_the_customer_by_bvn_and_list_every_account_and_loan()
    {
        FindsClients(Client(1001));
        _fineract.Setup(f => f.GetClientAccountsAsync(1001, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ClientAccountSummary(1001,
                [
                    new ClientLoanAccountSummary(5001, "LN-5001", "RH-SHF Inputs", 1, "Active", 300, "individual"),
                    new ClientLoanAccountSummary(5002, "LN-5002", "Old Lease", 2, "Closed", 600, "individual"),
                    new ClientLoanAccountSummary(5003, "LN-5003", "Rejected", 3, "Rejected", 500, "individual"),
                ],
                [
                    new ClientSavingsAccountSummary(9001, "000009001", "Farmers Savings", "Active", 142_300.55m, Nuban, "Savings", Now.AddYears(-7), Now.AddDays(-2)),
                    new ClientSavingsAccountSummary(9002, "000009002", "Current", "Dormant", 0m, "0000000002", "Current", Now.AddYears(-3), Now.AddYears(-2)),
                ])));
        _coreBanking.Setup(c => c.GetStatementAsync(Nuban, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new AccountStatement(Nuban, Now.AddMonths(-12), Now, 0, 0, 0, 0,
            [
                new StatementTransaction("1", Now.AddMonths(-3), "salary", 1000m, TransactionType.Credit, 1000m, null),
                new StatementTransaction("2", Now.AddDays(-2), "atm", 100m, TransactionType.Debit, 900m, null),
            ])));
        _coreBanking.Setup(c => c.GetStatementAsync("0000000002", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<AccountStatement>("no statement for a dormant account"));
        _fineract.Setup(f => f.GetLoanDetailAsync(5001, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(LoanDetail(5001, "LN-5001", 300, 2_500_000m, 1_625_000m,
            [
                (Now.AddDays(-35), 0m, true),
                (Now.AddDays(-5), 112_500m, false),
                (Now.AddDays(25), 112_500m, false),
            ])));
        _fineract.Setup(f => f.GetLoanDetailAsync(5002, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(LoanDetail(5002, "LN-5002", 600, 1_200_000m, 0m, [])));

        // No account number in the request: the BVN alone finds the customer.
        var outcome = await Service().CheckAsync(Consumer(), Query([CustomerCheckSections.BankAccounts, CustomerCheckSections.BankLoans]));
        var r = outcome.Response;

        Assert.Equal(SectionStatuses.Ok, r.BankAccounts.Status);
        Assert.Equal(BvnMatchResults.Match, r.BankAccounts.BvnMatch);
        Assert.Equal("1001", r.BankAccounts.ClientId);
        Assert.Equal("Active", r.BankAccounts.ClientStatus);
        Assert.Equal("2", r.BankAccounts.KycLevel);
        Assert.Equal(DateOnly.FromDateTime(Now.AddYears(-7)), r.BankAccounts.CustomerSince);
        Assert.Contains("****0002", r.BankAccounts.Reason);
        Assert.Equal(2, r.BankAccounts.Accounts.Count);

        var main = r.BankAccounts.Accounts.Single(a => a.AccountNumberLast4 == "6789");
        Assert.Equal(CheckAccountStatuses.Active, main.Status);
        Assert.Equal("SAVINGS", main.AccountType);
        Assert.NotNull(main.Activity);
        Assert.Equal(2, main.Activity!.TransactionsInWindow);
        Assert.Equal(1000m, main.Activity.CreditsInWindow);
        Assert.Equal(84, main.Activity.MonthsSinceOpened);

        var dormant = r.BankAccounts.Accounts.Single(a => a.AccountNumberLast4 == "0002");
        Assert.Equal(CheckAccountStatuses.Dormant, dormant.Status);
        Assert.Null(dormant.Activity);

        Assert.Equal(SectionStatuses.Ok, r.BankLoans.Status);
        Assert.Equal(2, r.BankLoans.Loans.Count);
        var running = r.BankLoans.Loans.Single(l => l.LoanAccountLast4 == "5001");
        Assert.Equal(CheckLoanStatuses.Overdue, running.Status);
        Assert.Equal(112_500m, running.OverdueAmount);
        Assert.Equal(5, running.DaysPastDue);
        Assert.Equal(DateOnly.FromDateTime(Now.AddDays(25)), running.NextDueDate);
        var paid = r.BankLoans.Loans.Single(l => l.LoanAccountLast4 == "5002");
        Assert.Equal(CheckLoanStatuses.Paid, paid.Status);
        Assert.Contains(r.Warnings, w => w.Contains("5 days past due"));
        Assert.Contains(CustomerCheckSections.BankAccounts, r.SectionsRun);

        // Both accounts carry a NUBAN, so both statements were read; nothing else was asked of core banking.
        _coreBanking.Verify(c => c.GetStatementAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _coreBanking.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task No_customer_with_the_bvn_is_no_record_and_nothing_else_is_read()
    {
        FindsClients();

        var outcome = await Service().CheckAsync(Consumer(), Query([CustomerCheckSections.BankAccounts, CustomerCheckSections.BankLoans]));
        var r = outcome.Response;

        Assert.Equal(SectionStatuses.NoRecord, r.BankAccounts.Status);
        Assert.Equal(SectionStatuses.NoRecord, r.BankLoans.Status);
        Assert.Empty(r.BankAccounts.Accounts);
        Assert.Empty(r.BankLoans.Loans);
        Assert.Contains("no customer", r.Coverage[CustomerCheckSections.BankAccounts]);
        _fineract.Verify(f => f.GetClientAccountsAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        _coreBanking.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Customer_search_failure_is_unavailable_and_leaves_other_sections_alone()
    {
        _fineract.Setup(f => f.FindClientsAsync(It.IsAny<FineractClientSearch>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<IReadOnlyList<FineractClientMatch>>("Fineract returned 503"));

        var outcome = await Service().CheckAsync(Consumer(), Query([CustomerCheckSections.BankAccounts, CustomerCheckSections.BankLoans, CustomerCheckSections.CrmsFacilities]));
        var r = outcome.Response;

        Assert.Equal(SectionStatuses.Unavailable, r.BankAccounts.Status);
        Assert.Contains("503", r.BankAccounts.Reason);
        Assert.Equal(SectionStatuses.Unavailable, r.BankLoans.Status);
        Assert.Equal(SectionStatuses.Ok, r.CrmsFacilities.Status);
    }

    [Fact]
    public async Task Accounts_named_in_the_request_go_first_for_activity_and_unknown_ones_are_pointed_out()
    {
        _settings.MaxActivityAccounts = 1;
        FindsClients(Client(1001));
        _fineract.Setup(f => f.GetClientAccountsAsync(1001, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ClientAccountSummary(1001, [],
                [
                    new ClientSavingsAccountSummary(9001, "000009001", "Big Current", "Active", 900_000m, "1111111111", "Current", Now.AddYears(-4), Now.AddDays(-1)),
                    new ClientSavingsAccountSummary(9002, "000009002", "Small Savings", "Active", 10m, Nuban, "Savings", Now.AddYears(-1), Now.AddDays(-9)),
                ])));
        _coreBanking.Setup(c => c.GetStatementAsync(Nuban, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new AccountStatement(Nuban, Now.AddMonths(-12), Now, 0, 0, 0, 0, [])));

        // The request names the small account and one the customer does not hold.
        var outcome = await Service().CheckAsync(Consumer(), Query([CustomerCheckSections.BankAccounts], Nuban, "2222222222"));
        var r = outcome.Response;

        Assert.Equal(SectionStatuses.Ok, r.BankAccounts.Status);
        Assert.Equal(2, r.BankAccounts.Accounts.Count);
        Assert.NotNull(r.BankAccounts.Accounts.Single(a => a.AccountNumberLast4 == "6789").Activity);
        Assert.Null(r.BankAccounts.Accounts.Single(a => a.AccountNumberLast4 == "1111").Activity);
        Assert.Contains("****2222", r.BankAccounts.Reason);
        Assert.Contains("1 of 2", r.BankAccounts.Reason);
        _coreBanking.Verify(c => c.GetStatementAsync(Nuban, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _coreBanking.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Accounts_from_the_search_still_come_back_when_the_client_list_cannot_be_read()
    {
        FindsClients(Client(1001, new ClientSavingsAccountSummary(17, "000000017", "Savings Account Product", "", 2_958_236.25m, Nuban)));
        _fineract.Setup(f => f.GetClientAccountsAsync(1001, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ClientAccountSummary>("Fineract returned 503"));
        _coreBanking.Setup(c => c.GetStatementAsync(Nuban, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<AccountStatement>("statement down"));

        var outcome = await Service().CheckAsync(Consumer(), Query([CustomerCheckSections.BankAccounts, CustomerCheckSections.BankLoans]));
        var r = outcome.Response;

        Assert.Equal(SectionStatuses.Ok, r.BankAccounts.Status);
        Assert.Contains("503", r.BankAccounts.Reason);
        var account = Assert.Single(r.BankAccounts.Accounts);
        Assert.Equal("6789", account.AccountNumberLast4);
        Assert.Equal(CheckAccountStatuses.Unknown, account.Status);
        Assert.Null(account.RawStatus);
        Assert.Equal(2_958_236.25m, account.Balance);
        Assert.Null(account.OpenedDate);
        Assert.Null(account.Activity);
        Assert.Equal(SectionStatuses.Unavailable, r.BankLoans.Status);
    }

    [Fact]
    public async Task Several_customer_records_with_the_bvn_are_all_listed()
    {
        FindsClients(Client(1001), Client(1002));
        _fineract.Setup(f => f.GetClientAccountsAsync(1001, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ClientAccountSummary(1001, [],
                [new ClientSavingsAccountSummary(9001, "000009001", "Savings", "Active", 5m, Nuban, "Savings", Now.AddYears(-2), null)])));
        _fineract.Setup(f => f.GetClientAccountsAsync(1002, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ClientAccountSummary(1002,
                [new ClientLoanAccountSummary(5001, "LN-5001", "Inputs", 1, "Active", 300, "individual")],
                [new ClientSavingsAccountSummary(9002, "000009002", "Savings", "Active", 7m, "3333333333", "Savings", Now.AddYears(-1), null)])));
        _fineract.Setup(f => f.GetLoanDetailAsync(5001, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(LoanDetail(5001, "LN-5001", 300, 100_000m, 80_000m, [(Now.AddDays(10), 20_000m, false)])));
        _coreBanking.Setup(c => c.GetStatementAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string n, DateTime f, DateTime t, CancellationToken _) => Result.Success(new AccountStatement(n, f, t, 0, 0, 0, 0, [])));

        var outcome = await Service().CheckAsync(Consumer(), Query([CustomerCheckSections.BankAccounts, CustomerCheckSections.BankLoans]));
        var r = outcome.Response;

        Assert.Equal("1001", r.BankAccounts.ClientId);
        Assert.Equal(2, r.BankAccounts.Accounts.Count);
        Assert.Contains("2 customer records", r.BankAccounts.Reason);
        var loan = Assert.Single(r.BankLoans.Loans);
        Assert.Equal(CheckLoanStatuses.Running, loan.Status);
    }

    // ── CRMS facilities ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Crms_facilities_are_reduced_from_the_party_exposure_lookup()
    {
        var exposure = EmptyExposure() with
        {
            MatchStatus = MatchStatuses.Match,
            Summary = new ExposureSummary(1, 0, 1_625_000m, null, null, 0, "NGN", true),
            Relationships =
            [
                new ExposureRelationship(RelationshipTypes.Borrower, null, [IdentifierTypes.Bvn], "P-1", "RHSHF", "RHSHF-2025-00318", null, "FYP",
                    "Dry-season inputs", new ExposureObligor(ObligorTypes.Corporate, "Grandbai Limited"), FacilityStatuses.Active, null,
                    new ExposureAmounts(2_500_000m, 2_500_000m, 1_625_000m, 0m), null, 0, null,
                    new ExposureDates(null, null, null, null, null), BalanceSources.CoreBankingLive, Now, Now, null),
            ],
        };
        _partyExposure.Setup(p => p.LookupAsync("req-1", It.Is<ExposureQuery>(q => q.Bvn == Bvn && q.FetchLiveBalances), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exposure);

        var outcome = await Service().CheckAsync(Consumer(), Query([CustomerCheckSections.CrmsFacilities]));
        var section = outcome.Response.CrmsFacilities;

        Assert.Equal(SectionStatuses.Ok, section.Status);
        Assert.Equal(MatchStatuses.Match, section.MatchStatus);
        Assert.Equal(1, section.Summary!.Direct);
        var f = Assert.Single(section.Relationships);
        Assert.Equal("BORROWER", f.Role);
        Assert.Equal("Grandbai Limited", f.Obligor);
        Assert.Equal(1_625_000m, f.Outstanding);
        Assert.Equal(BalanceSources.CoreBankingLive, f.BalanceSource);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    private static SmartComplyIndividualCreditReport Report()
        => new("id", Bvn, "ADEBAYO MUSA OKORO", "08031234567", "Male", "1985-03-14", "Kaduna", "a@b.c",
            new SmartComplyCreditSummary(5, 3, 2, 3, 1, 1, 2_500_000m, 100_000m, 6_100_000m, 1_840_000m, 215_000m, 94, "ORD-1"),
            [], [], [], [], Now);

    private static SmartComplyBvnAdvancedResult BvnData()
        => new(Bvn, null, "Mr", "Male", "ADEBAYO", "MUSA", "OKORO", "14-03-1985", "08031234567", null, "Married",
            "Kaduna", "Zaria", "Kaduna", "Chikun", "12 Farm Road", "Bank of Agriculture", "Kaduna", "22-08-2014", "2", "NO");

    private static SmartComplyLoanFraudResult Fraud(int score)
        => new(1, "ADEBAYO OKORO", Bvn, true, false, score, "See score", "ok", null, null, Now);

    private void FindsClients(params FineractClientMatch[] clients)
        => _fineract.Setup(f => f.FindClientsAsync(It.Is<FineractClientSearch>(s => s.ExternalId == Bvn), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<FineractClientMatch>>(clients));

    private static FineractClientMatch Client(long id, params ClientSavingsAccountSummary[] accounts)
        => new(id, $"00000{id}", Bvn, "Amina Bello", "Active", true, Now.AddYears(-7), "08012345678", null,
            Now.AddYears(-35), "Female", "Individual", 1, "Kano Branch", 2, accounts);

    private static FineractLoanDetail LoanDetail(long id, string accountNo, int statusCode, decimal principal, decimal outstanding,
        IEnumerable<(DateTime Due, decimal Outstanding, bool Complete)> periods)
    {
        var schedule = periods.Select((p, i) => new FineractSchedulePeriod(
            i + 1, p.Due.AddMonths(-1), p.Due, 0, 0, 0, 0, 0, 0, 0, 0, p.Outstanding, 0, p.Outstanding, p.Complete)).ToList();
        return new FineractLoanDetail(id, accountNo, "Product", statusCode == 300 ? "Active" : "Closed", statusCode, principal, principal, 18m, 24,
            Now.AddMonths(-6), Now.AddMonths(18),
            new FineractLoanSummary(0, 0, outstanding, principal, principal - outstanding, outstanding, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            schedule);
    }

    private static PartyExposureResponse EmptyExposure()
        => new("req-1", Now, "test", MatchStatuses.NoMatch, [], new IdentityConsistency(true, []),
            new Dictionary<string, object>(), new ExposureSummary(0, 0, null, null, null, null, "NGN", true), [], false);
}
