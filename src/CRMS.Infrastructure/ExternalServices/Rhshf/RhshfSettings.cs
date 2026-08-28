namespace CRMS.Infrastructure.ExternalServices.Rhshf;

/// <summary>
/// Configuration for the RH-SHF integration. Own config section — never shares Namp's.
/// </summary>
public class RhshfSettings
{
    public const string SectionName = "Rhshf";

    /// <summary>Inbound auth for the portal's calls to CRMS (§4.1/§4.5/§4.6) — X-Api-Key header.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>HMAC secret for signing the §4.2 reference token. Independent of staff JwtSettings.</summary>
    public string TokenSigningSecret { get; set; } = string.Empty;

    public int TokenExpiryMinutes { get; set; } = 20;

    /// <summary>Base URL the FAC opens the profiling form at; token is appended as a query string.</summary>
    public string ProfilingBaseUrl { get; set; } = string.Empty;

    /// <summary>Single flat committee for v1 (design doc §6 #11) — no value-based tiers like NAMP's
    /// Branch/Zonal/Regional/HO ladder. Quorum/majority thresholds for every RH-SHF committee vote.</summary>
    public int CommitteeRequiredVotes { get; set; } = 3;
    public int CommitteeMinimumApprovalVotes { get; set; } = 2;
}
