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

    /// <summary>
    /// Scheme + host CRMS is reachable at from the FAC's browser, e.g. "https://crms.boa.gov.ng".
    /// No path — the routes below are CRMS's own and get appended to it.
    ///
    /// One host setting rather than one full URL per page: the portal asked for an actionUrl beside
    /// actionRequired precisely so it need not hardcode our route shape, and two independently
    /// configured base URLs that must agree on a host will eventually disagree in exactly one
    /// environment.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>Legacy full-path base for the profiling form. Superseded by PublicBaseUrl, still
    /// read as a fallback so an environment configured the old way keeps working until migrated.</summary>
    public string ProfilingBaseUrl { get; set; } = string.Empty;

    /// <summary>CRMS-owned route shapes. They live here, not in the portal's code, so that we stay
    /// free to change them.</summary>
    public const string ProfilingRoute = "/rhshf/profiling";
    public const string OfferRoute = "/rhshf/offer";

    /// <summary>
    /// Absolute URL of the FAC-facing profiling form for a case, without a token — callers append
    /// one. Falls back to the legacy ProfilingBaseUrl, and returns null when neither is configured
    /// so a placeholder host is never handed to the portal as though it were real.
    /// </summary>
    public string? BuildProfilingUrl(string reference) =>
        !string.IsNullOrWhiteSpace(PublicBaseUrl)
            ? $"{PublicBaseUrl.TrimEnd('/')}{ProfilingRoute}/{reference}"
            : !string.IsNullOrWhiteSpace(ProfilingBaseUrl)
                ? $"{ProfilingBaseUrl.TrimEnd('/')}/{reference}"
                : null;

    /// <summary>Absolute URL of the FAC-facing offer acceptance page for a case, without a token.
    /// Null when PublicBaseUrl is unconfigured — there is no legacy setting to fall back to.</summary>
    public string? BuildOfferUrl(string reference) =>
        !string.IsNullOrWhiteSpace(PublicBaseUrl)
            ? $"{PublicBaseUrl.TrimEnd('/')}{OfferRoute}/{reference}"
            : null;

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
