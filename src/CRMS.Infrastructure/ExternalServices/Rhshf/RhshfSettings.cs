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

    /// <summary>HMAC secret for signing the §4.4 outcome webhook. Independent of TokenSigningSecret
    /// above — a different purpose, exchanged with the portal out-of-band, same as the brief's own
    /// security summary treats them as separate concerns.</summary>
    public string CallbackSigningSecret { get; set; } = string.Empty;

    public int CallbackTimeoutSeconds { get; set; } = 30;

    /// <summary>True in local/dev environments without a reachable portal callbackUrl — logs
    /// instead of making a real HTTP call, mirroring every other external-service mock in this
    /// codebase (Fineract, CoreBanking, NAMP's own callback).</summary>
    public bool CallbackUseMock { get; set; } = true;
}
