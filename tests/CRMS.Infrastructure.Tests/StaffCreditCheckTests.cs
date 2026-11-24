using CRMS.Application.Access;
using CRMS.Application.Advisory.Interfaces;
using CRMS.Application.Billing;
using CRMS.Application.CreditCheck;
using CRMS.Application.CustomerCheck;
using CRMS.Domain.Aggregates.Access;
using CRMS.Domain.Aggregates.Billing;
using CRMS.Domain.Aggregates.CreditCheck;
using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Common;
using CRMS.Domain.Entities.Identity;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Services;
using CRMS.Infrastructure.BackgroundServices;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// Credit checks staff run from the intranet: partner checks run exactly as before, staff checks
/// are billed to "Other lookups" and never touch the partner store, the company check's bank and
/// registry sections, and who may queue or open a check.
/// </summary>
public class StaffCreditCheckTests
{
    // ── Partner checks are unchanged ─────────────────────────────────────────────────────────────

    [Fact]
    public void A_partner_runs_as_its_own_consumer_billed_to_the_partner_api()
    {
        var consumer = ApiConsumer.Create("AgriPay", ApiKeys.Hash("k"), "crms_abc",
            [CustomerCheckSections.CreditBureau, CustomerCheckSections.FraudCheck], 30, true, 14, null, Guid.NewGuid()).Value;

        var requester = CustomerCheckRequester.ForConsumer(consumer, "req-1");

        Assert.Equal("AgriPay", requester.Name);
        Assert.Equal(BillingFlow.PartnerApi, requester.BillingFlow);
        Assert.Equal("AgriPay · req-1", requester.BillingReference);
        Assert.Equal(CustomerCheckService.PartnerReference("AgriPay", "req-1"), requester.BillingReference);
        Assert.Null(requester.TriggeredBy);
        Assert.True(requester.StoreBureauReports);
        Assert.True(requester.ReuseBureauReports);
        Assert.Equal(14, requester.ReuseMaxAgeDays);
        Assert.Equal(consumer.Id, requester.FetchedById);
        Assert.True(requester.Allows(CustomerCheckSections.CreditBureau));
        Assert.False(requester.Allows(CustomerCheckSections.Identity));
    }

