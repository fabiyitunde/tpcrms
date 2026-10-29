using CRMS.Infrastructure.ExternalServices.Rhshf;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>The portal signs party-exposure requests the way the CRMS signs its callbacks.</summary>
public class PortalRequestSignatureTests
{
    private const string Secret = "shared-secret";
    private const string Body = "{\"requestId\":\"r1\"}";
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    private static string Sign(string ts, string body)
        => "sha256=" + RhshfCallbackService.ComputeTimestampedSignature(ts, body, Secret);

    [Fact]
    public void A_correctly_signed_request_passes()
    {
        var ts = Now.ToUnixTimeSeconds().ToString();
        Assert.Null(PortalRequestSignature.Verify(ts, Sign(ts, Body), Body, Secret, 300, Now));
    }

    [Fact]
    public void A_changed_body_fails()
    {
        var ts = Now.ToUnixTimeSeconds().ToString();
        Assert.NotNull(PortalRequestSignature.Verify(ts, Sign(ts, Body), Body + " ", Secret, 300, Now));
    }

    [Fact]
    public void A_replay_outside_the_window_fails_even_with_a_valid_signature()
    {
        var old = (Now.ToUnixTimeSeconds() - 301).ToString();
        Assert.Contains("window", PortalRequestSignature.Verify(old, Sign(old, Body), Body, Secret, 300, Now));
    }

    [Fact]
    public void A_rewritten_timestamp_breaks_the_signature()
    {
        var signedAt = (Now.ToUnixTimeSeconds() - 1000).ToString();
        var rewritten = Now.ToUnixTimeSeconds().ToString();
        Assert.NotNull(PortalRequestSignature.Verify(rewritten, Sign(signedAt, Body), Body, Secret, 300, Now));
    }

    [Theory]
    [InlineData(null, "sha256=ab")]
    [InlineData("1800000000", null)]
    [InlineData("1800000000", "md5=ab")]
    public void Missing_or_malformed_headers_fail(string? ts, string? sig)
        => Assert.NotNull(PortalRequestSignature.Verify(ts, sig, Body, Secret, 300, Now));

    [Fact]
    public void No_secret_configured_refuses_rather_than_accepting()
    {
        var ts = Now.ToUnixTimeSeconds().ToString();
        Assert.NotNull(PortalRequestSignature.Verify(ts, Sign(ts, Body), Body, "", 300, Now));
    }
}
