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
    DateTime? CallbackSecretRotatedAt);

/// <summary>The consumer plus whichever secrets were just issued. Each is returned once, here, and
/// never again: the key on creation and key rotation, the callback signing secret on creation and
/// secret rotation.</summary>
public record ApiConsumerKeyIssuedDto(ApiConsumerDto Consumer, string? Key, string? CallbackSecret = null);