    [Fact]
    public async Task A_partner_check_still_stores_its_bureau_report_and_bills_the_partner_api()
    {
        var (service, smartComply, repository) = IndividualService();
        BillingScope? seen = null;
        smartComply.Setup(s => s.GetFirstCentralFullAsync(Bvn, It.IsAny<CancellationToken>()))
            .Callback(() => seen = BillingContext.Current)
            .ReturnsAsync(Result.Success(Report()));

        var consumer = ApiConsumer.Create("AgriPay", ApiKeys.Hash("k"), "crms_abc", CustomerCheckSections.All, 30, false, 30, null, Guid.NewGuid()).Value;
        var outcome = await service.CheckAsync(consumer, Query());

        Assert.Equal(SectionStatuses.Ok, outcome.Response.CreditBureau.Status);
        Assert.Equal("AgriPay", outcome.Response.Consumer);
        Assert.Equal(BillingFlow.PartnerApi, seen!.Flow);
        Assert.Equal("AgriPay · req-1", seen.CaseReference);
        repository.Verify(r => r.AddBureauReportAsync(It.IsAny<CustomerBureauReport>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_staff_check_bills_other_lookups_to_the_staff_member_and_stores_nothing_for_partners()
    {
        var (service, smartComply, repository) = IndividualService();
        BillingScope? seen = null;
        smartComply.Setup(s => s.GetFirstCentralFullAsync(Bvn, It.IsAny<CancellationToken>()))
            .Callback(() => seen = BillingContext.Current)
            .ReturnsAsync(Result.Success(Report()));

        var check = StaffCheck(StaffCreditCheck.Individual);
        var outcome = await service.CheckAsync(StaffCreditCheckRunner.RequesterFor(check, CustomerCheckSections.All), Query());

        Assert.Equal(SectionStatuses.Ok, outcome.Response.CreditBureau.Status);
        Assert.Equal(BillingFlow.Other, seen!.Flow);
        Assert.Equal(check.RequestedByUserId, seen.TriggeredBy);
        repository.Verify(r => r.AddBureauReportAsync(It.IsAny<CustomerBureauReport>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.FindBureauReportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Company check ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Without_an_account_number_the_bank_sections_say_why()
    {
        var (service, _, _, _) = CompanyService();

        var outcome = await service.CheckAsync(CompanyRequester(), CompanyQuery([CompanyCheckSections.BankAccounts, CompanyCheckSections.BankLoans]));

        Assert.Equal(SectionStatuses.NotPossible, outcome.Response.BankAccounts.Status);
        Assert.Equal(SectionStatuses.NotPossible, outcome.Response.BankLoans.Status);
        Assert.Contains("account number", outcome.Response.BankAccounts.Reason);
    }

    [Fact]
    public async Task An_account_registered_to_another_company_is_left_out_and_flagged()
    {
        var (service, _, coreBanking, fineract) = CompanyService();
        fineract.Setup(f => f.FindClientsAsync(It.Is<FineractClientSearch>(s => s.Nuban == Nuban), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<FineractClientMatch>>([Client(77)]));
        coreBanking.Setup(c => c.GetCorporateInfoAsync(Nuban, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CorporateInfo("77", "OTHER LTD", "RC999999", null, null, null, null)));

        var outcome = await service.CheckAsync(CompanyRequester(), CompanyQuery([CompanyCheckSections.BankAccounts], Nuban));

        Assert.Equal(SectionStatuses.NoRecord, outcome.Response.BankAccounts.Status);
        Assert.Contains("registered to a different company", outcome.Response.BankAccounts.Reason);
        Assert.Contains(outcome.Response.Warnings, w => w.Contains("different company"));
        fineract.Verify(f => f.GetClientAccountsAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_account_of_the_same_company_is_read_with_its_loans()
    {
        var (service, _, coreBanking, fineract) = CompanyService();
        fineract.Setup(f => f.FindClientsAsync(It.Is<FineractClientSearch>(s => s.Nuban == Nuban), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<FineractClientMatch>>([Client(77)]));
        coreBanking.Setup(c => c.GetCorporateInfoAsync(Nuban, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CorporateInfo("77", "ALLIEDSOFT LTD", "123456", null, null, null, null)));
        fineract.Setup(f => f.GetClientAccountsAsync(77, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ClientAccountSummary(77, [],
                [new ClientSavingsAccountSummary(1, "000123", "Current", "Active", 250_000m, Nuban, "Savings")])));

        var outcome = await service.CheckAsync(CompanyRequester(), CompanyQuery([CompanyCheckSections.BankAccounts, CompanyCheckSections.BankLoans], Nuban));

        Assert.Equal(SectionStatuses.Ok, outcome.Response.BankAccounts.Status);
        Assert.Single(outcome.Response.BankAccounts.Accounts);
        Assert.Equal(250_000m, outcome.Response.BankAccounts.Accounts[0].Balance);
        Assert.Equal(SectionStatuses.Ok, outcome.Response.BankLoans.Status);
    }

    [Fact]
    public async Task A_registry_with_no_such_company_reads_as_no_record()
    {
        var (service, smartComply, _, _) = CompanyService();
        smartComply.Setup(s => s.VerifyCacAdvancedAsync("RC123456", "Alliedsoft Ltd", "RC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SmartComplyCacResult>("Company not found"));

        var outcome = await service.CheckAsync(CompanyRequester(), CompanyQuery([CompanyCheckSections.CompanyRegistration]));

        Assert.Equal(SectionStatuses.NoRecord, outcome.Response.CompanyRegistration.Status);
        Assert.Contains(outcome.Response.Warnings, w => w.Contains("holds no company"));
    }

    [Fact]
    public async Task A_business_bureau_answer_is_mapped_and_bills_other_lookups()
    {
        var (service, smartComply, _, _) = CompanyService();
        BillingScope? seen = null;
        smartComply.Setup(s => s.GetCRCBusinessHistoryAsync("RC123456", It.IsAny<CancellationToken>()))
            .Callback(() => seen = BillingContext.Current)
            .ReturnsAsync(Result.Success(BusinessReport()));

        var outcome = await service.CheckAsync(CompanyRequester(), CompanyQuery([CompanyCheckSections.CreditBureau]));

        Assert.Equal(SectionStatuses.Ok, outcome.Response.CreditBureau.Status);
        Assert.Equal("CRC", outcome.Response.CreditBureau.Bureau);
        Assert.Equal(3, outcome.Response.CreditBureau.Summary!.TotalLoans);
        Assert.Equal(BillingFlow.Other, seen!.Flow);
        Assert.Equal("Company", seen.SubjectType);
    }

    // ── Queue and open ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_user_without_access_cannot_queue_a_check()
    {
        var h = new Handlers(allowRun: false);

        var result = await h.Queue().Handle(new QueueStaffCreditCheckCommand(h.User.Id, IndividualRequest()));

        Assert.False(result.IsSuccess);
        Assert.Contains("Feature Access", result.Error);
        h.Checks.Verify(c => c.AddAsync(It.IsAny<StaffCreditCheck>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_reason_is_required()
    {
        var h = new Handlers();

        var result = await h.Queue().Handle(new QueueStaffCreditCheckCommand(h.User.Id, IndividualRequest() with { Purpose = "hi" }));

        Assert.False(result.IsSuccess);
        Assert.Contains("why", result.Error);
    }

    [Fact]
    public async Task Only_one_check_at_a_time_per_user()
    {
        var h = new Handlers();
        h.Checks.Setup(c => c.CountOpenAsync(h.User.Id, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await h.Queue().Handle(new QueueStaffCreditCheckCommand(h.User.Id, IndividualRequest()));

        Assert.False(result.IsSuccess);
        Assert.Contains("already have a check running", result.Error);
    }

    [Fact]
    public async Task A_queued_check_stores_the_request_encrypted_and_a_masked_label()
    {
        var h = new Handlers();
        StaffCreditCheck? added = null;
        h.Checks.Setup(c => c.AddAsync(It.IsAny<StaffCreditCheck>(), It.IsAny<CancellationToken>()))
            .Callback((StaffCreditCheck c, CancellationToken _) => added = c);

        var result = await h.Queue().Handle(new QueueStaffCreditCheckCommand(h.User.Id, IndividualRequest()));

        Assert.True(result.IsSuccess);
        Assert.Equal(StaffCreditCheck.Queued, added!.Status);
        Assert.Equal("BVN ****5678", added.SubjectLabel);
        Assert.StartsWith("enc:", added.RequestCiphertext);
        Assert.DoesNotContain(Bvn, added.SubjectLabel + added.SubjectHash);
        Assert.Equal("Walk-in enquiry for an input loan", added.Purpose);
    }

    [Fact]
    public async Task A_company_check_is_queued_with_the_registration_number()
    {
        var h = new Handlers();
        StaffCreditCheck? added = null;
        h.Checks.Setup(c => c.AddAsync(It.IsAny<StaffCreditCheck>(), It.IsAny<CancellationToken>()))
            .Callback((StaffCreditCheck c, CancellationToken _) => added = c);

        var result = await h.Queue().Handle(new QueueStaffCreditCheckCommand(h.User.Id,
            new StaffCreditCheckRequest(StaffCreditCheck.Company, null, "rc 123456", "Alliedsoft Ltd", null, null, null, null, null, null, false,
                "Corporate loan pre-screen", null)));

        Assert.True(result.IsSuccess);
        Assert.Equal(StaffCreditCheck.Company, added!.SubjectType);
        Assert.Equal("RC123456 · Alliedsoft Ltd", added.SubjectLabel);
        Assert.Equal(CompanyCheckSections.All, added.SectionList);
    }

    [Fact]
    public async Task Someone_elses_check_is_hidden_without_view_all()
    {
        var h = new Handlers();
        var other = StaffCheck(StaffCreditCheck.Individual);
        h.Checks.Setup(c => c.GetByIdAsync(other.Id, It.IsAny<CancellationToken>())).ReturnsAsync(other);

        var result = await h.Get().Handle(new GetStaffCreditCheckQuery(other.Id, h.User.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal("Check not found.", result.Error);
    }

    [Fact]
    public async Task Someone_elses_check_opens_with_view_all()
    {
        var h = new Handlers(allowViewAll: true);
        var other = StaffCheck(StaffCreditCheck.Individual);
        h.Checks.Setup(c => c.GetByIdAsync(other.Id, It.IsAny<CancellationToken>())).ReturnsAsync(other);

        var result = await h.Get().Handle(new GetStaffCreditCheckQuery(other.Id, h.User.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(other.Id, result.Data!.Summary.Id);
    }

    [Fact]
    public async Task Feature_access_reads_the_users_roles_from_the_database()
    {
        var user = ApplicationUser.Create("a@b.ng", "a", "Ada", "Obi", UserType.Staff).Value;
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var roles = new Mock<IRoleRepository>();
        roles.Setup(r => r.GetUserRolesAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ApplicationRole.Create("CreditOfficer", null, RoleType.System)]);
        var rules = new Mock<IFeatureAccessRuleRepository>();
        rules.Setup(r => r.GetByKeyAsync(FeatureKeys.CreditCheckRun, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FeatureAccessRule.Create(FeatureKeys.CreditCheckRun, ["CreditOfficer"]));
        rules.Setup(r => r.GetByKeyAsync(FeatureKeys.CreditCheckViewAll, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FeatureAccessRule.Create(FeatureKeys.CreditCheckViewAll));
        var access = new FeatureAccessService(users.Object, roles.Object, rules.Object);

        Assert.True(await access.IsAllowedAsync(user.Id, FeatureKeys.CreditCheckRun));
        Assert.False(await access.IsAllowedAsync(user.Id, FeatureKeys.CreditCheckViewAll));
        Assert.False(await access.IsAllowedAsync(Guid.NewGuid(), FeatureKeys.CreditCheckRun));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    private const string Bvn = "22212345678";
    private const string Nuban = "0123456789";

    private static CustomerCheckQuery Query()
        => new("req-1", Bvn, [], [CustomerCheckSections.CreditBureau], 12, false, false, null, null);

    private static CompanyCheckQuery CompanyQuery(IReadOnlyList<string> sections, params string[] nubans)
        => new("staff-1", "RC123456", "RC", "Alliedsoft Ltd", nubans, sections, 12, false, null, null, null, null);

    private static StaffCreditCheck StaffCheck(string type) => StaffCreditCheck.Create(
        type, "hash", "BVN ****5678", Guid.NewGuid(), "Ada Obi", "Walk-in enquiry", null, [CustomerCheckSections.CreditBureau], "enc:{}").Value;

    private static CustomerCheckRequester CompanyRequester()
        => StaffCreditCheckRunner.RequesterFor(StaffCheck(StaffCreditCheck.Company), CompanyCheckSections.All);

    private static StaffCreditCheckRequest IndividualRequest() => new(
        StaffCreditCheck.Individual, Bvn, null, null, null, null, null, null, null, null, false, "Walk-in enquiry for an input loan", null);

    private static (CustomerCheckService, Mock<ISmartComplyProvider>, Mock<ICustomerCheckRepository>) IndividualService()
    {
        var smartComply = new Mock<ISmartComplyProvider>(MockBehavior.Strict);
        var repository = new Mock<ICustomerCheckRepository>();
        repository.Setup(r => r.FindBureauReportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerBureauReport?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        var service = new CustomerCheckService(
            smartComply.Object, new Mock<ICoreBankingService>().Object, new Mock<IFineractDirectService>().Object,
            new Mock<Application.PartyExposure.IPartyExposureService>().Object, new Mock<IAIAdvisoryService>().Object,
            repository.Object, unitOfWork.Object, Options.Create(new CustomerCheckSettings()),
            Options.Create(new ExternalServices.Rhshf.RhshfSettings { CallbackSigningSecret = "secret" }),
            NullLogger<CustomerCheckService>.Instance);
        return (service, smartComply, repository);
    }

    private static (CompanyCheckService, Mock<ISmartComplyProvider>, Mock<ICoreBankingService>, Mock<IFineractDirectService>) CompanyService()
    {
        var smartComply = new Mock<ISmartComplyProvider>();
        var coreBanking = new Mock<ICoreBankingService>();
        var fineract = new Mock<IFineractDirectService>();
        var db = new CRMSDbContext(new DbContextOptionsBuilder<CRMSDbContext>().UseInMemoryDatabase("Company_" + Guid.NewGuid()).Options);
        var service = new CompanyCheckService(
            smartComply.Object, coreBanking.Object, fineract.Object, new Mock<IAIAdvisoryService>().Object, db,
            Options.Create(new CustomerCheckSettings()), NullLogger<CompanyCheckService>.Instance);
        return (service, smartComply, coreBanking, fineract);
    }

    private static FineractClientMatch Client(long id) => new(
        id, "000077", "RC123456", "ALLIEDSOFT LTD", "Active", true, new DateTime(2020, 1, 1), null, null, null, null, "BUSINESS", 1, "HQ", 2, []);

    private static SmartComplyIndividualCreditReport Report()
        => new("id", Bvn, "ADEBAYO MUSA OKORO", "08031234567", "Male", "1985-03-14", "Kaduna", "a@b.c",
            new SmartComplyCreditSummary(5, 3, 2, 3, 1, 1, 2_500_000m, 100_000m, 6_100_000m, 1_840_000m, 215_000m, 94, "ORD-1"),
            [], [], [], [], DateTime.UtcNow);

    private static SmartComplyBusinessCreditReport BusinessReport() => new(
        "b1", "RC123456", "ALLIEDSOFT LTD", null, "2015-03-01", "Kano", null, null, 2, "Agriculture", "LTD", null,
        new SmartComplyBusinessCreditSummary(3, 1, 2, 2, 0m, 9_000_000m, 5_000_000m, 1_200_000m, 100_000m, 0, 1, 0, null, null),
        DateTime.UtcNow);

    /// <summary>The queue and open handlers over mocks, with a signed-in staff member.</summary>
    private sealed class Handlers
    {
        public readonly ApplicationUser User = ApplicationUser.Create("ada@boa.ng", "ada", "Ada", "Obi", UserType.Staff).Value;
        public readonly Mock<IStaffCreditCheckRepository> Checks = new();
        private readonly Mock<IFeatureAccessService> _access = new();
        private readonly Mock<IUserRepository> _users = new();
        private readonly Mock<ICustomerCheckService> _customerCheck = new();
        private readonly Mock<ICustomerCheckCipher> _cipher = new();
        private readonly AuditService _audit = new(new Mock<IAuditLogRepository>().Object, new Mock<IDataAccessLogRepository>().Object, new Mock<IUnitOfWork>().Object);

        public Handlers(bool allowRun = true, bool allowViewAll = false)
        {
            _access.Setup(a => a.IsAllowedAsync(User.Id, FeatureKeys.CreditCheckRun, It.IsAny<CancellationToken>())).ReturnsAsync(allowRun);
            _access.Setup(a => a.IsAllowedAsync(User.Id, FeatureKeys.CreditCheckViewAll, It.IsAny<CancellationToken>())).ReturnsAsync(allowViewAll);
            _users.Setup(u => u.GetByIdAsync(User.Id, It.IsAny<CancellationToken>())).ReturnsAsync(User);
            _customerCheck.Setup(c => c.HashIdentifier(It.IsAny<string>())).Returns((string v) => $"h{v.Length}");
            _cipher.Setup(c => c.Protect(It.IsAny<string>())).Returns((string v) => "enc:" + v.Length);
            _cipher.Setup(c => c.Unprotect(It.IsAny<string>())).Returns("{}");
        }

        public QueueStaffCreditCheckHandler Queue() => new(
            _access.Object, Checks.Object, _users.Object, _customerCheck.Object, _cipher.Object,
            new Mock<IUnitOfWork>().Object, _audit, new CustomerCheckSettings());

        public GetStaffCreditCheckHandler Get() => new(_access.Object, Checks.Object, _users.Object, _cipher.Object, _audit);
    }
}

/// <summary>The branded report renders whatever the check found, a full result or one where most
/// sections did not answer.</summary>
public class CreditCheckPdfGeneratorTests
{
    private static StaffCreditCheckSummary Summary(string type) => new(
        Guid.NewGuid(), type, type == StaffCreditCheck.Company ? "RC123456 · Alliedsoft Ltd" : "BVN ****5678", "Ada Obi Farms",
        StaffCreditCheck.Completed, "Ada Obi", Guid.NewGuid(), "Walk-in enquiry", "RHSHF-2026-000001",
        DateTime.UtcNow, DateTime.UtcNow, 64m, "Low", "ApproveWithConditions", 1);

    private static AiAdvisorySection Advisory() => new(
        SectionStatuses.Ok, null, 64m, "Low", "ApproveWithConditions",
        [new AdvisoryCategory("CreditHistory", true, 70m, 0.4m, "Low", "Bureau report", ["Repaid"], ["One delinquent"]),
         new AdvisoryCategory("RepaymentCapacity", false, null, 0.25m, null, "No statement was read.", [], [])],
        ["One delinquent bureau facility."], ["Clear overdue facilities."],
        new AdvisoryNarrative("Summary.", "Strengths.", "Weaknesses.", null, null),
        new AdvisoryProposal(500_000m, 12, true, 375_000m, 12), "Scored from a CRC report.", "TEMPLATE", "v1", DateTime.UtcNow);

    private static CreditBureauSection Bureau() => new(
        SectionStatuses.Ok, null, "CRC", [], false, DateTime.UtcNow, new BureauScore(612, "Fair", "CRC"),
        new BureauSummary(3, 2, 1, 2, 1, 1, 2_000_000m, 3_000_000m, 1_000_000m, 50_000m, 40, "NGN"),
        [new BureauLoan("FairMoney", "2210", "Personal", 300_000m, 215_000m, 50_000m, CheckLoanStatuses.Overdue, "Delinquent", null, null, null, "3 months", "Monthly")],
        [new BureauEnquiry("Carbon", "Loan", null)]);

    [Fact]
    public void A_full_individual_check_renders()
    {
        var r = new CustomerCheckResponse("r", DateTime.UtcNow, "staff", new CheckSubject("5678", []), [],
            new IdentitySection(SectionStatuses.Ok, null, "SmartComply", "ADA OBI", null, "Female", "0803****123", "Kano", "BOA", null, false),
            Bureau(), new FraudCheckSection(SectionStatuses.Ok, null, 38, "MEDIUM", "Proceed", false, null),
            new BankAccountsSection(SectionStatuses.Ok, null, "MATCH", "1", "Active", "2", null,
                [new BankAccount("5678", "Savings", "SAVINGS", "ACTIVE", "Active", null, 100m, "NGN", null, new AccountActivity(12, null, null, 4, 10m, 5m, 20))]),
            new BankLoansSection(SectionStatuses.Ok, null, [new BankLoan("9981", "RH-SHF", 1m, 1m, 0m, 0, "RUNNING", "Active", null, null, null, null)]),
            new CrmsFacilitiesSection(SectionStatuses.Ok, null, "MATCH", new CrmsFacilitySummary(1, 0, null, false),
                [new CrmsFacility("BORROWER", "RHSHF", "RHSHF-1", "Inputs", "Ada Farms", "ACTIVE", 1m, null, null, null, "NOT_REQUESTED")]),
            Advisory(), new Dictionary<string, string>(), ["One bureau loan is 40 days overdue."]);
        var view = new StaffCreditCheckView(Summary(StaffCreditCheck.Individual), CustomerCheckSections.All,
            new Dictionary<string, string>(), DateTime.UtcNow, 41_000, 4, "CRC", null, r, null);

        var pdf = new CRMS.Infrastructure.Documents.CreditCheckPdfGenerator().Generate(view, DateTime.UtcNow, "Ada Obi");

        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void A_company_check_where_most_sections_did_not_answer_renders()
    {
        var r = new CompanyCheckResponse("r", DateTime.UtcNow, "staff", new CompanySubject("RC123456", "Alliedsoft Ltd", []), [],
            new CompanyRegistrationSection(SectionStatuses.Ok, null, "SmartComply CAC", "ALLIEDSOFT LTD", "123456", "RC", "ACTIVE", null,
                "Kano", "Kano", null, "Agriculture", 1_000_000m, true,
                [new CompanyAffiliate("Ade Musa", "DIRECTOR", "ACTIVE", true, false, null, "Nigerian", "Farmer", 10_000, "ORDINARY")]),
            CreditBureauSection.Empty(SectionStatuses.NoRecord, "No bureau holds a record."),
            FraudCheckSection.Empty(SectionStatuses.Unavailable, "Timed out."),
            BankAccountsSection.Empty(SectionStatuses.NotPossible, "No account number was given."),
            BankLoansSection.Empty(SectionStatuses.NotPossible, "No account number was given."),
            CrmsFacilitiesSection.Empty(SectionStatuses.NoRecord, "No CRMS case."),
            AiAdvisorySection.Empty(SectionStatuses.NotRequested), new Dictionary<string, string>(), []);
        var view = new StaffCreditCheckView(Summary(StaffCreditCheck.Company), CompanyCheckSections.All,
            new Dictionary<string, string>(), DateTime.UtcNow, 12_000, 2, null, null, null, r);

        var pdf = new CRMS.Infrastructure.Documents.CreditCheckPdfGenerator().Generate(view, DateTime.UtcNow, "Ada Obi");

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
