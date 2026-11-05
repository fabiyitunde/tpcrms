using System.Net;
using System.Text.Json;
using CRMS.Application.CustomerCheck;
using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.BackgroundServices;
using CRMS.Infrastructure.ExternalServices.CustomerCheck;
using CRMS.Infrastructure.ExternalServices.Rhshf;
using CRMS.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>The background side of the customer check: the cipher, the signed callback, the job
/// runner, and the callback dispatcher with its retry schedule.</summary>
public class CustomerCheckJobsTests
{
    private static readonly byte[] Key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
    private static readonly CustomerCheckCipher Cipher = new(Key);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static ApiConsumer Consumer(bool withSecret = true)
    {
        var c = ApiConsumer.Create("AgriPay", ApiKeys.Hash("k"), "crms_abc", CustomerCheckSections.All, 30, false, 30, null, Guid.NewGuid()).Value;
        if (withSecret) c.SetCallbackSecret(Cipher.Protect("whsec-test"), "crms_whsec_abc", Guid.NewGuid());
        return c;
    }

    private static CustomerCheckQuery Query() =>
        new("req-1", "22212345678", ["0123456789"], CustomerCheckSections.All, 12, true, false, null, null, "https://partner.example.com/hook");

    private static CustomerCheckJob ProcessingJob(ApiConsumer consumer, string? callbackUrl = "https://partner.example.com/hook")
    {
        var job = CustomerCheckJob.Create(consumer.Id, consumer.Name, "req-1", "bvn-hash", "5678", CustomerCheckSections.All,
            Cipher.Protect(JsonSerializer.Serialize(Query(), Json)), callbackUrl);
        job.Start(DateTime.UtcNow);
        return job;
    }

    private static CustomerCheckResponse Response() => new(
        "req-1", DateTime.UtcNow, "AgriPay", new CheckSubject("5678", ["****6789"]), CustomerCheckSections.All,
        IdentitySection.Empty(SectionStatuses.Ok), CreditBureauSection.Empty(SectionStatuses.NoRecord),
        FraudCheckSection.Empty(SectionStatuses.Ok), BankAccountsSection.Empty(SectionStatuses.Ok),
        BankLoansSection.Empty(SectionStatuses.Ok), CrmsFacilitiesSection.Empty(SectionStatuses.Ok), AiAdvisorySection.Empty(SectionStatuses.NotRequested),
        new Dictionary<string, string>(), []);

    // ── Cipher ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Cipher_round_trips_and_refuses_tampering_or_another_key()
    {
        var text = "{\"bvn\":\"22212345678\"}";
        var sealed1 = Cipher.Protect(text);
        var sealed2 = Cipher.Protect(text);

        Assert.StartsWith("v1.", sealed1);
        Assert.NotEqual(sealed1, sealed2);
        Assert.DoesNotContain("22212345678", sealed1);
        Assert.Equal(text, Cipher.Unprotect(sealed1));

        var bytes = Convert.FromBase64String(sealed1[3..]);
        bytes[^1] ^= 0x01;
        Assert.ThrowsAny<Exception>(() => Cipher.Unprotect("v1." + Convert.ToBase64String(bytes)));

        var other = new CustomerCheckCipher(Enumerable.Repeat((byte)9, 32).ToArray());
        Assert.ThrowsAny<Exception>(() => other.Unprotect(sealed1));
        Assert.ThrowsAny<Exception>(() => Cipher.Unprotect("plain"));
    }

    [Fact]
    public void Cipher_key_comes_from_the_configured_value_or_is_derived()
    {
        var configured = Convert.ToBase64String(Key);
        Assert.Equal(Key, CustomerCheckCipher.ResolveKey(new CustomerCheckSettings { DataKey = configured }, new RhshfSettings()));

        var derived = CustomerCheckCipher.ResolveKey(new CustomerCheckSettings { HashKey = "hk" }, new RhshfSettings());
        Assert.Equal(32, derived.Length);
        Assert.Equal(derived, CustomerCheckCipher.ResolveKey(new CustomerCheckSettings { HashKey = "hk" }, new RhshfSettings()));
        Assert.NotEqual(derived, CustomerCheckCipher.ResolveKey(new CustomerCheckSettings { HashKey = "other" }, new RhshfSettings()));

        Assert.Throws<InvalidOperationException>(() => CustomerCheckCipher.ResolveKey(new CustomerCheckSettings { DataKey = "short" }, new RhshfSettings()));
        Assert.Throws<InvalidOperationException>(() => CustomerCheckCipher.ResolveKey(new CustomerCheckSettings { DataKey = Convert.ToBase64String(new byte[16]) }, new RhshfSettings()));
    }

