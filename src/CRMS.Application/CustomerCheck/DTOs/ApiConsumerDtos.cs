namespace CRMS.Application.CustomerCheck.DTOs;

public record ApiConsumerDto(
    Guid Id,
    string Name,
    string? Notes,
    string KeyPrefix,
    bool IsActive,
    IReadOnlyList<string> AllowedSections,
    int PerMinuteLimit,
    bool ReuseBureauReports,
    int BureauReuseMaxAgeDays,
    DateTime? LastUsedAt,
    DateTime? KeyRotatedAt,
    DateTime CreatedAt,
    string? CallbackUrl,
    IReadOnlyList<string> AllowedCallbackHosts,
    int MaxQueuedJobs,
    string? CallbackSecretPrefix,
    DateTime? CallbackSecretRotatedAt,
    string? ContactEmail = null,
    ApiConsumerSetupLinkDto? LatestSetupLink = null);

/// <summary>The consumer plus whichever secrets were just issued. Each is returned once, here, and
/// never again: the key on creation and key rotation, the callback signing secret on creation and
/// secret rotation.</summary>
public record ApiConsumerKeyIssuedDto(ApiConsumerDto Consumer, string? Key, string? CallbackSecret = null);

/// <summary>One setup link as an administrator sees it: who sent it where, and what became of it.
/// <c>State</c> is PENDING, OPENED, EXPIRED or REPLACED.</summary>
public record ApiConsumerSetupLinkDto(
    Guid Id,
    string SentTo,
    bool IssuesKey,
    bool IssuesSecret,
    string SentByName,
    DateTime SentAt,
    DateTime ExpiresAt,
    DateTime? OpenedAt,
    string? OpenedFromIp,
    string State);

/// <summary>What the setup page may say about a link before it is opened. <c>State</c> is one of
/// the link states, or INVALID when the token is not recognised.</summary>
public record ApiConsumerSetupLinkView(string State, string ConsumerName, bool IssuesKey, bool IssuesSecret, DateTime? ExpiresAt, DateTime? OpenedAt)
{
    public const string InvalidState = "INVALID";
    public static readonly ApiConsumerSetupLinkView Invalid = new(InvalidState, string.Empty, false, false, null, null);
}

/// <summary>The credentials a setup link issued. Returned once, to the page that opened it.</summary>
public record ApiConsumerSetupRedeemedDto(string ConsumerName, string? Key, string? CallbackSecret);
