using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.CustomerCheck.Reporting;

// The partner-API dashboard (Admin > Partner API): what consumers used, what it cost them, how
// the checks and callbacks fared. Every figure comes from the job table and the audit log, so it
// reconciles with what a consumer is invoiced.

public interface ICustomerCheckReporting
{
    Task<CustomerCheckOverviewDto> GetOverviewAsync(DateTime fromUtc, DateTime toUtc, Guid? consumerId, CancellationToken ct = default);
    /// <summary>One row per job in the period for one consumer, priced, for the statement export.</summary>
    Task<CustomerCheckStatementDto> GetStatementAsync(Guid consumerId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<(List<CustomerCheckJobSummaryDto> Items, int Total)> SearchJobsAsync(CustomerCheckJobFilter filter, CancellationToken ct = default);
    Task<CustomerCheckJobDetailDto?> GetJobDetailAsync(Guid jobId, CancellationToken ct = default);
}

public record CustomerCheckOverviewDto(
    DateTime From,
    DateTime To,
    Guid? ConsumerId,
    string Currency,
    OverviewTotals Totals,
    OverviewTotals Previous,
    IReadOnlyList<DailyPoint> Daily,
    IReadOnlyList<string> ConsumerNames,
    IReadOnlyList<ConsumerRow> Consumers,
    IReadOnlyList<NamedCount> SectionOutcomes,
    IReadOnlyList<SectionRow> Sections,
    IReadOnlyList<NamedCount> BureauUsage,
    IReadOnlyList<NamedCount> AdvisoryRatings,
    IReadOnlyList<NamedCount> Recommendations,
    CallbackStats Callbacks,
    /// <summary>Submissions by weekday (0 = Monday) and hour, local to the server.</summary>
    int[][] Heatmap,
    IReadOnlyList<RecentFailure> RecentFailures);

public record OverviewTotals(
    int Submitted,
    int Completed,
    int Failed,
    int Open,
    decimal CompletionRate,
    int BilledCalls,
    BilledCallCounts CallsByKind,
    decimal Amount,
    int MedianDurationMs,
    int AverageDurationMs,
    int AdvisoriesScored,
    decimal? AverageAdvisoryScore,
    int BureauBilled,
    int BureauReused,
    decimal CallbackDeliveryRate);

public record DailyPoint(DateOnly Date, int Submitted, int Completed, int Failed, int BilledCalls, decimal Amount, IReadOnlyDictionary<string, int> ByConsumer);

public record ConsumerRow(
    Guid ConsumerId,
    string Name,
    int Checks,
    int Completed,
    int Failed,
    int BilledCalls,
    decimal Amount,
    int AverageDurationMs,
    int CallbacksFailed,
    decimal? AverageAdvisoryScore,
    DateTime? LastCheckAt);

public record NamedCount(string Name, int Count);

/// <summary>How one section fared across the period's completed checks.</summary>
public record SectionRow(string Section, int Requested, int Ok, int NoRecord, int Unavailable, int NotAllowed);

public record CallbackStats(int Requested, int Delivered, int DeliveredFirstAttempt, int Failed, int Pending);

public record RecentFailure(string JobId, string Consumer, string RequestId, DateTime At, string? ErrorCode, string? ErrorMessage);

public record CustomerCheckStatementDto(
    Guid ConsumerId,
    string ConsumerName,
    DateTime From,
    DateTime To,
    string Currency,
    IReadOnlyList<StatementLine> Lines,
    decimal Total,
    IReadOnlyList<StatementItemTotal> ByItem);

public record StatementLine(
    DateTime QueuedAt,
    string JobId,
    string RequestId,
    string Status,
    string Sections,
    string? SectionOutcomes,
    BilledCallCounts Calls,
    decimal Amount);

public record StatementItemTotal(string Item, string DisplayName, int Quantity, decimal UnitPrice, decimal Amount);

public record CustomerCheckJobSummaryDto(
    Guid Id,
    string JobId,
    Guid ConsumerId,
    string ConsumerName,
    string RequestId,
    string Status,
    string BvnLast4,
    IReadOnlyList<string> Sections,
    DateTime QueuedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int DurationMs,
    int BilledCalls,
    string? BureauUsed,
    bool BureauReused,
    string? ErrorCode,
    string? ErrorMessage,
    string CallbackStatus,
    int CallbackAttempts,
    DateTime? CallbackDeliveredAt,
    int? LastCallbackStatusCode,
    DateTime? NextCallbackAt,
    bool HasCallbackUrl,
    decimal? AdvisoryScore,
    string? AdvisoryRating,
    string? AdvisoryRecommendation,
    int RunAttempts);

public record CustomerCheckJobDetailDto(
    CustomerCheckJobSummaryDto Summary,
    string? SectionOutcomes,
    string? CallbackHost,
    string? LastCallbackError,
    string EventId,
    /// <summary>The reply as it was stored and sent, decrypted for staff; null until COMPLETED or
    /// when it cannot be read.</summary>
    string? ResultJson,
    string? ResultProblem);

public record CustomerCheckPriceDto(string Item, string DisplayName, decimal UnitPrice, string Currency, DateTime? ModifiedAt, string? ModifiedBy);

/// <summary>What one completed check costs under the current price list.</summary>
public static class CustomerCheckBilling
{
    public static readonly string[] ChargeableStatuses = [SectionStatuses.Ok, SectionStatuses.NoRecord];