    // ── Sender ───────────────────────────────────────────────────────────────────────────────────

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request;
        public string? Body;
        public Func<HttpResponseMessage> Respond = () => new HttpResponseMessage(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(ct);
            return Respond();
        }
    }

    [Fact]
    public async Task Sender_signs_the_timestamp_and_body_with_the_consumer_secret()
    {
        var handler = new CapturingHandler();
        var sender = new CustomerCheckCallbackSender(new HttpClient(handler), NullLogger<CustomerCheckCallbackSender>.Instance);

        var sent = await sender.SendAsync("https://partner.example.com/hook", "whsec-test", "evt_1", CustomerCheckEvents.Completed, "{\"a\":1}");

        Assert.True(sent.Succeeded);
        Assert.Equal(200, sent.StatusCode);
        Assert.Equal("{\"a\":1}", handler.Body);
        Assert.Equal("evt_1", handler.Request!.Headers.GetValues("X-CRMS-Event-Id").Single());
        Assert.Equal(CustomerCheckEvents.Completed, handler.Request.Headers.GetValues("X-CRMS-Event-Type").Single());
        var timestamp = handler.Request.Headers.GetValues("X-CRMS-Timestamp").Single();
        Assert.True(Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - long.Parse(timestamp)) < 60);
        var signature = handler.Request.Headers.GetValues("X-CRMS-Signature-V2").Single();
        Assert.Equal("sha256=" + RhshfCallbackService.ComputeTimestampedSignature(timestamp, "{\"a\":1}", "whsec-test"), signature);
        Assert.False(handler.Request.Headers.Contains("X-CRMS-Signature"));
    }

    [Fact]
    public async Task Sender_reports_a_refusal_and_a_dead_host_without_throwing()
    {
        var refusing = new CapturingHandler { Respond = () => new HttpResponseMessage(HttpStatusCode.BadGateway) };
        var sender = new CustomerCheckCallbackSender(new HttpClient(refusing), NullLogger<CustomerCheckCallbackSender>.Instance);
        var refused = await sender.SendAsync("https://partner.example.com/hook", "s", "evt", "T", "{}");
        Assert.False(refused.Succeeded);
        Assert.Equal(502, refused.StatusCode);
        Assert.Equal("HTTP 502", refused.Error);

        var dead = new CapturingHandler { Respond = () => throw new HttpRequestException("No such host is known.") };
        sender = new CustomerCheckCallbackSender(new HttpClient(dead), NullLogger<CustomerCheckCallbackSender>.Instance);
        var failed = await sender.SendAsync("https://partner.example.com/hook", "s", "evt", "T", "{}");
        Assert.False(failed.Succeeded);
        Assert.Null(failed.StatusCode);
        Assert.Contains("No such host", failed.Error);
    }

    // ── Runner ───────────────────────────────────────────────────────────────────────────────────

    private sealed class RunnerHarness
    {
        public readonly Mock<ICustomerCheckJobRepository> Jobs = new();
        public readonly Mock<IApiConsumerRepository> Consumers = new();
        public readonly Mock<ICustomerCheckService> Service = new();
        public readonly CustomerCheckSettings Settings = new() { MaxRunAttempts = 2, JobTimeoutMs = 2000 };
        public CustomerCheckLog? Recorded;
        public int Saves;

        public RunnerHarness(ApiConsumer consumer, CustomerCheckJob job)
        {
            Jobs.Setup(j => j.GetByIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
            Consumers.Setup(c => c.GetByIdAsync(consumer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(consumer);
            Service.Setup(s => s.HashIdentifier(It.IsAny<string>())).Returns<string>(v => "h:" + v);
            Service.Setup(s => s.RecordAsync(It.IsAny<CustomerCheckLog>(), It.IsAny<CancellationToken>()))
                .Callback<CustomerCheckLog, CancellationToken>((l, _) => { Recorded = l; Saves++; })
                .Returns(Task.CompletedTask);
        }

        public CustomerCheckJobRunner Runner() => new(
            Jobs.Object, Consumers.Object, Service.Object, Cipher, Options.Create(Settings), NullLogger<CustomerCheckJobRunner>.Instance);
    }

    [Fact]
    public async Task Runner_completes_the_job_stores_the_result_encrypted_and_audits_with_the_job_id()
    {
        var consumer = Consumer();
        var job = ProcessingJob(consumer);
        var h = new RunnerHarness(consumer, job);
        h.Service.Setup(s => s.CheckAsync(consumer, It.Is<CustomerCheckQuery>(q => q.Bvn == "22212345678" && q.CallbackUrl != null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerCheckOutcome(Response(), "FirstCentral", true, new BilledCallCounts(1, 1, 0, 1, 0)));

        await h.Runner().RunAsync(job.Id, CancellationToken.None);

        Assert.Equal(CustomerCheckJob.Completed, job.Status);
        Assert.Equal(CustomerCheckJob.CallbackPending, job.CallbackStatus);
        Assert.NotNull(job.ResultCiphertext);
        Assert.DoesNotContain("req-1", job.ResultCiphertext);
        var stored = JsonSerializer.Deserialize<JsonElement>(Cipher.Unprotect(job.ResultCiphertext!), Json);
        Assert.Equal("req-1", stored.GetProperty("requestId").GetString());
        Assert.Equal("NO_RECORD", stored.GetProperty("creditBureau").GetProperty("status").GetString());
        Assert.Equal("FirstCentral", job.BureauUsed);
        Assert.True(job.BureauReused);
        Assert.Equal(3, job.BilledCalls);
        Assert.Contains("creditBureau=NO_RECORD", job.SectionOutcomes);

        Assert.NotNull(h.Recorded);
        Assert.Equal(job.Id, h.Recorded!.JobId);
        Assert.Equal("bvn-hash", h.Recorded.BvnHash);
        Assert.Equal("h:0123456789", h.Recorded.NubanHashes);
        Assert.Equal(1, h.Recorded.IdentityCalls);
        Assert.Equal(1, h.Recorded.FraudCalls);
        Assert.Null(h.Recorded.Error);
        Assert.NotNull(consumer.LastUsedAt);
        h.Consumers.Verify(c => c.Update(consumer), Times.Once);
        h.Jobs.Verify(j => j.Update(job), Times.Once);
    }

    [Fact]
    public async Task Runner_fails_the_job_when_the_check_throws_and_still_audits()
    {
        var consumer = Consumer();
        var job = ProcessingJob(consumer);
        var h = new RunnerHarness(consumer, job);
        h.Service.Setup(s => s.CheckAsync(consumer, It.IsAny<CustomerCheckQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await h.Runner().RunAsync(job.Id, CancellationToken.None);

        Assert.Equal(CustomerCheckJob.Failed, job.Status);
        Assert.Equal(CustomerCheckErrors.CheckFailed, job.ErrorCode);
        Assert.DoesNotContain("boom", job.ErrorMessage);
        Assert.Null(job.ResultCiphertext);
        Assert.Equal(CustomerCheckJob.CallbackPending, job.CallbackStatus);
        Assert.Equal(CustomerCheckErrors.CheckFailed, h.Recorded!.Error);
        Assert.Contains("identity=ERROR", h.Recorded.SectionOutcomes);
    }

    [Fact]
    public async Task Runner_reports_a_timeout_as_such()
    {
        var consumer = Consumer();
        var job = ProcessingJob(consumer);
        var h = new RunnerHarness(consumer, job);
        h.Settings.JobTimeoutMs = 50;
        h.Service.Setup(s => s.CheckAsync(consumer, It.IsAny<CustomerCheckQuery>(), It.IsAny<CancellationToken>()))
            .Returns<ApiConsumer, CustomerCheckQuery, CancellationToken>(async (_, _, ct) => { await Task.Delay(5000, ct); return null!; });

        await h.Runner().RunAsync(job.Id, CancellationToken.None);

        Assert.Equal(CustomerCheckJob.Failed, job.Status);
        Assert.Equal(CustomerCheckErrors.TimedOut, job.ErrorCode);
    }

    [Fact]
    public async Task Runner_fails_a_job_interrupted_too_often_without_running_it()
    {
        var consumer = Consumer();
        var job = ProcessingJob(consumer);
        job.Start(DateTime.UtcNow);
        job.Start(DateTime.UtcNow);
        var h = new RunnerHarness(consumer, job);

        await h.Runner().RunAsync(job.Id, CancellationToken.None);

        Assert.Equal(CustomerCheckJob.Failed, job.Status);
        Assert.Equal(CustomerCheckErrors.Interrupted, job.ErrorCode);
        h.Service.Verify(s => s.CheckAsync(It.IsAny<ApiConsumer>(), It.IsAny<CustomerCheckQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(1, h.Saves);
    }

    [Fact]
    public async Task Runner_fails_a_job_whose_consumer_was_switched_off()
    {
        var consumer = Consumer();
        consumer.SetActive(false, Guid.NewGuid());
        var job = ProcessingJob(consumer);
        var h = new RunnerHarness(consumer, job);

        await h.Runner().RunAsync(job.Id, CancellationToken.None);

        Assert.Equal(CustomerCheckJob.Failed, job.Status);
        Assert.Equal(CustomerCheckErrors.ConsumerInactive, job.ErrorCode);
        h.Service.Verify(s => s.CheckAsync(It.IsAny<ApiConsumer>(), It.IsAny<CustomerCheckQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Runner_ignores_a_job_that_is_not_processing()
    {
        var consumer = Consumer();
        var job = CustomerCheckJob.Create(consumer.Id, consumer.Name, "r", "h", "0000", CustomerCheckSections.All, "v1.x", null);
        var h = new RunnerHarness(consumer, job);

        await h.Runner().RunAsync(job.Id, CancellationToken.None);

        Assert.Equal(CustomerCheckJob.Queued, job.Status);
        Assert.Equal(0, h.Saves);
    }

    // ── Dispatcher ───────────────────────────────────────────────────────────────────────────────

    private sealed class DispatcherHarness
    {
        public readonly Mock<ICustomerCheckJobRepository> Jobs = new();
        public readonly Mock<IApiConsumerRepository> Consumers = new();
        public readonly Mock<ICustomerCheckCallbackSender> Sender = new();
        public readonly Mock<IUnitOfWork> UnitOfWork = new();
        public readonly List<(string Url, string Secret, string EventId, string EventType, string Body)> Sent = [];

        public DispatcherHarness(ApiConsumer consumer, CustomerCheckJob job, CallbackSendResult result)
        {
            Jobs.Setup(j => j.GetByIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
            Jobs.Setup(j => j.GetDueCallbackIdsAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([job.Id]);
            Consumers.Setup(c => c.GetByIdAsync(consumer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(consumer);
            Sender.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, string, string, string, CancellationToken>((u, s, e, t, b, _) => Sent.Add((u, s, e, t, b)))
                .ReturnsAsync(result);
            UnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        }

        public CustomerCheckCallbackDispatcher Dispatcher() => new(
            Jobs.Object, Consumers.Object, Sender.Object, Cipher, UnitOfWork.Object, NullLogger<CustomerCheckCallbackDispatcher>.Instance);
    }

    private static CustomerCheckJob CompletedJob(ApiConsumer consumer)
    {
        var job = ProcessingJob(consumer);
        job.Complete(Cipher.Protect(JsonSerializer.Serialize(Response(), Json)), [new("identity", "OK")], null, false, 2, 100, DateTime.UtcNow);
        return job;
    }

    [Fact]
    public async Task Dispatcher_posts_the_stored_result_signed_with_the_consumer_secret_and_marks_it_delivered()
    {
        var consumer = Consumer();
        var job = CompletedJob(consumer);
        var h = new DispatcherHarness(consumer, job, new CallbackSendResult(true, 200, null));

        var processed = await h.Dispatcher().ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, processed);
        var sent = Assert.Single(h.Sent);
        Assert.Equal("https://partner.example.com/hook", sent.Url);
        Assert.Equal("whsec-test", sent.Secret);
        Assert.Equal(job.EventId, sent.EventId);
        Assert.Equal(CustomerCheckEvents.Completed, sent.EventType);
        var envelope = JsonSerializer.Deserialize<JsonElement>(sent.Body, Json);
        Assert.Equal(CustomerCheckEvents.Completed, envelope.GetProperty("eventType").GetString());
        Assert.Equal(CustomerCheckJobIds.Format(job.Id), envelope.GetProperty("jobId").GetString());
        Assert.Equal("req-1", envelope.GetProperty("requestId").GetString());
        Assert.Equal("COMPLETED", envelope.GetProperty("status").GetString());
        Assert.Equal("req-1", envelope.GetProperty("result").GetProperty("requestId").GetString());
        Assert.Equal(JsonValueKind.Null, envelope.GetProperty("error").ValueKind);

        Assert.Equal(CustomerCheckJob.CallbackDelivered, job.CallbackStatus);
        Assert.Equal(1, job.CallbackAttempts);
        h.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Dispatcher_retries_on_the_schedule_then_gives_up()
    {
        var consumer = Consumer();
        var job = CompletedJob(consumer);
        var h = new DispatcherHarness(consumer, job, new CallbackSendResult(false, 503, "HTTP 503"));
        var dispatcher = h.Dispatcher();

        var expected = CustomerCheckCallbackDispatcher.RetryDelays;
        for (var attempt = 0; attempt < expected.Length; attempt++)
        {
            var before = DateTime.UtcNow;
            await dispatcher.DeliverAsync(job, CancellationToken.None);
            Assert.Equal(CustomerCheckJob.CallbackPending, job.CallbackStatus);
            Assert.Equal(attempt + 1, job.CallbackAttempts);
            Assert.InRange(job.NextCallbackAt!.Value, before.Add(expected[attempt]), DateTime.UtcNow.Add(expected[attempt]));
        }

        await dispatcher.DeliverAsync(job, CancellationToken.None);
        Assert.Equal(CustomerCheckJob.CallbackFailed, job.CallbackStatus);
        Assert.Equal(expected.Length + 1, job.CallbackAttempts);
        Assert.Null(job.NextCallbackAt);
        Assert.Equal(expected.Length + 1, h.Sent.Count);
        Assert.All(h.Sent, s => Assert.Equal(job.EventId, s.EventId));
    }

    [Fact]
    public async Task Dispatcher_sends_a_failed_check_as_a_failed_event_with_the_error()
    {
        var consumer = Consumer();
        var job = ProcessingJob(consumer);
        job.Fail(CustomerCheckErrors.TimedOut, "Too slow.", 1, DateTime.UtcNow);
        var h = new DispatcherHarness(consumer, job, new CallbackSendResult(true, 204, null));

        await h.Dispatcher().DeliverAsync(job, CancellationToken.None);

        var envelope = JsonSerializer.Deserialize<JsonElement>(h.Sent.Single().Body, Json);
        Assert.Equal(CustomerCheckEvents.Failed, envelope.GetProperty("eventType").GetString());
        Assert.Equal(CustomerCheckErrors.TimedOut, envelope.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, envelope.GetProperty("result").ValueKind);
    }

    [Fact]
    public async Task Dispatcher_cannot_sign_without_a_secret_and_says_so()
    {
        var consumer = Consumer(withSecret: false);
        var job = CompletedJob(consumer);
        var h = new DispatcherHarness(consumer, job, new CallbackSendResult(true, 200, null));

        await h.Dispatcher().DeliverAsync(job, CancellationToken.None);

        Assert.Empty(h.Sent);
        Assert.Equal(CustomerCheckJob.CallbackPending, job.CallbackStatus);
        Assert.Contains("signing secret", job.LastCallbackError);
    }
}
