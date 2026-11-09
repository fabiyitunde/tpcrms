using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Billing;

/// <summary>
/// One billable event: a third-party call, a partner check, or a loan reaching a priced milestone.
/// Rows carry no money. The amount is the rate in force for the item, flow and month at the time
/// the row is read, so a price entered late, or corrected, re-prices history without rewriting it,
/// and a statement freezes amounts only when a period is closed.
/// </summary>
public class BillingUsage : AggregateRoot
{
    public const int DetailMaxLength = 300;

    public string Item { get; private set; } = string.Empty;
    public BillingCategory Category { get; private set; }
    public BillingFlow Flow { get; private set; }
    public Guid? CaseId { get; private set; }
    /// <summary>Application number, case reference or partner request id: what a person would search for.</summary>
    public string? CaseReference { get; private set; }
    /// <summary>Applicant, Director, Guarantor, Company, Signatory, ...</summary>
    public string? SubjectType { get; private set; }
    /// <summary>A non-identifying hint such as the last four digits of a BVN or an RC number.</summary>
    public string? SubjectHint { get; private set; }
    public bool Succeeded { get; private set; }
    /// <summary>The provider's answer in a few words when the call failed; empty when it succeeded.</summary>
    public string? Detail { get; private set; }
    public string Host { get; private set; } = string.Empty;
    public Guid? TriggeredBy { get; private set; }
    public DateTime OccurredAt { get; private set; }
    /// <summary>First day of the month of <see cref="OccurredAt"/>, UTC. The billing period.</summary>
    public DateTime Month { get; private set; }
    public BillingUsageSource Source { get; private set; }
    /// <summary>Idempotency key for derived and reconstructed rows; null for live rows.</summary>
    public string? SourceKey { get; private set; }
    public int? DurationMs { get; private set; }

    private BillingUsage() { }

    public static DateTime MonthOf(DateTime utc) => new(utc.Year, utc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    public static Result<BillingUsage> Create(
        string item, BillingFlow flow, Guid? caseId, string? caseReference, string? subjectType, string? subjectHint,
        bool succeeded, string? detail, string host, Guid? triggeredBy, DateTime occurredAtUtc,
        BillingUsageSource source, string? sourceKey, int? durationMs)
    {
        if (string.IsNullOrWhiteSpace(item) || item.Length > 50)
            return Result.Failure<BillingUsage>("Item is required.");
        if (source != BillingUsageSource.Live && string.IsNullOrWhiteSpace(sourceKey))
            return Result.Failure<BillingUsage>("A derived or reconstructed row needs a source key.");

        occurredAtUtc = occurredAtUtc.Kind == DateTimeKind.Utc ? occurredAtUtc : occurredAtUtc.ToUniversalTime();
        var usage = new BillingUsage
        {
            Item = item.Trim(),
            Category = BillingItems.CategoryOf(item.Trim()),
            Flow = flow,
            CaseId = caseId,
            CaseReference = Truncate(caseReference, 100),
            SubjectType = Truncate(subjectType, 50),
            SubjectHint = Truncate(subjectHint, 30),
            Succeeded = succeeded,
            Detail = succeeded ? null : Truncate(detail, DetailMaxLength),
            Host = Truncate(host, 100) ?? string.Empty,
            TriggeredBy = triggeredBy,
            OccurredAt = occurredAtUtc,
            Month = MonthOf(occurredAtUtc),
            Source = source,
            SourceKey = Truncate(sourceKey, 150),
            DurationMs = durationMs,
        };
        usage.SetAuditInfo((triggeredBy ?? Guid.Empty).ToString(), isNew: true);
        return Result.Success(usage);
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        return value.Length <= max ? value : value[..max];
    }
}