    public static decimal Amount(IReadOnlyDictionary<string, decimal> prices, string status, string? sectionOutcomes, BilledCallCounts calls)
    {
        if (status != CustomerCheckJob.Completed) return 0m;
        var amount = Price(prices, CustomerCheckPriceItems.Check);
        foreach (var (section, outcome) in ParseOutcomes(sectionOutcomes))
            if (ChargeableStatuses.Contains(outcome))
                amount += Price(prices, CustomerCheckPriceItems.Section(section));
        amount += calls.Identity * Price(prices, CustomerCheckPriceItems.CallIdentity);
        amount += calls.Bureau * Price(prices, CustomerCheckPriceItems.CallBureau);
        amount += calls.Score * Price(prices, CustomerCheckPriceItems.CallScore);
        amount += calls.Fraud * Price(prices, CustomerCheckPriceItems.CallFraud);
        amount += calls.Advisory * Price(prices, CustomerCheckPriceItems.CallAdvisory);
        return Math.Round(amount, 2);
    }

    /// <summary>Quantity of each billable item in one completed check.</summary>
    public static IEnumerable<(string Item, int Quantity)> Quantities(string status, string? sectionOutcomes, BilledCallCounts calls)
    {
        if (status != CustomerCheckJob.Completed) yield break;
        yield return (CustomerCheckPriceItems.Check, 1);
        foreach (var (section, outcome) in ParseOutcomes(sectionOutcomes))
            if (ChargeableStatuses.Contains(outcome))
                yield return (CustomerCheckPriceItems.Section(section), 1);
        if (calls.Identity > 0) yield return (CustomerCheckPriceItems.CallIdentity, calls.Identity);
        if (calls.Bureau > 0) yield return (CustomerCheckPriceItems.CallBureau, calls.Bureau);
        if (calls.Score > 0) yield return (CustomerCheckPriceItems.CallScore, calls.Score);
        if (calls.Fraud > 0) yield return (CustomerCheckPriceItems.CallFraud, calls.Fraud);
        if (calls.Advisory > 0) yield return (CustomerCheckPriceItems.CallAdvisory, calls.Advisory);
    }

    public static IEnumerable<(string Section, string Outcome)> ParseOutcomes(string? sectionOutcomes)
    {
        foreach (var pair in (sectionOutcomes ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq > 0) yield return (pair[..eq], pair[(eq + 1)..]);
        }
    }

    private static decimal Price(IReadOnlyDictionary<string, decimal> prices, string item) =>
        prices.TryGetValue(item, out var p) ? p : 0m;
}
