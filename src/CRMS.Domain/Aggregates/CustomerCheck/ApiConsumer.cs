using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.CustomerCheck;

/// <summary>
/// A third-party system allowed to call the customer-credit-check API on the bank's behalf. Each consumer
/// has its own key (stored only as a SHA-256 hash of a high-entropy random value), its own
/// entitlements and limits, and its own audit trail. Keys are shown once, at creation or rotation.
/// </summary>
public class ApiConsumer : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    /// <summary>The partner's own mailbox for credentials: setup links go here and nowhere else.
    /// Required for a consumer set up from now on; older ones have none until they are edited.</summary>
    public string? ContactEmail { get; private set; }
    /// <summary>SHA-256 of the key, hex. The key itself is never stored.</summary>
    public string KeyHash { get; private set; } = string.Empty;
    /// <summary>First characters of the key, so an administrator can tell keys apart.</summary>
    public string KeyPrefix { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    /// <summary>Comma-separated section names the consumer may ask for. See CustomerCheckSections.</summary>
    public string AllowedSections { get; private set; } = string.Empty;
    public int PerMinuteLimit { get; private set; } = 30;
    /// <summary>When on, a bureau report for the same BVN newer than the age below is returned
    /// from the stored copy instead of billing a new call.</summary>
    public bool ReuseBureauReports { get; private set; }
    public int BureauReuseMaxAgeDays { get; private set; } = 30;
    public DateTime? LastUsedAt { get; private set; }
    public DateTime? KeyRotatedAt { get; private set; }

    /// <summary>Where results are posted when a request names no callbackUrl of its own.</summary>
    public string? CallbackUrl { get; private set; }
    /// <summary>Comma-separated hosts a per-request callbackUrl may point to. The registered
    /// callback URL's host is always allowed.</summary>
    public string AllowedCallbackHosts { get; private set; } = string.Empty;
    /// <summary>The consumer's own HMAC secret for callbacks, encrypted at rest. Shown once.</summary>
    public string? CallbackSecretCiphertext { get; private set; }
    public string? CallbackSecretPrefix { get; private set; }
    public DateTime? CallbackSecretRotatedAt { get; private set; }
    /// <summary>Checks the consumer may have queued or running at once.</summary>
    public int MaxQueuedJobs { get; private set; } = 100;

    private ApiConsumer() { }

    public static Result<ApiConsumer> Create(
        string name, string keyHash, string keyPrefix, IEnumerable<string> allowedSections,
        int perMinuteLimit, bool reuseBureauReports, int bureauReuseMaxAgeDays, string? notes, Guid createdBy)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<ApiConsumer>("Name is required.");
        if (string.IsNullOrWhiteSpace(keyHash) || string.IsNullOrWhiteSpace(keyPrefix))
            return Result.Failure<ApiConsumer>("A key is required.");
        var limitsError = ValidateLimits(perMinuteLimit, bureauReuseMaxAgeDays);
        if (limitsError is not null)
            return Result.Failure<ApiConsumer>(limitsError);

        var consumer = new ApiConsumer
        {
            Name = name.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            AllowedSections = JoinSections(allowedSections),
            PerMinuteLimit = perMinuteLimit,
            ReuseBureauReports = reuseBureauReports,
            BureauReuseMaxAgeDays = bureauReuseMaxAgeDays,
        };
        consumer.SetAuditInfo(createdBy.ToString(), isNew: true);
        return Result.Success(consumer);
    }

    public Result Update(
        string name, IEnumerable<string> allowedSections, int perMinuteLimit,
        bool reuseBureauReports, int bureauReuseMaxAgeDays, string? notes, Guid modifiedBy)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure("Name is required.");
        var limitsError = ValidateLimits(perMinuteLimit, bureauReuseMaxAgeDays);
        if (limitsError is not null)
            return Result.Failure(limitsError);

        Name = name.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        AllowedSections = JoinSections(allowedSections);
        PerMinuteLimit = perMinuteLimit;
        ReuseBureauReports = reuseBureauReports;
        BureauReuseMaxAgeDays = bureauReuseMaxAgeDays;
        SetAuditInfo(modifiedBy.ToString());
        return Result.Success();
    }

    /// <summary>Sets where setup links are sent. One address, not a list: a link is for one reader.</summary>
    public Result SetContactEmail(string? email, Guid modifiedBy)
    {
        var value = (email ?? string.Empty).Trim();
        if (value.Length == 0)
            return Result.Failure("A contact email is required.");
        if (value.Length > 256)
            return Result.Failure("Contact email must be 256 characters or fewer.");
        if (!LooksLikeEmail(value))
            return Result.Failure("Contact email is not a valid email address.");

        ContactEmail = value;
        SetAuditInfo(modifiedBy.ToString());
        return Result.Success();
    }

    private static bool LooksLikeEmail(string value)
    {
        if (value.Any(c => char.IsWhiteSpace(c) || c is ',' or ';' or '<' or '>')) return false;
        var at = value.IndexOf('@');
        if (at <= 0 || at != value.LastIndexOf('@')) return false;
        var domain = value[(at + 1)..];
        return domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.')
            && Uri.CheckHostName(domain) == UriHostNameType.Dns;
    }

    public void SetActive(bool active, Guid modifiedBy)
    {
        IsActive = active;
        SetAuditInfo(modifiedBy.ToString());
    }

    /// <summary>Replaces the key. The old key stops working at once.</summary>
    public void RotateKey(string keyHash, string keyPrefix, Guid modifiedBy)
    {
        KeyHash = keyHash;
        KeyPrefix = keyPrefix;
        KeyRotatedAt = DateTime.UtcNow;
        SetAuditInfo(modifiedBy.ToString());
    }

    /// <summary>Callback registration. The URL is optional; hosts are optional; both are validated
    /// here so a bad entry never reaches a request.</summary>
    public Result SetCallback(string? callbackUrl, IEnumerable<string>? allowedHosts, int maxQueuedJobs, Guid modifiedBy)
    {
        if (maxQueuedJobs is < 1 or > 10_000)
            return Result.Failure("Maximum queued checks must be between 1 and 10,000.");

        string? url = null;
        if (!string.IsNullOrWhiteSpace(callbackUrl))
        {
            if (!Uri.TryCreate(callbackUrl.Trim(), UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                return Result.Failure("Callback URL must be an absolute http or https URL.");
            if (callbackUrl.Trim().Length > 500)
                return Result.Failure("Callback URL must be 500 characters or fewer.");
            url = uri.ToString();
        }

        var hosts = new List<string>();
        foreach (var h in allowedHosts ?? [])
        {
            var host = h.Trim().TrimEnd('.').ToLowerInvariant();
            if (host.Length == 0) continue;
            if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
                return Result.Failure($"'{h.Trim()}' is not a valid host name.");
            if (!hosts.Contains(host)) hosts.Add(host);
        }
        var joined = string.Join(",", hosts);
        if (joined.Length > 1000)
            return Result.Failure("Too many allowed callback hosts.");

        CallbackUrl = url;
        AllowedCallbackHosts = joined;
        MaxQueuedJobs = maxQueuedJobs;
        SetAuditInfo(modifiedBy.ToString());
        return Result.Success();
    }

    /// <summary>Replaces the callback signing secret. Callbacks signed from now on use the new one.</summary>
    public void SetCallbackSecret(string secretCiphertext, string secretPrefix, Guid modifiedBy)
    {
        CallbackSecretCiphertext = secretCiphertext;
        CallbackSecretPrefix = secretPrefix;
        CallbackSecretRotatedAt = DateTime.UtcNow;
        SetAuditInfo(modifiedBy.ToString());
    }

    public IReadOnlyList<string> AllowedCallbackHostList =>
        AllowedCallbackHosts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>True when a per-request callback may point at this host: the registered URL's own
    /// host, or one on the allow-list.</summary>
    public bool AllowsCallbackHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;
        if (CallbackUrl is not null && Uri.TryCreate(CallbackUrl, UriKind.Absolute, out var own)
            && string.Equals(own.Host, host, StringComparison.OrdinalIgnoreCase))
            return true;
        return AllowedCallbackHostList.Any(h => string.Equals(h, host, StringComparison.OrdinalIgnoreCase));
    }

    public void Touch() => LastUsedAt = DateTime.UtcNow;

    public IReadOnlyList<string> AllowedSectionList =>
        AllowedSections.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public bool Allows(string section) =>
        AllowedSectionList.Contains(section, StringComparer.OrdinalIgnoreCase);

    private static string JoinSections(IEnumerable<string> sections) =>
        string.Join(",", sections.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));

    private static string? ValidateLimits(int perMinuteLimit, int reuseDays) =>
        perMinuteLimit is < 1 or > 10_000 ? "Per-minute limit must be between 1 and 10,000."
        : reuseDays is < 1 or > 365 ? "Bureau reuse age must be between 1 and 365 days."
        : null;
}
