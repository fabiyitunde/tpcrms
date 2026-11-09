using System.Net;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.ExternalServices.SmartComply;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// The SmartComply key check and the wording of a rejected key, against a stubbed HTTP handler.
/// On 2026-11-09 one production host carried a key SmartComply refused; every NAMP bureau job that
/// host picked up failed with a generic "API request failed", and the other host's key was fine,
/// so the fault looked like SmartComply's and not like configuration.
/// </summary>
public class SmartComplyAccessCheckTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.BadRequest;
        public string Body { get; set; } = "";
        public List<(string Path, string Content)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.RequestUri!.AbsolutePath, request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct)));
            return new HttpResponseMessage(Status) { Content = new StringContent(Body, System.Text.Encoding.UTF8, "application/json") };
        }
    }

    private static (SmartComplyProvider Provider, StubHandler Handler) Build()
    {
        var handler = new StubHandler();
        var settings = Options.Create(new SmartComplySettings { BaseUrl = "https://smartcomply.test", ApiKey = "wSxAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAzzz", UseMock = false });
        return (new SmartComplyProvider(new HttpClient(handler), settings, NullLogger<SmartComplyProvider>.Instance), handler);
    }

    [Fact]
    public async Task An_accepted_key_is_told_apart_by_the_validation_answer_and_no_subject_is_sent()
    {
        var (provider, handler) = Build();
        handler.Body = """{"status":"failed","data":[],"message":"BVN is required."}""";

        var check = await provider.CheckAccessAsync();

        Assert.True(check.Accepted);
        Assert.Contains("accepts", check.Summary);
        Assert.Equal(Environment.MachineName, check.Host);
        Assert.Equal("wSx…zzz (67 chars)", check.KeyHint);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/onboarding/individual/crc_summary/", request.Path);
        Assert.DoesNotContain("bvn", request.Content, StringComparison.OrdinalIgnoreCase);   // nothing to bill
    }

    [Fact]
    public async Task A_rejected_key_is_reported_as_such()
    {
        var (provider, handler) = Build();
        handler.Body = """{"status":"failed","data":[],"message":"Authorization Failed"}""";

        var check = await provider.CheckAccessAsync();

        Assert.False(check.Accepted);
        Assert.Contains("rejected this host's API key", check.Summary);
        Assert.Contains("Authorization Failed", check.Summary);
    }

    [Fact]
    public async Task An_unreachable_service_is_not_mistaken_for_a_bad_key()
    {
        var settings = Options.Create(new SmartComplySettings { BaseUrl = "https://127.0.0.1:1", ApiKey = "k", UseMock = false, TimeoutSeconds = 2 });
        var provider = new SmartComplyProvider(new HttpClient(), settings, NullLogger<SmartComplyProvider>.Instance);

        var check = await provider.CheckAccessAsync();

        Assert.False(check.Accepted);
        Assert.Contains("could not be reached", check.Summary);
    }

    [Fact]
    public async Task A_bureau_call_with_a_rejected_key_names_the_key_and_the_host()
    {
        var (provider, handler) = Build();
        handler.Body = """{"status":"failed","data":[],"message":"Authorization Failed"}""";

        var report = await provider.GetCRCFullAsync("12345678901");
        var score = await provider.GetCRCScoreAsync("12345678901");
        var business = await provider.GetCRCBusinessHistoryAsync("RC123");

        foreach (var error in new[] { report.Error, score.Error, business.Error })
        {
            Assert.StartsWith("API request failed:", error);
            Assert.Contains("rejected this server's API key", error);
            Assert.Contains(Environment.MachineName, error);
            Assert.Contains("SmartComply:ApiKey", error);
        }
    }

    [Fact]
    public async Task Other_failures_keep_the_status_and_the_providers_words()
    {
        var (provider, handler) = Build();
        handler.Status = HttpStatusCode.InternalServerError;
        handler.Body = """{"status":"failed","message":"CRC is unavailable"}""";

        var report = await provider.GetCRCFullAsync("12345678901");

        Assert.Equal("""API request failed: InternalServerError - {"status":"failed","message":"CRC is unavailable"}""", report.Error);
    }

    [Fact]
    public async Task The_mock_provider_always_reports_access()
    {
        var check = await new MockSmartComplyProvider(NullLogger<MockSmartComplyProvider>.Instance).CheckAccessAsync();
        Assert.True(check.Accepted);
        Assert.Contains("Mock", check.Summary);
    }
}
