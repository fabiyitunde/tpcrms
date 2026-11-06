using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.CustomerCheck;

/// <summary>
/// A one-time link emailed to a consumer's contact. Opening it issues the consumer a new key, a new
/// callback signing secret, or both, and shows them once, so neither ever sits in a mailbox or
/// passes through a member of staff. Only the SHA-256 of the link's token is stored. A link is
/// good until it is opened, replaced by a later one, or expires, whichever comes first; the row
/// stays as the record of who sent what to whom and when it was opened.
/// </summary>
public class ApiConsumerSetupLink : Entity
{
    public Guid ConsumerId { get; private set; }
    /// <summary>SHA-256 of the token, hex. The token itself is only ever in the email.</summary>
    public string TokenHash { get; private set; } = string.Empty;
    /// <summary>The address the link went to, as it stood when the link was sent.</summary>
    public string SentTo { get; private set; } = string.Empty;
    public bool IssuesKey { get; private set; }
    public bool IssuesSecret { get; private set; }
    public string SentByName { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? OpenedAt { get; private set; }
    public string? OpenedFromIp { get; private set; }
    /// <summary>Set when a later link, or switching the consumer off, made this one void.</summary>
    public DateTime? RevokedAt { get; private set; }

    private ApiConsumerSetupLink() { }

    public static Result<ApiConsumerSetupLink> Issue(
        Guid consumerId, string tokenHash, string sentTo, bool issuesKey, bool issuesSecret,
        Guid sentBy, string sentByName, DateTime now, TimeSpan lifetime)
    {
        if (consumerId == Guid.Empty)
            return Result.Failure<ApiConsumerSetupLink>("A consumer is required.");
        if (string.IsNullOrWhiteSpace(tokenHash))
            return Result.Failure<ApiConsumerSetupLink>("A token is required.");
        if (string.IsNullOrWhiteSpace(sentTo))
            return Result.Failure<ApiConsumerSetupLink>("The consumer has no contact email.");
        if (!issuesKey && !issuesSecret)
            return Result.Failure<ApiConsumerSetupLink>("Choose what the link issues: the key, the signing secret, or both.");
        if (lifetime <= TimeSpan.Zero)
            return Result.Failure<ApiConsumerSetupLink>("The link must be valid for some time.");

        var link = new ApiConsumerSetupLink
        {
            ConsumerId = consumerId,
            TokenHash = tokenHash,
            SentTo = sentTo.Trim(),
            IssuesKey = issuesKey,
            IssuesSecret = issuesSecret,
            SentByName = (sentByName ?? string.Empty).Trim(),
            ExpiresAt = now.Add(lifetime),
        };
        link.SetAuditInfo(sentBy.ToString(), isNew: true);
        link.CreatedAt = now;
        return Result.Success(link);
    }

    public string State(DateTime now) =>
        OpenedAt is not null ? Opened
        : RevokedAt is not null ? Replaced
        : ExpiresAt <= now ? Expired
        : Pending;

    public bool CanBeOpened(DateTime now) => State(now) == Pending;

    public void Revoke(DateTime now)
    {
        if (OpenedAt is null && RevokedAt is null)
            RevokedAt = now;
    }

    public const string Pending = "PENDING";
    public const string Opened = "OPENED";
    public const string Expired = "EXPIRED";
    public const string Replaced = "REPLACED";
}
