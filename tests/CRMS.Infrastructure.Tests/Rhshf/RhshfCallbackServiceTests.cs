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
        public HttpStatusCode ResponseStatus { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CapturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            CapturedSignatureHeader = request.Headers.TryGetValues("X-CRMS-Signature", out var values) ? values.First() : null;
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
}
