using CRMS.Infrastructure.ExternalServices.Rhshf;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.Tests.Rhshf;

public class RhshfTokenServiceTests
{
    private static RhshfTokenService CreateService(int tokenExpiryMinutes = 20, string secret = "a-sufficiently-long-test-signing-secret-1234567890")
        => new(Options.Create(new RhshfSettings
        {
            TokenSigningSecret = secret,
            TokenExpiryMinutes = tokenExpiryMinutes,
            ProfilingBaseUrl = "https://crms.example.com/rhshf/profiling",
        }));

    [Fact]
    public void IssueThenValidate_RoundTrips_WithMatchingClaims()
    {
        var service = CreateService();
        var facId = Guid.NewGuid();
        var issued = service.IssueToken(Guid.NewGuid(), "RHSHF-2026-000123", facId, "RH-SHF-DRY-2026");

        var validated = service.ValidateToken(issued.Token);

        Assert.NotNull(validated);
        Assert.Equal("RHSHF-2026-000123", validated!.Reference);
        Assert.Equal(facId, validated.FacId);
        Assert.Equal("RH-SHF-DRY-2026", validated.ProgrammeCode);
        Assert.Equal(issued.Jti, validated.Jti);
    }

    [Fact]
    public void ValidateToken_Expired_ReturnsNull()
    {
        var service = CreateService(tokenExpiryMinutes: -5); // already expired at issuance
        var issued = service.IssueToken(Guid.NewGuid(), "RHSHF-2026-000123", Guid.NewGuid(), "RH-SHF-DRY-2026");

        var validated = service.ValidateToken(issued.Token);

        Assert.Null(validated);
    }

    [Fact]
    public void ValidateToken_SignedWithDifferentSecret_ReturnsNull()
    {
        var issuer = CreateService(secret: "secret-one-that-is-long-enough-1234567890");
        var verifier = CreateService(secret: "a-totally-different-secret-1234567890abcd");
        var issued = issuer.IssueToken(Guid.NewGuid(), "RHSHF-2026-000123", Guid.NewGuid(), "RH-SHF-DRY-2026");

        var validated = verifier.ValidateToken(issued.Token);

        Assert.Null(validated);
    }

    [Fact]
    public void ValidateToken_Malformed_ReturnsNull()
    {
        var service = CreateService();

        var validated = service.ValidateToken("not-a-real-jwt");

        Assert.Null(validated);
    }

    [Fact]
    public void IssueToken_EachCall_ProducesUniqueJti()
    {
        var service = CreateService();

        var first = service.IssueToken(Guid.NewGuid(), "RHSHF-2026-000123", Guid.NewGuid(), "RH-SHF-DRY-2026");
        var second = service.IssueToken(Guid.NewGuid(), "RHSHF-2026-000123", Guid.NewGuid(), "RH-SHF-DRY-2026");

        Assert.NotEqual(first.Jti, second.Jti);
    }
}
