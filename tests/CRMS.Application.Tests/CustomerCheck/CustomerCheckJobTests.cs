using CRMS.Application.CustomerCheck;
using CRMS.Domain.Aggregates.CustomerCheck;
using Xunit;

namespace CRMS.Application.Tests.CustomerCheck;

/// <summary>The asynchronous side of the customer check (docs/Customer_Check.md, "The flow"): the
/// job's life, callback scheduling, the consumer's callback registration, and the request-side
/// rules for callback URLs and the history search.</summary>
public class CustomerCheckJobTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly CustomerCheckSettings Settings = new();

    private static ApiConsumer Consumer(string? callbackUrl = null, IEnumerable<string>? hosts = null)
    {
        var c = ApiConsumer.Create("AgriPay", ApiKeys.Hash("k"), "crms_abc", CustomerCheckSections.All, 30, false, 30, null, Guid.NewGuid()).Value;
        Assert.True(c.SetCallback(callbackUrl, hosts, 100, Guid.NewGuid()).IsSuccess);
        return c;
    }

    private static CustomerCheckJob Job(string? callbackUrl = "https://partner.example.com/hook") =>
        CustomerCheckJob.Create(Guid.NewGuid(), "AgriPay", "req-1", "hash", "5678", CustomerCheckSections.All, "v1.cipher", callbackUrl);

    // ── Job life ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_new_job_is_queued_with_no_callback_yet_and_a_stable_event_id()
    {
        var job = Job();

        Assert.Equal(CustomerCheckJob.Queued, job.Status);
        Assert.Equal(CustomerCheckJob.CallbackNotRequested, job.CallbackStatus);
        Assert.StartsWith("evt_", job.EventId);
        Assert.True(job.IsOpen);
        Assert.Equal(0, job.RunAttempts);
        Assert.Equal(CustomerCheckSections.All, job.SectionList);
    }

    [Fact]
    public void Completing_schedules_the_callback_at_once_when_a_url_is_registered()
    {
        var job = Job();
        job.Start(Now);

        job.Complete("v1.result", [new("identity", "OK")], "FirstCentral", false, 3, 4200, Now.AddSeconds(40));

        Assert.Equal(CustomerCheckJob.Completed, job.Status);
        Assert.False(job.IsOpen);
        Assert.Equal(1, job.RunAttempts);
        Assert.Equal(CustomerCheckJob.CallbackPending, job.CallbackStatus);
        Assert.Equal(Now.AddSeconds(40), job.NextCallbackAt);
        Assert.Equal("identity=OK", job.SectionOutcomes);
        Assert.Equal(3, job.BilledCalls);
    }

    [Fact]
    public void Completing_without_a_url_leaves_the_callback_not_requested()
    {
        var job = Job(callbackUrl: null);
        job.Start(Now);

        job.Complete("v1.result", [], null, false, 0, 10, Now);

        Assert.Equal(CustomerCheckJob.CallbackNotRequested, job.CallbackStatus);
        Assert.Null(job.NextCallbackAt);
    }

    [Fact]
    public void Failing_keeps_the_error_and_still_notifies_the_consumer()
    {
        var job = Job();
        job.Start(Now);

        job.Fail(CustomerCheckErrors.TimedOut, "Too slow.", 300_000, Now);

        Assert.Equal(CustomerCheckJob.Failed, job.Status);
        Assert.Equal(CustomerCheckErrors.TimedOut, job.ErrorCode);
        Assert.Equal("Too slow.", job.ErrorMessage);
        Assert.Equal(CustomerCheckJob.CallbackPending, job.CallbackStatus);
    }

    // ── Callback attempts ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_successful_attempt_marks_the_callback_delivered()
    {
        var job = Job();
        job.Start(Now);
        job.Complete("v1.result", [], null, false, 0, 10, Now);

        job.RecordCallbackAttempt(true, 200, null, null, Now.AddSeconds(1));

        Assert.Equal(CustomerCheckJob.CallbackDelivered, job.CallbackStatus);
        Assert.Equal(1, job.CallbackAttempts);
        Assert.Equal(200, job.LastCallbackStatusCode);
        Assert.Equal(Now.AddSeconds(1), job.CallbackDeliveredAt);
        Assert.Null(job.NextCallbackAt);
    }

    [Fact]
    public void A_failed_attempt_with_a_next_time_stays_pending_and_one_without_gives_up()
    {
        var job = Job();
        job.Start(Now);
        job.Complete("v1.result", [], null, false, 0, 10, Now);

        job.RecordCallbackAttempt(false, 503, "HTTP 503", Now.AddMinutes(1), Now);
        Assert.Equal(CustomerCheckJob.CallbackPending, job.CallbackStatus);
        Assert.Equal(Now.AddMinutes(1), job.NextCallbackAt);
        Assert.Equal("HTTP 503", job.LastCallbackError);

        job.RecordCallbackAttempt(false, null, "No answer in time.", null, Now.AddMinutes(1));
        Assert.Equal(CustomerCheckJob.CallbackFailed, job.CallbackStatus);
        Assert.Equal(2, job.CallbackAttempts);
        Assert.Null(job.NextCallbackAt);
    }

    [Fact]
    public void A_resend_needs_a_url_and_a_finished_job_and_keeps_the_event_id()
    {
        var noUrl = Job(callbackUrl: null);
        Assert.True(noUrl.ResendCallback(Now).IsFailure);

        var open = Job();
        Assert.True(open.ResendCallback(Now).IsFailure);

        var done = Job();
        done.Start(Now);
        done.Complete("v1.result", [], null, false, 0, 10, Now);
        done.RecordCallbackAttempt(false, null, "x", null, Now);
        var eventId = done.EventId;

        Assert.True(done.ResendCallback(Now.AddHours(1)).IsSuccess);
        Assert.Equal(CustomerCheckJob.CallbackPending, done.CallbackStatus);
        Assert.Equal(Now.AddHours(1), done.NextCallbackAt);
        Assert.Equal(eventId, done.EventId);
    }

    // ── Consumer callback registration ───────────────────────────────────────────────────────────

    [Fact]
    public void A_consumer_allows_its_own_callback_host_and_the_listed_ones_only()
    {
        var c = Consumer("https://partner.example.com/hook", ["Staging.Partner.example.com", " api.partner.example.com. "]);

        Assert.True(c.AllowsCallbackHost("partner.example.com"));
        Assert.True(c.AllowsCallbackHost("staging.partner.example.com"));
        Assert.True(c.AllowsCallbackHost("API.partner.example.com"));
        Assert.False(c.AllowsCallbackHost("evil.example.com"));
        Assert.Equal(["staging.partner.example.com", "api.partner.example.com"], c.AllowedCallbackHostList);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://partner.example.com/hook")]
    public void A_bad_callback_url_is_refused_at_registration(string url)
    {
        var c = Consumer();
        Assert.True(c.SetCallback(url, null, 100, Guid.NewGuid()).IsFailure);
    }

    [Fact]
    public void Queue_limits_and_host_names_are_checked_at_registration()
    {
        var c = Consumer();
        Assert.True(c.SetCallback(null, null, 0, Guid.NewGuid()).IsFailure);
        Assert.True(c.SetCallback(null, ["bad host name"], 100, Guid.NewGuid()).IsFailure);
        Assert.True(c.SetCallback(null, null, 25, Guid.NewGuid()).IsSuccess);
        Assert.Equal(25, c.MaxQueuedJobs);
    }

    // ── Request-side callback rules ──────────────────────────────────────────────────────────────

    [Fact]
    public void No_request_url_means_the_registered_one()
    {
        var c = Consumer("https://partner.example.com/hook");
        var (url, error) = CustomerCheckRules.ResolveCallback(null, c, requireHttps: true);
        Assert.Null(error);
        Assert.Equal("https://partner.example.com/hook", url);

        var (none, noneError) = CustomerCheckRules.ResolveCallback(" ", Consumer(), requireHttps: true);
        Assert.Null(noneError);
        Assert.Null(none);
    }

    [Fact]
    public void A_request_url_must_sit_on_an_allowed_host_over_https()
    {
        var c = Consumer("https://partner.example.com/hook", ["staging.partner.example.com"]);

        var (ok, okError) = CustomerCheckRules.ResolveCallback("https://staging.partner.example.com/v2/hook?x=1", c, true);
        Assert.Null(okError);
        Assert.Equal("https://staging.partner.example.com/v2/hook?x=1", ok);

        Assert.NotNull(CustomerCheckRules.ResolveCallback("https://evil.example.com/hook", c, true).Error);
        Assert.NotNull(CustomerCheckRules.ResolveCallback("http://partner.example.com/hook", c, true).Error);
        Assert.Null(CustomerCheckRules.ResolveCallback("http://partner.example.com/hook", c, false).Error);
        Assert.NotNull(CustomerCheckRules.ResolveCallback("https://user:pw@partner.example.com/hook", c, true).Error);
        Assert.NotNull(CustomerCheckRules.ResolveCallback("nonsense", c, true).Error);
    }

    [Fact]
    public void The_request_id_is_required_now_that_it_is_the_idempotency_key()
    {
        var (query, errors) = CustomerCheckRules.Validate(new CustomerCheckRequest(null, "22212345678", null, null, null), Settings);
        Assert.Null(query);
        Assert.Contains("requestId is required.", errors);
    }

    // ── History search ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void History_filters_are_scoped_hashed_and_bounded()
    {
        var consumerId = Guid.NewGuid();
        var request = new CustomerCheckHistoryRequest(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), "completed", " req-9 ", "222 1234 5678", 2, 20);

        var (filter, errors) = CustomerCheckRules.ValidateHistory(request, consumerId, v => "h:" + v, Settings);

        Assert.Empty(errors);
        Assert.NotNull(filter);
        Assert.Equal(consumerId, filter!.ConsumerId);
        Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), filter.From);
        Assert.Equal(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), filter.To);
        Assert.Equal(CustomerCheckJob.Completed, filter.Status);
        Assert.Equal("req-9", filter.RequestId);
        Assert.Equal("h:22212345678", filter.BvnHash);
        Assert.Equal(2, filter.Page);
        Assert.Equal(20, filter.PageSize);
    }

    [Fact]
    public void History_defaults_and_refusals()
    {
        var (filter, _) = CustomerCheckRules.ValidateHistory(new(null, null, null, null, null, null, null), Guid.NewGuid(), v => v, Settings);
        Assert.NotNull(filter);
        Assert.Equal(1, filter!.Page);
        Assert.Equal(50, filter.PageSize);
        Assert.Null(filter.Status);

        var (bad, errors) = CustomerCheckRules.ValidateHistory(
            new(new DateOnly(2026, 11, 30), new DateOnly(2026, 11, 1), "DONE", null, "123", 0, 500), Guid.NewGuid(), v => v, Settings);
        Assert.Null(bad);
        Assert.Contains("from must be on or before to.", errors);
        Assert.Contains(errors, e => e.StartsWith("status must be one of"));
        Assert.Contains("bvn must be 11 digits.", errors);
        Assert.Contains("page must be 1 or more.", errors);
        Assert.Contains($"pageSize must be between 1 and {Settings.HistoryMaxPageSize}.", errors);
        Assert.DoesNotContain(errors, e => e.Contains("123"));
    }

    [Fact]
    public void Job_ids_round_trip_in_their_compact_form()
    {
        var id = Guid.NewGuid();
        var text = CustomerCheckJobIds.Format(id);
        Assert.Equal(32, text.Length);
        Assert.True(CustomerCheckJobIds.TryParse(text, out var parsed));
        Assert.Equal(id, parsed);
        Assert.True(CustomerCheckJobIds.TryParse(id.ToString(), out _));
        Assert.False(CustomerCheckJobIds.TryParse("nope", out _));
        Assert.Equal($"/api/v1/customer-check/{text}", CustomerCheckJobIds.StatusPath(id));
    }
}
