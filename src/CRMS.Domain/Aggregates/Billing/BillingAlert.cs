using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Billing;

public enum BillingAlertKind
{
    /// <summary>A service's calls this month run far above its recent monthly norm.</summary>
    SpendSpike,
    /// <summary>Most of a service's recent calls failed, and every one of them is billed.</summary>
    HighFailureRate,
    /// <summary>Calls this month that no case claimed.</summary>
    UnattributedCalls,
    /// <summary>An item with calls this month and no price, so billed at zero.</summary>
    UnpricedActivity,
    /// <summary>An issued statement past its due date and not marked paid.</summary>
    OverdueStatement,
    /// <summary>The previous month has ended with unbilled rows and no statement.</summary>
    MonthNotClosed,
}

public enum BillingAlertSeverity
{
    Info,
    Warning,
    Critical,
}

public enum BillingAlertStatus
{
    Open,
    Snoozed,
    Dismissed,
    /// <summary>The check no longer finds the condition.</summary>
    Resolved,
}

/// <summary>
/// Something the billing checks found that a Billing Manager should know about. One row per
/// condition, identified by <see cref="Key"/>: a condition that persists updates its alert rather
/// than adding another, and when the check stops finding it the alert resolves itself. Dismissing
/// silences a condition until it resolves; snoozing silences it for a while.
/// </summary>
public class BillingAlert : AggregateRoot
{
    public BillingAlertKind Kind { get; private set; }
    public BillingAlertSeverity Severity { get; private set; }
    public BillingAlertStatus Status { get; private set; }
    /// <summary>Stable identity of the condition, e.g. "spike:bureau:crc-full:2026-11".</summary>
    public string Key { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Detail { get; private set; } = string.Empty;
    /// <summary>Relative intranet address of the page that shows the figures behind the alert.</summary>
    public string? Link { get; private set; }
    public DateTime FirstSeenAt { get; private set; }
    public DateTime LastSeenAt { get; private set; }
    public int TimesSeen { get; private set; }
    public DateTime? SnoozedUntil { get; private set; }
    public Guid? ActedBy { get; private set; }
    public DateTime? ActedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public DateTime? NotifiedAt { get; private set; }

    private BillingAlert() { }

    public static Result<BillingAlert> Create(BillingAlertKind kind, BillingAlertSeverity severity, string key, string title, string detail, string? link)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 150)
            return Result.Failure<BillingAlert>("An alert needs a key.");
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure<BillingAlert>("An alert needs a title.");
        var now = DateTime.UtcNow;
        var alert = new BillingAlert
        {
            Kind = kind,
            Severity = severity,
            Status = BillingAlertStatus.Open,
            Key = key.Trim(),
            Title = Clip(title, 200),
            Detail = Clip(detail, 1000),
            Link = ClipOrNull(link, 300),
            FirstSeenAt = now,
            LastSeenAt = now,
            TimesSeen = 1,
        };
        alert.SetAuditInfo(Guid.Empty.ToString(), isNew: true);
        return Result.Success(alert);
    }

    public bool IsActive => Status != BillingAlertStatus.Resolved;

    /// <summary>The check found the condition again. A snooze that has run out reopens it.</summary>
    public void SeenAgain(BillingAlertSeverity severity, string title, string detail)
    {
        Severity = severity;
        Title = Clip(title, 200);
        Detail = Clip(detail, 1000);
        LastSeenAt = DateTime.UtcNow;
        TimesSeen++;
        if (Status == BillingAlertStatus.Snoozed && SnoozedUntil is { } until && until <= DateTime.UtcNow)
        {
            Status = BillingAlertStatus.Open;
            SnoozedUntil = null;
        }
    }

    public void Resolve()
    {
        Status = BillingAlertStatus.Resolved;
        ResolvedAt = DateTime.UtcNow;
        SnoozedUntil = null;
    }

    public Result Snooze(DateTime untilUtc, Guid by)
    {
        if (!IsActive) return Result.Failure("This alert has already resolved.");
        if (untilUtc <= DateTime.UtcNow) return Result.Failure("Snooze until a time in the future.");
        Status = BillingAlertStatus.Snoozed;
        SnoozedUntil = untilUtc;
        ActedBy = by;
        ActedAt = DateTime.UtcNow;
        SetAuditInfo(by.ToString());
        return Result.Success();
    }

    public Result Dismiss(Guid by)
    {
        if (!IsActive) return Result.Failure("This alert has already resolved.");
        Status = BillingAlertStatus.Dismissed;
        SnoozedUntil = null;
        ActedBy = by;
        ActedAt = DateTime.UtcNow;
        SetAuditInfo(by.ToString());
        return Result.Success();
    }

    public Result Reopen(Guid by)
    {
        if (!IsActive) return Result.Failure("This alert has already resolved.");
        Status = BillingAlertStatus.Open;
        SnoozedUntil = null;
        ActedBy = by;
        ActedAt = DateTime.UtcNow;
        SetAuditInfo(by.ToString());
        return Result.Success();
    }

    public void MarkNotified() => NotifiedAt = DateTime.UtcNow;

    private static string Clip(string value, int max) => value.Length <= max ? value.Trim() : value[..max].Trim();
    private static string? ClipOrNull(string? value, int max) => value is null ? null : Clip(value, max);
}
