using System.Text.Json;
using CRMS.Application.CustomerCheck;
using CRMS.Application.CustomerCheck.Commands;
using CRMS.Application.CustomerCheck.Reporting;
using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.BackgroundServices;
using CRMS.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// Company checks on the partner API: switched off until the bank turns them on, run through the
/// company check by the same worker, priced on their own items, called back with their own event,
/// and never mixed into the individual endpoints. Every individual behaviour stays as it was.
/// </summary>
public class PartnerCompanyCheckTests
{
    private static readonly CustomerCheckCipher Cipher = new(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static ApiConsumer Consumer(bool company, IEnumerable<string>? sections = null)
    {
        var c = ApiConsumer.Create("AgriPay", ApiKeys.Hash("k"), "crms_abc",
            sections ?? CustomerCheckSections.All.Append(CompanyCheckSections.CompanyRegistration), 30, false, 30, null, Guid.NewGuid()).Value;
        c.SetCompanyChecks(company, Guid.NewGuid());
        c.SetCallbackSecret(Cipher.Protect("whsec-test"), "crms_whsec_abc", Guid.NewGuid());
        return c;
    }

    private static CompanyCheckQuery Query() =>
        new("req-co-1", "RC123456", "RC", "Alliedsoft Ltd", ["0123456789"], CompanyCheckSections.All, 12, false, null, null, null, null,
            "https://partner.example.com/hook");

    private static CustomerCheckJob CompanyJob(ApiConsumer consumer)
    {
        var job = CustomerCheckJob.Create(consumer.Id, consumer.Name, "req-co-1", "rc-hash", "3456", CompanyCheckSections.All,
            Cipher.Protect(JsonSerializer.Serialize(Query(), Json)), "https://partner.example.com/hook", CustomerCheckJob.Company);
        job.Start(DateTime.UtcNow);
        return job;
    }

    private static CompanyCheckResponse Response() => new(
        "req-co-1", DateTime.UtcNow, "AgriPay", new CompanySubject("RC123456", "Alliedsoft Ltd", ["****6789"]), CompanyCheckSections.All,
        CompanyRegistrationSection.Empty(SectionStatuses.Ok), CreditBureauSection.Empty(SectionStatuses.NoRecord),
        FraudCheckSection.Empty(SectionStatuses.Ok), BankAccountsSection.Empty(SectionStatuses.Ok), BankLoansSection.Empty(SectionStatuses.Ok),
        CrmsFacilitiesSection.Empty(SectionStatuses.Ok), AiAdvisorySection.Empty(SectionStatuses.NotRequested),
        new Dictionary<string, string>(), []);

    // ── Entitlements ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_new_consumer_has_company_checks_off()
    {
        var c = ApiConsumer.Create("New", ApiKeys.Hash("k"), "crms_x", CustomerCheckSections.All, 30, false, 30, null, Guid.NewGuid()).Value;

        Assert.False(c.AllowCompanyChecks);
        Assert.Empty(CustomerCheckRequester.ForConsumerCompany(c, "r").AllowedSections);
    }

    [Fact]
    public void A_partner_with_company_checks_on_runs_its_ticked_company_sections()
    {
        var c = Consumer(company: true, [CustomerCheckSections.CreditBureau, CompanyCheckSections.CompanyRegistration, CustomerCheckSections.Identity]);

        var company = CustomerCheckRequester.ForConsumerCompany(c, "r");
        var individual = CustomerCheckRequester.ForConsumer(c, "r");

        Assert.Equal([CompanyCheckSections.CompanyRegistration, CompanyCheckSections.CreditBureau], company.AllowedSections.OrderBy(s => s));
        Assert.DoesNotContain(CompanyCheckSections.CompanyRegistration, individual.AllowedSections);
        Assert.Equal(individual.BillingFlow, company.BillingFlow);
        Assert.Equal(individual.BillingReference, company.BillingReference);
    }

    [Fact]
    public void The_cac_section_is_a_known_section_for_a_consumer()
    {
        var (sections, error) = ApiConsumerMapping.Sections(["identity", "COMPANYREGISTRATION"]);

        Assert.Null(error);
        Assert.Equal([CustomerCheckSections.Identity, CompanyCheckSections.CompanyRegistration], sections);
    }

    // ── Pricing ──────────────────────────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, decimal> Prices = new()
    {
        [CustomerCheckPriceItems.Check] = 100m,
        [CustomerCheckPriceItems.Section("creditBureau")] = 10m,
        [CustomerCheckPriceItems.CallBureau] = 50m,
        [CustomerCheckPriceItems.CompanyCheck] = 1000m,
        [CustomerCheckPriceItems.CompanySection("creditBureau")] = 70m,
        [CustomerCheckPriceItems.CompanySection("companyRegistration")] = 30m,
        [CustomerCheckPriceItems.CompanyCallBureau] = 500m,
        [CustomerCheckPriceItems.CompanyCallRegistry] = 200m,
    };

    [Fact]
    public void An_individual_check_is_priced_as_it_always_was()
    {
        var amount = CustomerCheckBilling.Amount(Prices, CustomerCheckJob.Completed, "creditBureau=OK,identity=NOT_REQUESTED",
            new BilledCallCounts(0, 1, 0, 0, 0));

        Assert.Equal(160m, amount);
    }

    [Fact]
    public void A_company_check_is_priced_on_the_company_items_only()
    {
        var amount = CustomerCheckBilling.Amount(Prices, CustomerCheckJob.Completed, "companyRegistration=OK,creditBureau=NO_RECORD",
            new BilledCallCounts(1, 1, 0, 0, 0), company: true);

        Assert.Equal(1000m + 30m + 70m + 200m + 500m, amount);
    }

    [Fact]
    public void Company_items_are_on_the_price_list_after_every_individual_item()
    {
        var items = CustomerCheckPriceItems.All.Select(i => i.Item).ToList();

        Assert.Equal(CustomerCheckPriceItems.Check, items[0]);
        Assert.True(items.IndexOf(CustomerCheckPriceItems.CallAdvisory) < items.IndexOf(CustomerCheckPriceItems.CompanyCheck));
        Assert.Contains(CustomerCheckPriceItems.CompanySection("companyRegistration"), items);
    }

    // ── History, paths and events ────────────────────────────────────────────────────────────────

    [Fact]
    public void Each_history_lists_only_its_own_kind_of_check()
    {
        var settings = new CustomerCheckSettings();
        var (individual, _) = CustomerCheckRules.ValidateHistory(new CustomerCheckHistoryRequest(null, null, null, null, null, null, null), Guid.NewGuid(), v => "h:" + v, settings);
        var (company, errors) = CompanyCheckRules.ValidateHistory(new CompanyCheckHistoryRequest(null, null, null, null, "rc 123456", null, null), Guid.NewGuid(), v => "h:" + v, settings);

        Assert.Equal(CustomerCheckJob.Individual, individual!.SubjectType);
        Assert.Empty(errors);
        Assert.Equal(CustomerCheckJob.Company, company!.SubjectType);
        Assert.Equal("h:RC:RC123456", company.BvnHash);
    }

    [Fact]
    public void A_company_job_is_read_from_the_company_endpoint_and_called_back_as_a_company_event()
    {
        var consumer = Consumer(company: true);
        var company = CompanyJob(consumer);
        var individual = CustomerCheckJob.Create(consumer.Id, consumer.Name, "r", "h", "5678", CustomerCheckSections.All, "v1.x", null);

        Assert.StartsWith("/api/v1/company-credit-check/", CustomerCheckJobIds.StatusPath(company));
        Assert.StartsWith("/api/v1/customer-credit-check/", CustomerCheckJobIds.StatusPath(individual));
        Assert.Equal(CustomerCheckEvents.CompanyCompleted, CustomerCheckEvents.For(company, true));
        Assert.Equal(CustomerCheckEvents.Failed, CustomerCheckEvents.For(individual, false));
        Assert.Equal(CustomerCheckJob.Individual, individual.SubjectType);
    }

    // ── Runner ───────────────────────────────────────────────────────────────────────────────────

    private sealed class Harness
    {
        public readonly Mock<ICustomerCheckJobRepository> Jobs = new();
        public readonly Mock<IApiConsumerRepository> Consumers = new();
        public readonly Mock<ICustomerCheckService> Service = new();
        public readonly Mock<ICompanyCheckService> Company = new();
        public CustomerCheckLog? Recorded;

        public Harness(ApiConsumer consumer, CustomerCheckJob job)
        {
            Jobs.Setup(j => j.GetByIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
            Consumers.Setup(c => c.GetByIdAsync(consumer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(consumer);
            Service.Setup(s => s.HashIdentifier(It.IsAny<string>())).Returns<string>(v => "h:" + v);
            Service.Setup(s => s.RecordAsync(It.IsAny<CustomerCheckLog>(), It.IsAny<CancellationToken>()))
                .Callback<CustomerCheckLog, CancellationToken>((l, _) => Recorded = l)
                .Returns(Task.CompletedTask);
        }

        public CustomerCheckJobRunner Runner() => new(
            Jobs.Object, Consumers.Object, Service.Object, Cipher,
            Options.Create(new CustomerCheckSettings { JobTimeoutMs = 2000 }), NullLogger<CustomerCheckJobRunner>.Instance, Company.Object);
    }

    [Fact]
    public async Task A_company_job_runs_through_the_company_check_and_is_logged_as_a_company()
    {
        var consumer = Consumer(company: true);
        var job = CompanyJob(consumer);
        var h = new Harness(consumer, job);
        h.Company.Setup(s => s.CheckAsync(It.Is<CustomerCheckRequester>(r => r.Allows(CompanyCheckSections.CompanyRegistration)),
                It.Is<CompanyCheckQuery>(q => q.RcNumber == "RC123456"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyCheckOutcome(Response(), "CRC", new BilledCallCounts(1, 1, 0, 1, 0)));

        await h.Runner().RunAsync(job.Id, CancellationToken.None);

        Assert.Equal(CustomerCheckJob.Completed, job.Status);
        Assert.Contains("companyRegistration=OK", job.SectionOutcomes);
        var stored = JsonSerializer.Deserialize<JsonElement>(Cipher.Unprotect(job.ResultCiphertext!), Json);
        Assert.Equal("RC123456", stored.GetProperty("subject").GetProperty("rcNumber").GetString());
        Assert.Equal(CustomerCheckJob.Company, h.Recorded!.SubjectType);
        Assert.Equal(1, h.Recorded.IdentityCalls);
        h.Service.Verify(s => s.CheckAsync(It.IsAny<ApiConsumer>(), It.IsAny<CustomerCheckQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_company_job_fails_if_company_checks_were_switched_off_before_it_ran()
    {
        var consumer = Consumer(company: false);
        var job = CompanyJob(consumer);
        var h = new Harness(consumer, job);

        await h.Runner().RunAsync(job.Id, CancellationToken.None);

        Assert.Equal(CustomerCheckJob.Failed, job.Status);
        Assert.Equal(CustomerCheckErrors.CompanyChecksNotEnabled, job.ErrorCode);
        Assert.Equal(CustomerCheckJob.Company, h.Recorded!.SubjectType);
        h.Company.Verify(s => s.CheckAsync(It.IsAny<CustomerCheckRequester>(), It.IsAny<CompanyCheckQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void A_company_callback_carries_the_company_event_and_path()
    {
        var consumer = Consumer(company: true);
        var job = CompanyJob(consumer);
        job.Complete(Cipher.Protect(JsonSerializer.Serialize(Response(), Json)), [new("companyRegistration", "OK")], "CRC", false, 2, 100, DateTime.UtcNow);
        var dispatcher = new CustomerCheckCallbackDispatcher(
            new Mock<ICustomerCheckJobRepository>().Object, new Mock<IApiConsumerRepository>().Object, new Mock<ICustomerCheckCallbackSender>().Object,
            Cipher, new Mock<IUnitOfWork>().Object, NullLogger<CustomerCheckCallbackDispatcher>.Instance);

        var envelope = dispatcher.BuildEnvelope(job);

        Assert.Equal(CustomerCheckEvents.CompanyCompleted, envelope.EventType);
        Assert.StartsWith("/api/v1/company-credit-check/", envelope.StatusUrl);
        Assert.Equal("RC123456", envelope.Result!.Value.GetProperty("subject").GetProperty("rcNumber").GetString());
    }
}
