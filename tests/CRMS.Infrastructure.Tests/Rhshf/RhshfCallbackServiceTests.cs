using System.Net;
using System.Security.Cryptography;
using System.Text;
using CRMS.Infrastructure.ExternalServices.Rhshf;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CRMS.Infrastructure.Tests.Rhshf;

public class RhshfCallbackServiceTests
{
    private const string Secret = "test-shared-secret";

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? CapturedBody { get; private set; }
        public string? CapturedSignatureHeader { get; private set; }
        public string? CapturedTimestampHeader { get; private set; }
        public string? CapturedSignatureV2Header { get; private set; }
        public HttpStatusCode ResponseStatus { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CapturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            CapturedSignatureHeader = request.Headers.TryGetValues("X-CRMS-Signature", out var values) ? values.First() : null;
            CapturedTimestampHeader = request.Headers.TryGetValues("X-CRMS-Timestamp", out var ts) ? ts.First() : null;
            CapturedSignatureV2Header = request.Headers.TryGetValues("X-CRMS-Signature-V2", out var v2) ? v2.First() : null;
            return new HttpResponseMessage(ResponseStatus);
        }
    }

    [Fact]
    public void ComputeSignature_IsVerifiableAgainstKnownSecretAndBody()
    {
        const string body = "{\"reference\":\"RHSHF-2026-000123\"}";

        var signature = RhshfCallbackService.ComputeSignature(body, Secret);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        Assert.Equal(expected, signature);
    }

    [Fact]
    public void ComputeSignature_DifferentBody_ProducesDifferentSignature()
    {
        var sig1 = RhshfCallbackService.ComputeSignature("{\"a\":1}", Secret);
        var sig2 = RhshfCallbackService.ComputeSignature("{\"a\":2}", Secret);

        Assert.NotEqual(sig1, sig2);
    }

    [Fact]
    public async Task SendAsync_SignsTheExactRawBodySent()
    {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler);
        var settings = Options.Create(new RhshfSettings { CallbackSigningSecret = Secret });
        var service = new RhshfCallbackService(httpClient, settings, NullLogger<RhshfCallbackService>.Instance);
        var payload = new { reference = "RHSHF-2026-000123", status = "APPROVED" };

        var result = await service.SendAsync("https://portal.example.gov.ng/webhook", payload);

        Assert.True(result.Succeeded);
        Assert.NotNull(handler.CapturedBody);
        var expectedSignature = $"sha256={RhshfCallbackService.ComputeSignature(handler.CapturedBody!, Secret)}";
        Assert.Equal(expectedSignature, handler.CapturedSignatureHeader);
    }

    [Fact]
    public async Task SendAsync_NonSuccessStatus_ReturnsFailure()
    {
        var handler = new CapturingHandler { ResponseStatus = HttpStatusCode.ServiceUnavailable };
        var httpClient = new HttpClient(handler);
        var settings = Options.Create(new RhshfSettings { CallbackSigningSecret = Secret });
        var service = new RhshfCallbackService(httpClient, settings, NullLogger<RhshfCallbackService>.Instance);

        var result = await service.SendAsync("https://portal.example.gov.ng/webhook", new { ok = true });

        Assert.False(result.Succeeded);
        Assert.Equal(503, result.StatusCode);
    }

    // ── X-CRMS-Timestamp + V2 signature (portal integration note §4.4) ───────
    //
    // A timestamp only prevents replay if it is signed — otherwise an attacker replaying a captured
    // callback just rewrites the header. But the portal verifies sha256(body) today, so V1 keeps its
    // exact meaning and V2 carries the timestamped variant until they migrate.

    [Fact]
    public async Task SendAsync_SendsATimestampHeader()
    {
        var handler = new CapturingHandler();
        var service = new RhshfCallbackService(
            new HttpClient(handler), Options.Create(new RhshfSettings { CallbackSigningSecret = Secret }),
            NullLogger<RhshfCallbackService>.Instance);
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await service.SendAsync("https://portal.example.gov.ng/webhook", new { reference = "RHSHF-2026-000123" });

        Assert.NotNull(handler.CapturedTimestampHeader);
        var sent = long.Parse(handler.CapturedTimestampHeader!);
        Assert.InRange(sent, before - 5, DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 5);
    }

    [Fact]
    public async Task SendAsync_V2Signature_CoversTheTimestampAndTheBody()
    {
        var handler = new CapturingHandler();
        var service = new RhshfCallbackService(
            new HttpClient(handler), Options.Create(new RhshfSettings { CallbackSigningSecret = Secret }),
            NullLogger<RhshfCallbackService>.Instance);

        await service.SendAsync("https://portal.example.gov.ng/webhook", new { reference = "RHSHF-2026-000123" });

        var expected = $"sha256={RhshfCallbackService.ComputeTimestampedSignature(
            handler.CapturedTimestampHeader!, handler.CapturedBody!, Secret)}";
        Assert.Equal(expected, handler.CapturedSignatureV2Header);
    }

    [Fact]
    public async Task SendAsync_V1Signature_IsUnchanged_SoTheExistingReceiverKeepsWorking()
    {
        var handler = new CapturingHandler();
        var service = new RhshfCallbackService(
            new HttpClient(handler), Options.Create(new RhshfSettings { CallbackSigningSecret = Secret }),
            NullLogger<RhshfCallbackService>.Instance);

        await service.SendAsync("https://portal.example.gov.ng/webhook", new { reference = "RHSHF-2026-000123" });

        // Body only — no timestamp mixed in. This is the compatibility guarantee.
        Assert.Equal(
            $"sha256={RhshfCallbackService.ComputeSignature(handler.CapturedBody!, Secret)}",
            handler.CapturedSignatureHeader);
        Assert.NotEqual(handler.CapturedSignatureHeader, handler.CapturedSignatureV2Header);
    }

    [Fact]
    public void TimestampedSignature_ChangesWithTheTimestamp_EvenForAnIdenticalBody()
    {
        // The property that makes replay detectable: the same captured body re-sent at a different
        // time cannot reuse the old signature.
        const string body = "{\"reference\":\"RHSHF-2026-000123\"}";

        var a = RhshfCallbackService.ComputeTimestampedSignature("1700000000", body, Secret);
        var b = RhshfCallbackService.ComputeTimestampedSignature("1700000060", body, Secret);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void TimestampedSignature_IsUnambiguous_BetweenTimestampAndBody()
    {
        // Without a separator, ("170", "0.body") and ("1700", ".body") would sign identical bytes.
        var a = RhshfCallbackService.ComputeTimestampedSignature("170", "0.body", Secret);
        var b = RhshfCallbackService.ComputeTimestampedSignature("1700", ".body", Secret);

        Assert.NotEqual(a, b);
    }
}
