using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CRMS.Application.Rhshf.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CRMS.Infrastructure.ExternalServices.Rhshf;

/// <summary>
/// Issues and validates the §4.2 reference token. Uses the same JWT library/approach as
/// staff-login's TokenService, but its own signing secret and its own claim set — not a fork of
/// NAMP's or staff-login's token code, per the RH-SHF isolation rule.
/// </summary>
public class RhshfTokenService : IRhshfTokenService
{
    private const string Issuer = "crms-rhshf";
    private const string Audience = "rhshf-profiling";

    private readonly RhshfSettings _settings;

    public RhshfTokenService(IOptions<RhshfSettings> settings)
    {
        _settings = settings.Value;
    }

    public RhshfIssuedTokenResult IssueToken(Guid rhshfCreditProfileId, string reference, Guid facId, string programmeCode)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.TokenExpiryMinutes);
        var jti = Guid.NewGuid().ToString();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.TokenSigningSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, reference),
            new("facId", facId.ToString()),
            new("programme", programmeCode),
            new(JwtRegisteredClaimNames.Jti, jti),
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        var profilingUrl = $"{_settings.ProfilingBaseUrl.TrimEnd('/')}/{reference}?token={tokenString}";

        return new RhshfIssuedTokenResult(tokenString, jti, expiresAt, profilingUrl);
    }

    public RhshfTokenValidationResult? ValidateToken(string token)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.TokenSigningSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(token, parameters, out _);
        }
        catch
        {
            // Expired, tampered, wrong signer, malformed — all treated the same: not a valid token.
            return null;
        }

        var reference = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var facIdClaim = principal.FindFirst("facId")?.Value;
        var programmeCode = principal.FindFirst("programme")?.Value;
        var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

        if (string.IsNullOrEmpty(reference) || string.IsNullOrEmpty(jti)
            || !Guid.TryParse(facIdClaim, out var facId) || string.IsNullOrEmpty(programmeCode))
            return null;

        return new RhshfTokenValidationResult(reference, facId, programmeCode, jti);
    }
}
