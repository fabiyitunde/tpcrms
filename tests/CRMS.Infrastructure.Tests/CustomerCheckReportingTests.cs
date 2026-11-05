using CRMS.Application.CustomerCheck;
using CRMS.Application.CustomerCheck.Reporting;
using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>The partner-API dashboard and statements: totals, pricing, callback health and the
/// per-consumer rows, from jobs and audit rows seeded into an in-memory database.</summary>
public class CustomerCheckReportingTests
{
    private static readonly CustomerCheckCipher Cipher = new(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid AgriPay = Guid.NewGuid();
    private static readonly Guid FarmCo = Guid.NewGuid();

    private static CRMSDbContext Db() => new(new DbContextOptionsBuilder<CRMSDbContext>()
        .UseInMemoryDatabase("cc-reporting-" + Guid.NewGuid()).Options);

    private static CustomerCheckJob Job(Guid consumerId, string consumer, string requestId, DateTime queuedAt, string? callbackUrl = "https://p.example.com/h")
    {
        var job = CustomerCheckJob.Create(consumerId, consumer, requestId, "h-" + requestId, "1234", CustomerCheckSections.All, Cipher.Protect("{}"), callbackUrl);
        typeof(CustomerCheckJob).GetProperty("CreatedAt")!.SetValue(job, queuedAt);
        return job;
    }

    private static CustomerCheckJob Completed(Guid consumerId, string consumer, string requestId, DateTime queuedAt, int durationMs, BilledCallCounts calls,
        string outcomes = "identity=OK,creditBureau=OK,fraudCheck=OK,bankAccounts=NO_RECORD,bankLoans=NO_RECORD,crmsFacilities=OK,aiAdvisory=OK",
        decimal? score = 70m, string? rating = "Low", string? recommendation = "Approve", bool deliveredFirstTry = true, string? callbackUrl = "https://p.example.com/h")
    {
        var job = Job(consumerId, consumer, requestId, queuedAt, callbackUrl);
        job.Start(queuedAt.AddSeconds(1));
        job.Complete(Cipher.Protect("{\"requestId\":\"" + requestId + "\"}"), CustomerCheckBilling.ParseOutcomes(outcomes).Select(o => new KeyValuePair<string, string>(o.Section, o.Outcome)),
            "FirstCentral", false, calls.Total, durationMs, queuedAt.AddMilliseconds(durationMs), score, rating, recommendation);
        if (callbackUrl is not null)
        {
            if (deliveredFirstTry) job.RecordCallbackAttempt(true, 200, null, null, queuedAt.AddSeconds(5));
            else { job.RecordCallbackAttempt(false, 503, "HTTP 503", queuedAt.AddMinutes(1), queuedAt.AddSeconds(5)); job.RecordCallbackAttempt(true, 200, null, null, queuedAt.AddMinutes(1)); }
        }
        return job;
    }

    private static CustomerCheckLog Log(CustomerCheckJob job, BilledCallCounts calls) => CustomerCheckLog.Record(
        job.RequestId, job.ConsumerId, job.ConsumerName, job.BvnHash, [], job.SectionList, [], job.BureauUsed, job.BureauReused, calls, job.DurationMs, job.ErrorCode, job.Id);

    private static async Task<CRMSDbContext> SeededAsync()
    {
        var db = Db();
        var calls = new BilledCallCounts(1, 1, 1, 1, 1);
        var a1 = Completed(AgriPay, "AgriPay", "a-1", Now.AddDays(-1), 30_000, calls);
        var a2 = Completed(AgriPay, "AgriPay", "a-2", Now.AddDays(-2), 50_000, calls, score: 40m, rating: "High", recommendation: "Decline", deliveredFirstTry: false);
        var f1 = Completed(FarmCo, "FarmCo", "f-1", Now.AddDays(-1).AddHours(2), 20_000, new BilledCallCounts(0, 1, 0, 0, 0),
            outcomes: "identity=NOT_REQUESTED,creditBureau=OK,fraudCheck=NOT_REQUESTED,bankAccounts=UNAVAILABLE,bankLoans=UNAVAILABLE,crmsFacilities=OK,aiAdvisory=OK", callbackUrl: null);
        var failed = Job(FarmCo, "FarmCo", "f-2", Now.AddDays(-3));
        failed.Start(Now.AddDays(-3)); failed.Fail(CustomerCheckErrors.TimedOut, "Too slow.", 300_000, Now.AddDays(-3).AddMinutes(5));
        failed.RecordCallbackAttempt(false, null, "x", null, Now.AddDays(-3).AddMinutes(5));
        var queued = Job(AgriPay, "AgriPay", "a-3", Now.AddMinutes(-1));
        var old = Completed(AgriPay, "AgriPay", "a-old", Now.AddDays(-10), 10_000, calls);

        db.CustomerCheckJobs.AddRange(a1, a2, f1, failed, queued, old);
        db.CustomerCheckLogs.AddRange(Log(a1, calls), Log(a2, calls), Log(f1, new BilledCallCounts(0, 1, 0, 0, 0)), Log(old, calls));
        db.CustomerCheckPrices.AddRange(
            CustomerCheckPrice.Create(CustomerCheckPriceItems.Check, "Per check", 100m, "NGN", Guid.Empty).Value,
            CustomerCheckPrice.Create(CustomerCheckPriceItems.Section(CustomerCheckSections.CreditBureau), "Bureau section", 50m, "NGN", Guid.Empty).Value,
            CustomerCheckPrice.Create(CustomerCheckPriceItems.CallBureau, "Bureau call", 1000m, "NGN", Guid.Empty).Value,
            CustomerCheckPrice.Create(CustomerCheckPriceItems.CallAdvisory, "Advisory call", 25m, "NGN", Guid.Empty).Value);
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public void Billing_charges_the_check_the_answered_sections_and_each_call()
    {
        var prices = new Dictionary<string, decimal>
        {
            [CustomerCheckPriceItems.Check] = 100m,
            [CustomerCheckPriceItems.Section("creditBureau")] = 50m,
            [CustomerCheckPriceItems.Section("bankAccounts")] = 40m,
            [CustomerCheckPriceItems.CallBureau] = 1000m,
            [CustomerCheckPriceItems.CallScore] = 300m,
        };
        var calls = new BilledCallCounts(1, 1, 1, 0, 0);

        // Bureau answered (charged), accounts unavailable (not charged), identity not priced.
        var amount = CustomerCheckBilling.Amount(prices, CustomerCheckJob.Completed, "identity=OK,creditBureau=OK,bankAccounts=UNAVAILABLE", calls);
        Assert.Equal(100m + 50m + 1000m + 300m, amount);

        Assert.Equal(0m, CustomerCheckBilling.Amount(prices, CustomerCheckJob.Failed, "creditBureau=OK", calls));
        Assert.Equal(100m + 40m, CustomerCheckBilling.Amount(prices, CustomerCheckJob.Completed, "bankAccounts=NO_RECORD", BilledCallCounts.None));
        Assert.Equal([(CustomerCheckPriceItems.Check, 1), (CustomerCheckPriceItems.Section("creditBureau"), 1), (CustomerCheckPriceItems.CallIdentity, 1), (CustomerCheckPriceItems.CallBureau, 1), (CustomerCheckPriceItems.CallScore, 1)],
            CustomerCheckBilling.Quantities(CustomerCheckJob.Completed, "creditBureau=OK,bankAccounts=UNAVAILABLE", calls));
    }

    [Fact]
    public async Task Overview_totals_reconcile_with_the_seeded_jobs_and_prices()
    {
        await using var db = await SeededAsync();
        var reporting = new CustomerCheckReportingService(db, Cipher);

        var o = await reporting.GetOverviewAsync(Now.AddDays(-7), Now.AddDays(1), null);

        Assert.Equal(5, o.Totals.Submitted);
        Assert.Equal(3, o.Totals.Completed);
        Assert.Equal(1, o.Totals.Failed);
        Assert.Equal(1, o.Totals.Open);
        Assert.Equal(75m, o.Totals.CompletionRate);
        Assert.Equal(11, o.Totals.BilledCalls);
        Assert.Equal(3, o.Totals.CallsByKind.Bureau);
        Assert.Equal(2, o.Totals.CallsByKind.Advisory);
        // a1, a2: 100 + 50 + 1000 + 25 each; f1: 100 + 50 + 1000.
        Assert.Equal(1175m * 2 + 1150m, o.Totals.Amount);
        Assert.Equal(30_000, o.Totals.MedianDurationMs);
        Assert.Equal(3, o.Totals.AdvisoriesScored);
        Assert.Equal(60m, o.Totals.AverageAdvisoryScore);
        Assert.Equal(3, o.Totals.BureauBilled);
        Assert.Equal(1, o.Previous.Submitted);

        Assert.Equal(9, o.Daily.Count);
        Assert.Equal(2, o.Daily.Single(d => d.Date == DateOnly.FromDateTime(Now.AddDays(-1))).Submitted);
        Assert.Equal(["AgriPay", "FarmCo"], o.ConsumerNames);

        var agri = o.Consumers.Single(c => c.Name == "AgriPay");
        Assert.Equal(3, agri.Checks);
        Assert.Equal(2, agri.Completed);
        Assert.Equal(2350m, agri.Amount);
        Assert.Equal(55m, agri.AverageAdvisoryScore);
        var farm = o.Consumers.Single(c => c.Name == "FarmCo");
        Assert.Equal(1, farm.Failed);
        Assert.Equal(1, farm.CallbacksFailed);

        Assert.Equal(3, o.Callbacks.Requested);
        Assert.Equal(2, o.Callbacks.Delivered);
        Assert.Equal(1, o.Callbacks.DeliveredFirstAttempt);
        Assert.Equal(1, o.Callbacks.Failed);
        Assert.Equal(Math.Round(100m * 2 / 3, 1), o.Totals.CallbackDeliveryRate);

        Assert.Contains(o.SectionOutcomes, n => n.Name == SectionStatuses.Ok && n.Count == 13);
        Assert.Contains(o.SectionOutcomes, n => n.Name == SectionStatuses.Unavailable && n.Count == 2);
        Assert.DoesNotContain(o.SectionOutcomes, n => n.Name == SectionStatuses.NotRequested);
        var bureau = o.Sections.Single(s => s.Section == CustomerCheckSections.CreditBureau);
        Assert.Equal(3, bureau.Requested);
        Assert.Equal(3, bureau.Ok);
        Assert.Single(o.BureauUsage);
        Assert.Equal("FIRST_CENTRAL billed", o.BureauUsage[0].Name);
        Assert.Equal(["Low", "High"], o.AdvisoryRatings.Select(r => r.Name));
        Assert.Equal(["Approve", "Decline"], o.Recommendations.Select(r => r.Name));
        Assert.Equal(7, o.Heatmap.Length);
        Assert.Equal(5, o.Heatmap.Sum(r => r.Sum()));
        var failure = Assert.Single(o.RecentFailures);
        Assert.Equal("f-2", failure.RequestId);
        Assert.Equal(CustomerCheckErrors.TimedOut, failure.ErrorCode);
    }

    [Fact]
    public async Task Overview_can_be_narrowed_to_one_consumer()
    {
        await using var db = await SeededAsync();
        var o = await new CustomerCheckReportingService(db, Cipher).GetOverviewAsync(Now.AddDays(-7), Now.AddDays(1), FarmCo);

        Assert.Equal(2, o.Totals.Submitted);
        Assert.Single(o.Consumers);
        Assert.Equal(["FarmCo"], o.ConsumerNames);
        Assert.Equal(1150m, o.Totals.Amount);
    }

    [Fact]
    public async Task Statement_lists_each_check_and_totals_by_item()
    {
        await using var db = await SeededAsync();
        var s = await new CustomerCheckReportingService(db, Cipher).GetStatementAsync(AgriPay, Now.AddDays(-7), Now.AddDays(1));

        Assert.Equal("AgriPay", s.ConsumerName);
        Assert.Equal(3, s.Lines.Count);
        Assert.Equal(2350m, s.Total);
        Assert.Equal(0m, s.Lines.Single(l => l.RequestId == "a-3").Amount);
        var check = s.ByItem.Single(i => i.Item == CustomerCheckPriceItems.Check);
        Assert.Equal(2, check.Quantity);
        Assert.Equal(200m, check.Amount);
        var bureauCalls = s.ByItem.Single(i => i.Item == CustomerCheckPriceItems.CallBureau);
        Assert.Equal(2000m, bureauCalls.Amount);
        Assert.Contains(s.ByItem, i => i.Item == CustomerCheckPriceItems.CallIdentity && i.UnitPrice == 0m);

        var csv = System.Text.Encoding.UTF8.GetString(ExportCustomerCheckStatementHandler.Csv(s));
        Assert.Contains("Customer check statement,AgriPay", csv);
        Assert.Contains("a-1", csv);
        Assert.Contains("Total,,,2350.00", csv);
        Assert.DoesNotContain("h-a-1", csv);
    }

    [Fact]
    public async Task Jobs_search_and_detail_decrypt_the_result_for_staff()
    {
        await using var db = await SeededAsync();
        var reporting = new CustomerCheckReportingService(db, Cipher);

        var (items, total) = await reporting.SearchJobsAsync(new Domain.Interfaces.CustomerCheckJobFilter(null, null, null, CustomerCheckJob.Completed, "a-", null, 1, 10));
        Assert.Equal(3, total);
        Assert.All(items, i => Assert.Equal(CustomerCheckJob.Completed, i.Status));
        Assert.Equal("a-1", items[0].RequestId);

        var detail = await reporting.GetJobDetailAsync(items[0].Id);
        Assert.NotNull(detail);
        Assert.Equal("{\"requestId\":\"a-1\"}", detail!.ResultJson);
        Assert.Equal("p.example.com", detail.CallbackHost);
        Assert.StartsWith("evt_", detail.EventId);
        Assert.Contains("creditBureau=OK", detail.SectionOutcomes);

        Assert.Null(await reporting.GetJobDetailAsync(Guid.NewGuid()));
    }
}
