using CRMS.Domain.Aggregates.Billing;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Billing.Alerts;

/// <summary>One condition a check found, before it becomes or updates an alert.</summary>
public sealed record BillingAlertFinding(BillingAlertKind Kind, BillingAlertSeverity Severity, string Key, string Title, string Detail, string? Link);

/// <summary>
/// The six billing checks. Pure functions of what the repositories report, so each is testable on
/// its own and the evaluator only has to reconcile findings with the alerts already recorded.
/// </summary>
public sealed class BillingAlertChecks
{
    private readonly IBillingUsageRepository _usage;
    private readonly IBillingRateRepository _rates;
    private readonly IBillingStatementRepository _statements;
    private readonly BillingSettings _settings;
    private readonly Func<DateTime> _clock;

    public BillingAlertChecks(IBillingUsageRepository usage, IBillingRateRepository rates, IBillingStatementRepository statements, BillingSettings settings, Func<DateTime>? clock = null)
    {
        _usage = usage;
        _rates = rates;
        _statements = statements;
        _settings = settings;
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    private BillingAlertSettings A => _settings.Alerts;

    public async Task<IReadOnlyList<BillingAlertFinding>> RunAsync(CancellationToken ct = default)
    {
        var now = _clock();
        var month = BillingUsage.MonthOf(now);
        var findings = new List<BillingAlertFinding>();

        var history = Math.Max(1, A.HistoryMonths);
        var counts = await _usage.CountsAsync(month.AddMonths(-history), month, null, ct);
        var thisMonth = counts.Where(c => c.Month == month).ToList();
        var book = new BillingPriceBook(await _rates.GetAllAsync(ct));

        findings.AddRange(SpendSpikes(now, month, counts, thisMonth, history));
        findings.AddRange(await FailureRatesAsync(now, ct));
        findings.AddRange(Unattributed(month, thisMonth));
        findings.AddRange(Unpriced(month, thisMonth, book));
        findings.AddRange(await OverdueStatementsAsync(now, ct));
        findings.AddRange(await MonthNotClosedAsync(now, month, ct));
        return findings;
    }

    private IEnumerable<BillingAlertFinding> SpendSpikes(DateTime now, DateTime month, IReadOnlyList<BillingUsageCount> counts, List<BillingUsageCount> thisMonth, int history)
    {
        var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
        var elapsed = Math.Max(1m, (decimal)(now - month).TotalDays);
        foreach (var g in thisMonth.Where(c => BillingItems.CategoryOf(c.Item) != BillingCategory.Platform).GroupBy(c => c.Item))
        {
            var sofar = g.Sum(c => c.Count);
            if (sofar < A.SpikeMinCalls) continue;
            var earlier = counts.Where(c => c.Item == g.Key && c.Month < month).Sum(c => c.Count);
            var norm = (decimal)earlier / history;
            if (norm <= 0) continue;                                  // no history, nothing to compare with
            var projected = sofar * daysInMonth / elapsed;
            var ratio = projected / norm;
            if (ratio < A.SpikeFactor) continue;
            var severity = ratio >= A.SpikeFactor * 2 ? BillingAlertSeverity.Critical : BillingAlertSeverity.Warning;
            yield return new BillingAlertFinding(BillingAlertKind.SpendSpike, severity,
                $"spike:{g.Key}:{month:yyyy-MM}",
                $"{BillingItems.DisplayNameOf(g.Key)} is running {ratio:0.#}x its usual month",
                $"{sofar:N0} calls so far in {month:MMMM}, on course for about {projected:N0} against a norm of {norm:N0} a month over the last {history} month(s). Check for a retry loop or a flow calling it more than it should.",
                "/billing");
        }
    }

    private async Task<IEnumerable<BillingAlertFinding>> FailureRatesAsync(DateTime now, CancellationToken ct)
    {
        var since = now.AddDays(-Math.Max(1, A.FailureWindowDays));
        var recent = await _usage.RecentCountsAsync(since, ct);
        var result = new List<BillingAlertFinding>();
        foreach (var g in recent.GroupBy(c => c.Item))
        {
            var total = g.Sum(c => c.Count);
            var failed = g.Where(c => !c.Succeeded).Sum(c => c.Count);
            if (total < A.FailureMinCalls) continue;
            var pct = 100 * failed / total;
            if (pct < A.FailureRatePercent) continue;
            result.Add(new BillingAlertFinding(BillingAlertKind.HighFailureRate, BillingAlertSeverity.Critical,
                $"failures:{g.Key}",
                $"{BillingItems.DisplayNameOf(g.Key)}: {pct}% of recent calls failed",
                $"{failed:N0} of {total:N0} calls in the last {A.FailureWindowDays} day(s) failed. Each failed call is still billed to the bank, so the bank is paying for nothing. See Admin > Diagnostics for the provider's answer.",
                "/admin/diagnostics"));
        }
        return result;
    }

    private IEnumerable<BillingAlertFinding> Unattributed(DateTime month, List<BillingUsageCount> thisMonth)
    {
        var n = thisMonth.Where(c => c.Flow == BillingFlow.Unattributed).Sum(c => c.Count);
        if (n < Math.Max(1, A.UnattributedMinCalls)) yield break;
        yield return new BillingAlertFinding(BillingAlertKind.UnattributedCalls, BillingAlertSeverity.Warning,
            $"unattributed:{month:yyyy-MM}",
            $"{n:N0} call(s) this month belong to no case",
            "They are billed, but nobody can say which application they were for. A new call site was probably added without a billing tag. The Charges list on the dashboard shows them under the Unattributed flow.",
            "/billing");
    }

    private IEnumerable<BillingAlertFinding> Unpriced(DateTime month, List<BillingUsageCount> thisMonth, BillingPriceBook book)
    {
        foreach (var g in thisMonth.GroupBy(c => (c.Item, Flow: BillingItems.Find(c.Item)?.PerFlow == true ? c.Flow : (BillingFlow?)null)))
        {
            var rate = book.Resolve(g.Key.Item, g.Key.Flow ?? g.First().Flow, month);
            if (rate.IsSet) continue;
            var n = g.Sum(c => c.Count);
            var name = BillingItems.DisplayNameOf(g.Key.Item) + (g.Key.Flow is { } f ? $" ({BillingItems.FlowDisplayName(f)})" : "");
            yield return new BillingAlertFinding(BillingAlertKind.UnpricedActivity, BillingAlertSeverity.Warning,
                $"unpriced:{g.Key.Item}:{g.Key.Flow?.ToString() ?? "all"}:{month:yyyy-MM}",
                $"{name} has no price",
                $"{n:N0} event(s) this month are billed at zero until a price is set.",
                BillingItems.Find(g.Key.Item)?.PerFlow == true ? "/billing/platform-fees" : "/billing/rates");
        }
    }

    private async Task<IEnumerable<BillingAlertFinding>> OverdueStatementsAsync(DateTime now, CancellationToken ct)
    {
        var all = await _statements.GetAllAsync(ct);
        var result = new List<BillingAlertFinding>();
        foreach (var s in all.Where(s => s.Status == BillingStatementStatus.Issued))
        {
            var due = s.IssuedAt.AddDays(_settings.PaymentTermsDays);
            if (due > now) continue;
            var daysLate = (int)(now - due).TotalDays;
            result.Add(new BillingAlertFinding(BillingAlertKind.OverdueStatement, daysLate >= 30 ? BillingAlertSeverity.Critical : BillingAlertSeverity.Warning,
                $"overdue:{s.Number}",
                $"{s.Number} is {daysLate} day(s) overdue",
                $"{s.Currency} {s.Total:N2} for {s.Month:MMMM yyyy} was due on {due:dd MMM yyyy} and is not marked paid.",
                "/billing/statements"));
        }
        return result;
    }

    private async Task<IEnumerable<BillingAlertFinding>> MonthNotClosedAsync(DateTime now, DateTime month, CancellationToken ct)
    {
        var previous = month.AddMonths(-1);
        if (await _statements.ActiveForMonthAsync(previous, ct) is not null) return [];
        var unbilled = await _usage.UnbilledCountsAsync(previous, ct);
        if (unbilled.Count == 0) return [];
        var n = unbilled.Sum(c => c.Count);
        var severity = now.Day >= A.MonthCloseGraceDays ? BillingAlertSeverity.Warning : BillingAlertSeverity.Info;
        return
        [
            new BillingAlertFinding(BillingAlertKind.MonthNotClosed, severity,
                $"unclosed:{previous:yyyy-MM}",
                $"{previous:MMMM yyyy} has not been closed",
                $"{n:N0} billable event(s) up to {previous:MMMM} are waiting for a statement.",
                "/billing/statements"),
        ];
    }
}
