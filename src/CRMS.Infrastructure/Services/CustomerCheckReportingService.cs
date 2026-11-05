using CRMS.Application.CustomerCheck;
using CRMS.Application.CustomerCheck.Reporting;
using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Services;

/// <summary>
/// Figures for the partner-API dashboard and statements, read straight from the job table and
/// the audit log and priced with the current price list. Projections leave the encrypted request
/// and result columns in the database; only the job detail view decrypts a result, for staff.
/// </summary>
public class CustomerCheckReportingService : ICustomerCheckReporting
{
    private readonly CRMSDbContext _context;
    private readonly ICustomerCheckCipher _cipher;

    public CustomerCheckReportingService(CRMSDbContext context, ICustomerCheckCipher cipher)
    {
        _context = context;
        _cipher = cipher;
    }

    /// <summary>A job without its ciphertext columns.</summary>
    private sealed record JobRow(
        Guid Id, Guid ConsumerId, string ConsumerName, string RequestId, string Status, string BvnLast4, string SectionsRequested,
        DateTime CreatedAt, DateTime? StartedAt, DateTime? CompletedAt, int DurationMs, int BilledCalls, string? BureauUsed, bool BureauReused,
        string? SectionOutcomes, string? ErrorCode, string? ErrorMessage, bool HasCallback, string CallbackStatus, int CallbackAttempts,
        DateTime? CallbackDeliveredAt, int? LastCallbackStatusCode, DateTime? NextCallbackAt,
        decimal? AdvisoryScore, string? AdvisoryRating, string? AdvisoryRecommendation, int RunAttempts);

    private IQueryable<JobRow> Rows(IQueryable<CustomerCheckJob> jobs) => jobs.Select(j => new JobRow(
        j.Id, j.ConsumerId, j.ConsumerName, j.RequestId, j.Status, j.BvnLast4, j.SectionsRequested,
        j.CreatedAt, j.StartedAt, j.CompletedAt, j.DurationMs, j.BilledCalls, j.BureauUsed, j.BureauReused,
        j.SectionOutcomes, j.ErrorCode, j.ErrorMessage, j.CallbackUrl != null, j.CallbackStatus, j.CallbackAttempts,
        j.CallbackDeliveredAt, j.LastCallbackStatusCode, j.NextCallbackAt,
        j.AdvisoryScore, j.AdvisoryRating, j.AdvisoryRecommendation, j.RunAttempts));

    private async Task<(List<JobRow> Jobs, Dictionary<Guid, BilledCallCounts> Calls)> LoadAsync(DateTime from, DateTime to, Guid? consumerId, CancellationToken ct)
    {
        var q = _context.CustomerCheckJobs.Where(j => j.CreatedAt >= from && j.CreatedAt < to);
        if (consumerId is { } c) q = q.Where(j => j.ConsumerId == c);
        var jobs = await Rows(q.OrderBy(j => j.CreatedAt)).ToListAsync(ct);
        var calls = await CallsAsync(jobs.Select(j => j.Id).ToList(), ct);
        return (jobs, calls);
    }

    private async Task<Dictionary<Guid, BilledCallCounts>> CallsAsync(List<Guid> jobIds, CancellationToken ct)
    {
        if (jobIds.Count == 0) return [];
        var logs = await _context.CustomerCheckLogs
            .Where(l => l.JobId != null && jobIds.Contains(l.JobId.Value))
            .Select(l => new { l.JobId, l.IdentityCalls, l.BureauCalls, l.ScoreCalls, l.FraudCalls, l.AdvisoryCalls })
            .ToListAsync(ct);
        return logs.GroupBy(l => l.JobId!.Value).ToDictionary(g => g.Key, g =>
        {
            var l = g.First();
            return new BilledCallCounts(l.IdentityCalls, l.BureauCalls, l.ScoreCalls, l.FraudCalls, l.AdvisoryCalls);
        });
    }

    private async Task<(Dictionary<string, decimal> Prices, string Currency)> PricesAsync(CancellationToken ct)
    {
        var prices = await _context.Set<CustomerCheckPrice>().ToListAsync(ct);
        var currency = prices.Select(p => p.Currency).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? "NGN";
        return (prices.ToDictionary(p => p.Item, p => p.UnitPrice, StringComparer.OrdinalIgnoreCase), currency);
    }

    private static BilledCallCounts CallsOf(Dictionary<Guid, BilledCallCounts> calls, JobRow j) =>
        calls.TryGetValue(j.Id, out var c) ? c : BilledCallCounts.None;

    private static decimal AmountOf(Dictionary<string, decimal> prices, Dictionary<Guid, BilledCallCounts> calls, JobRow j) =>
        CustomerCheckBilling.Amount(prices, j.Status, j.SectionOutcomes, CallsOf(calls, j));

