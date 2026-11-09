using CRMS.Domain.Aggregates.Billing;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class BillingUsageRepository : IBillingUsageRepository
{
    private readonly CRMSDbContext _context;

    public BillingUsageRepository(CRMSDbContext context) => _context = context;

    public async Task AddAsync(BillingUsage usage, CancellationToken ct = default) => await _context.BillingUsages.AddAsync(usage, ct);

    public async Task AddRangeAsync(IEnumerable<BillingUsage> usages, CancellationToken ct = default) => await _context.BillingUsages.AddRangeAsync(usages, ct);

    public Task<bool> SourceKeyExistsAsync(string sourceKey, CancellationToken ct = default) =>
        _context.BillingUsages.AnyAsync(u => u.SourceKey == sourceKey, ct);

    public async Task<HashSet<string>> ExistingSourceKeysAsync(IEnumerable<string> sourceKeys, CancellationToken ct = default)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        // Chunked so a large backfill never builds one enormous IN list.
        foreach (var chunk in sourceKeys.Distinct(StringComparer.Ordinal).Chunk(500))
        {
            var hits = await _context.BillingUsages.AsNoTracking()
                .Where(u => u.SourceKey != null && chunk.Contains(u.SourceKey))
                .Select(u => u.SourceKey!)
                .ToListAsync(ct);
            foreach (var h in hits) found.Add(h);
        }
        return found;
    }

    public async Task<IReadOnlyList<BillingUsageCount>> CountsAsync(DateTime fromMonth, DateTime toMonth, BillingFlow? flow, CancellationToken ct = default)
    {
        var q = _context.BillingUsages.AsNoTracking().Where(u => u.Month >= fromMonth && u.Month <= toMonth);
        if (flow is { } f) q = q.Where(u => u.Flow == f);
        return await q
            .GroupBy(u => new { u.Item, u.Flow, u.Month, u.Succeeded })
            .Select(g => new BillingUsageCount(g.Key.Item, g.Key.Flow, g.Key.Month, g.Key.Succeeded, g.Count()))
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<BillingUsage> Items, int Total)> SearchAsync(BillingUsageSearch s, CancellationToken ct = default)
    {
        var q = _context.BillingUsages.AsNoTracking().AsQueryable();
        if (s.Month is { } m) q = q.Where(u => u.Month == m);
        if (s.Flow is { } f) q = q.Where(u => u.Flow == f);
        if (s.Category is { } c) q = q.Where(u => u.Category == c);
        if (!string.IsNullOrWhiteSpace(s.Item)) q = q.Where(u => u.Item == s.Item);
        if (s.CaseId is { } id) q = q.Where(u => u.CaseId == id);
        if (!string.IsNullOrWhiteSpace(s.CaseReference)) q = q.Where(u => u.CaseReference != null && u.CaseReference.Contains(s.CaseReference));
        if (s.Succeeded is { } ok) q = q.Where(u => u.Succeeded == ok);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(u => u.OccurredAt)
            .Skip((s.Page - 1) * s.PageSize).Take(s.PageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<BillingUsage>> ForCaseAsync(Guid caseId, CancellationToken ct = default) =>
        await _context.BillingUsages.AsNoTracking().Where(u => u.CaseId == caseId).OrderByDescending(u => u.OccurredAt).ToListAsync(ct);

    public async Task<IReadOnlyList<BillingUsage>> ForCasesAsync(IEnumerable<Guid> caseIds, CancellationToken ct = default)
    {
        var ids = caseIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        return await _context.BillingUsages.AsNoTracking().Where(u => u.CaseId != null && ids.Contains(u.CaseId.Value)).ToListAsync(ct);
    }

    public async Task<DateTime?> FirstLiveAtAsync(CancellationToken ct = default) =>
        await _context.BillingUsages.AsNoTracking().Where(u => u.Source == BillingUsageSource.Live).MinAsync(u => (DateTime?)u.OccurredAt, ct);

    public Task<int> CountBySourceAsync(BillingUsageSource source, CancellationToken ct = default) =>
        _context.BillingUsages.AsNoTracking().CountAsync(u => u.Source == source, ct);
}

public class BillingRateRepository : IBillingRateRepository
{
    private readonly CRMSDbContext _context;

    public BillingRateRepository(CRMSDbContext context) => _context = context;

    public async Task<IReadOnlyList<BillingRate>> GetAllAsync(CancellationToken ct = default) =>
        await _context.BillingRates.AsNoTracking().OrderBy(r => r.Item).ThenBy(r => r.EffectiveFrom).ToListAsync(ct);

    public Task<BillingRate?> FindAsync(string item, BillingFlow? flow, DateTime effectiveFromMonth, CancellationToken ct = default) =>
        _context.BillingRates.FirstOrDefaultAsync(r => r.Item == item && r.Flow == flow && r.EffectiveFrom == effectiveFromMonth, ct);

    public async Task AddAsync(BillingRate rate, CancellationToken ct = default) => await _context.BillingRates.AddAsync(rate, ct);

    public Task UpdateAsync(BillingRate rate, CancellationToken ct = default)
    {
        _context.BillingRates.Update(rate);
        return Task.CompletedTask;
    }
}