    // ── Overview ─────────────────────────────────────────────────────────────────────────────────

    public async Task<CustomerCheckOverviewDto> GetOverviewAsync(DateTime fromUtc, DateTime toUtc, Guid? consumerId, CancellationToken ct = default)
    {
        var (prices, currency) = await PricesAsync(ct);
        var (jobs, calls) = await LoadAsync(fromUtc, toUtc, consumerId, ct);
        var span = toUtc - fromUtc;
        var (previousJobs, previousCalls) = await LoadAsync(fromUtc - span, fromUtc, consumerId, ct);

        var totals = Totals(jobs, calls, prices);
        var previous = Totals(previousJobs, previousCalls, prices);

        var consumerNames = jobs.Select(j => j.ConsumerName).Distinct().OrderBy(n => n).ToList();
        var byDay = jobs.GroupBy(j => DateOnly.FromDateTime(j.CreatedAt)).ToDictionary(g => g.Key, g => g.ToList());
        var daily = new List<DailyPoint>();
        for (var d = DateOnly.FromDateTime(fromUtc); d < DateOnly.FromDateTime(toUtc.AddTicks(-1)).AddDays(1); d = d.AddDays(1))
        {
            var day = byDay.TryGetValue(d, out var list) ? list : [];
            daily.Add(new DailyPoint(d, day.Count,
                day.Count(j => j.Status == CustomerCheckJob.Completed), day.Count(j => j.Status == CustomerCheckJob.Failed),
                day.Sum(j => j.BilledCalls), day.Sum(j => AmountOf(prices, calls, j)),
                consumerNames.ToDictionary(n => n, n => day.Count(j => j.ConsumerName == n))));
        }

        var consumers = jobs.GroupBy(j => new { j.ConsumerId, j.ConsumerName })
            .Select(g => new ConsumerRow(
                g.Key.ConsumerId, g.Key.ConsumerName, g.Count(),
                g.Count(j => j.Status == CustomerCheckJob.Completed), g.Count(j => j.Status == CustomerCheckJob.Failed),
                g.Sum(j => j.BilledCalls), g.Sum(j => AmountOf(prices, calls, j)),
                Average(g.Where(j => j.Status == CustomerCheckJob.Completed).Select(j => j.DurationMs)),
                g.Count(j => j.CallbackStatus == CustomerCheckJob.CallbackFailed),
                AverageScore(g), g.Max(j => (DateTime?)j.CreatedAt)))
            .OrderByDescending(r => r.Checks).ThenBy(r => r.Name)
            .ToList();

        var completed = jobs.Where(j => j.Status == CustomerCheckJob.Completed).ToList();
        var outcomes = completed.SelectMany(j => CustomerCheckBilling.ParseOutcomes(j.SectionOutcomes)).ToList();
        var sectionOutcomes = outcomes.Where(o => o.Outcome != SectionStatuses.NotRequested)
            .GroupBy(o => o.Outcome).Select(g => new NamedCount(g.Key, g.Count()))
            .OrderByDescending(n => n.Count).ToList();
        var sections = CustomerCheckSections.All.Select(s =>
        {
            var o = outcomes.Where(x => x.Section == s).ToList();
            return new SectionRow(s, o.Count(x => x.Outcome != SectionStatuses.NotRequested),
                o.Count(x => x.Outcome == SectionStatuses.Ok), o.Count(x => x.Outcome == SectionStatuses.NoRecord),
                o.Count(x => x.Outcome is SectionStatuses.Unavailable or "ERROR"), o.Count(x => x.Outcome == SectionStatuses.NotAllowed));
        }).ToList();

        var bureauUsage = completed.Where(j => !string.IsNullOrEmpty(j.BureauUsed))
            .GroupBy(j => $"{Bureaus.Label(j.BureauUsed!)} {(j.BureauReused ? "reused" : "billed")}")
            .Select(g => new NamedCount(g.Key, g.Count())).OrderBy(n => n.Name).ToList();
        var ratings = completed.Where(j => j.AdvisoryRating != null).GroupBy(j => j.AdvisoryRating!)
            .Select(g => new NamedCount(g.Key, g.Count())).OrderBy(n => RatingOrder(n.Name)).ToList();
        var recommendations = completed.Where(j => j.AdvisoryRecommendation != null).GroupBy(j => j.AdvisoryRecommendation!)
            .Select(g => new NamedCount(g.Key, g.Count())).OrderBy(n => RecommendationOrder(n.Name)).ToList();

        var withCallback = jobs.Where(j => j.HasCallback && !(j.Status is CustomerCheckJob.Queued or CustomerCheckJob.Processing)).ToList();
        var callbacks = new CallbackStats(
            withCallback.Count,
            withCallback.Count(j => j.CallbackStatus == CustomerCheckJob.CallbackDelivered),
            withCallback.Count(j => j.CallbackStatus == CustomerCheckJob.CallbackDelivered && j.CallbackAttempts == 1),
            withCallback.Count(j => j.CallbackStatus == CustomerCheckJob.CallbackFailed),
            withCallback.Count(j => j.CallbackStatus == CustomerCheckJob.CallbackPending));

        var heatmap = Enumerable.Range(0, 7).Select(_ => new int[24]).ToArray();
        foreach (var j in jobs)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(j.CreatedAt, DateTimeKind.Utc), TimeZoneInfo.Local);
            heatmap[((int)local.DayOfWeek + 6) % 7][local.Hour]++;
        }

        var failures = jobs.Where(j => j.Status == CustomerCheckJob.Failed).OrderByDescending(j => j.CompletedAt ?? j.CreatedAt).Take(10)
            .Select(j => new RecentFailure(CustomerCheckJobIds.Format(j.Id), j.ConsumerName, j.RequestId, j.CompletedAt ?? j.CreatedAt, j.ErrorCode, j.ErrorMessage))
            .ToList();

        return new CustomerCheckOverviewDto(
            fromUtc, toUtc, consumerId, currency, totals, previous, daily, consumerNames, consumers,
            sectionOutcomes, sections, bureauUsage, ratings, recommendations, callbacks, heatmap, failures);
    }

    private static OverviewTotals Totals(List<JobRow> jobs, Dictionary<Guid, BilledCallCounts> calls, Dictionary<string, decimal> prices)
    {
        var completed = jobs.Where(j => j.Status == CustomerCheckJob.Completed).ToList();
        var failed = jobs.Count(j => j.Status == CustomerCheckJob.Failed);
        var finished = completed.Count + failed;
        var kinds = jobs.Aggregate(BilledCallCounts.None, (acc, j) =>
        {
            var c = CallsOf(calls, j);
            return new BilledCallCounts(acc.Identity + c.Identity, acc.Bureau + c.Bureau, acc.Score + c.Score, acc.Fraud + c.Fraud, acc.Advisory + c.Advisory);
        });
        var withCallback = jobs.Where(j => j.HasCallback && !(j.Status is CustomerCheckJob.Queued or CustomerCheckJob.Processing)).ToList();
        var scored = completed.Where(j => j.AdvisoryScore != null).ToList();
        return new OverviewTotals(
            jobs.Count, completed.Count, failed, jobs.Count(j => j.Status is CustomerCheckJob.Queued or CustomerCheckJob.Processing),
            finished == 0 ? 0 : Math.Round(100m * completed.Count / finished, 1),
            jobs.Sum(j => j.BilledCalls), kinds, jobs.Sum(j => AmountOf(prices, calls, j)),
            Median(completed.Select(j => j.DurationMs)), Average(completed.Select(j => j.DurationMs)),
            scored.Count, scored.Count == 0 ? null : Math.Round(scored.Average(j => j.AdvisoryScore!.Value), 1),
            completed.Count(j => j.BureauUsed != null && !j.BureauReused), completed.Count(j => j.BureauReused),
            withCallback.Count == 0 ? 0 : Math.Round(100m * withCallback.Count(j => j.CallbackStatus == CustomerCheckJob.CallbackDelivered) / withCallback.Count, 1));
    }

    private static decimal? AverageScore(IEnumerable<JobRow> jobs)
    {
        var scores = jobs.Where(j => j.AdvisoryScore != null).Select(j => j.AdvisoryScore!.Value).ToList();
        return scores.Count == 0 ? null : Math.Round(scores.Average(), 1);
    }

    private static int Average(IEnumerable<int> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? 0 : (int)list.Average();
    }

    private static int Median(IEnumerable<int> values)
    {
        var list = values.OrderBy(v => v).ToList();
        if (list.Count == 0) return 0;
        return list.Count % 2 == 1 ? list[list.Count / 2] : (list[list.Count / 2 - 1] + list[list.Count / 2]) / 2;
    }

    private static int RatingOrder(string r) => r switch { "VeryLow" => 0, "Low" => 1, "Medium" => 2, "High" => 3, "VeryHigh" => 4, _ => 5 };
    private static int RecommendationOrder(string r) => r switch { "StrongApprove" => 0, "Approve" => 1, "ApproveWithConditions" => 2, "Refer" => 3, "Decline" => 4, _ => 5 };

    // ── Statement ────────────────────────────────────────────────────────────────────────────────

    public async Task<CustomerCheckStatementDto> GetStatementAsync(Guid consumerId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var (prices, currency) = await PricesAsync(ct);
        var (jobs, calls) = await LoadAsync(fromUtc, toUtc, consumerId, ct);
        var consumerName = jobs.FirstOrDefault()?.ConsumerName
            ?? await _context.ApiConsumers.Where(c => c.Id == consumerId).Select(c => c.Name).FirstOrDefaultAsync(ct)
            ?? "Unknown consumer";

        var lines = jobs.Select(j => new StatementLine(
            j.CreatedAt, CustomerCheckJobIds.Format(j.Id), j.RequestId, j.Status, j.SectionsRequested, j.SectionOutcomes,
            CallsOf(calls, j), AmountOf(prices, calls, j))).ToList();

        var quantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var j in jobs)
            foreach (var (item, quantity) in CustomerCheckBilling.Quantities(j.Status, j.SectionOutcomes, CallsOf(calls, j)))
                quantities[item] = quantities.GetValueOrDefault(item) + quantity;
        var byItem = CustomerCheckPriceItems.All
            .Where(i => quantities.ContainsKey(i.Item))
            .Select(i =>
            {
                var unit = prices.GetValueOrDefault(i.Item);
                return new StatementItemTotal(i.Item, i.DisplayName, quantities[i.Item], unit, Math.Round(quantities[i.Item] * unit, 2));
            }).ToList();

        return new CustomerCheckStatementDto(consumerId, consumerName, fromUtc, toUtc, currency, lines, lines.Sum(l => l.Amount), byItem);
    }

    // ── Jobs ─────────────────────────────────────────────────────────────────────────────────────

    public async Task<(List<CustomerCheckJobSummaryDto> Items, int Total)> SearchJobsAsync(CustomerCheckJobFilter filter, CancellationToken ct = default)
    {
        var q = _context.CustomerCheckJobs.AsQueryable();
        if (filter.ConsumerId is { } consumerId) q = q.Where(j => j.ConsumerId == consumerId);
        if (filter.From is { } from) q = q.Where(j => j.CreatedAt >= from);
        if (filter.To is { } to) q = q.Where(j => j.CreatedAt < to);
        if (!string.IsNullOrEmpty(filter.Status)) q = q.Where(j => j.Status == filter.Status);
        if (!string.IsNullOrEmpty(filter.RequestId)) q = q.Where(j => j.RequestId.Contains(filter.RequestId));
        if (!string.IsNullOrEmpty(filter.BvnHash)) q = q.Where(j => j.BvnHash == filter.BvnHash);

        var total = await q.CountAsync(ct);
        var rows = await Rows(q.OrderByDescending(j => j.CreatedAt))
            .Skip((Math.Max(1, filter.Page) - 1) * Math.Max(1, filter.PageSize))
            .Take(Math.Max(1, filter.PageSize))
            .ToListAsync(ct);
        return (rows.Select(Summary).ToList(), total);
    }

    public async Task<CustomerCheckJobDetailDto?> GetJobDetailAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _context.CustomerCheckJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null) return null;
        var row = await Rows(_context.CustomerCheckJobs.Where(j => j.Id == jobId)).FirstAsync(ct);

        string? result = null, problem = null;
        if (job.Status == CustomerCheckJob.Completed && job.ResultCiphertext is not null)
        {
            try { result = _cipher.Unprotect(job.ResultCiphertext); }
            catch (Exception) { problem = "The stored result could not be decrypted with the current data key."; }
        }
        var host = job.CallbackUrl is not null && Uri.TryCreate(job.CallbackUrl, UriKind.Absolute, out var u) ? u.Host : null;
        return new CustomerCheckJobDetailDto(Summary(row), job.SectionOutcomes, host, job.LastCallbackError, job.EventId, result, problem);
    }

    private static CustomerCheckJobSummaryDto Summary(JobRow j) => new(
        j.Id, CustomerCheckJobIds.Format(j.Id), j.ConsumerId, j.ConsumerName, j.RequestId, j.Status, j.BvnLast4,
        j.SectionsRequested.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        j.CreatedAt, j.StartedAt, j.CompletedAt, j.DurationMs, j.BilledCalls, j.BureauUsed, j.BureauReused, j.ErrorCode, j.ErrorMessage,
        j.CallbackStatus, j.CallbackAttempts, j.CallbackDeliveredAt, j.LastCallbackStatusCode, j.NextCallbackAt, j.HasCallback,
        j.AdvisoryScore, j.AdvisoryRating, j.AdvisoryRecommendation, j.RunAttempts);
}
