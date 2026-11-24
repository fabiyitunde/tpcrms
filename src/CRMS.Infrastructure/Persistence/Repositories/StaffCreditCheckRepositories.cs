using CRMS.Domain.Aggregates.Access;
using CRMS.Domain.Aggregates.CreditCheck;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class StaffCreditCheckRepository : IStaffCreditCheckRepository
{
    private readonly CRMSDbContext _context;

    public StaffCreditCheckRepository(CRMSDbContext context) => _context = context;

    public async Task AddAsync(StaffCreditCheck check, CancellationToken ct = default)
        => await _context.StaffCreditChecks.AddAsync(check, ct);

    public Task<StaffCreditCheck?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.StaffCreditChecks.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<int> CountOpenAsync(Guid userId, CancellationToken ct = default)
        => _context.StaffCreditChecks.CountAsync(
            c => c.RequestedByUserId == userId && (c.Status == StaffCreditCheck.Queued || c.Status == StaffCreditCheck.Processing), ct);

    public async Task<Guid?> ClaimNextAsync(DateTime now, DateTime staleBefore, CancellationToken ct = default)
    {
        // Same claim as the partner queue: read a few candidates, then take one with a conditional
        // update that only lands if the row is still in the state the read saw.
        var candidates = await _context.StaffCreditChecks
            .Where(c => c.Status == StaffCreditCheck.Queued
                || (c.Status == StaffCreditCheck.Processing && c.StartedAt != null && c.StartedAt < staleBefore))
            .OrderBy(c => c.CreatedAt)
            .Select(c => c.Id)
            .Take(5)
            .ToListAsync(ct);

        foreach (var id in candidates)
        {
            var claimed = await _context.StaffCreditChecks
                .Where(c => c.Id == id
                    && (c.Status == StaffCreditCheck.Queued
                        || (c.Status == StaffCreditCheck.Processing && c.StartedAt != null && c.StartedAt < staleBefore)))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Status, StaffCreditCheck.Processing)
                    .SetProperty(c => c.StartedAt, now)
                    .SetProperty(c => c.RunAttempts, c => c.RunAttempts + 1), ct);
            if (claimed == 1)
                return id;
        }
        return null;
    }

    public async Task<(List<StaffCreditCheck> Items, int Total)> SearchAsync(StaffCreditCheckFilter filter, CancellationToken ct = default)
    {
        var q = _context.StaffCreditChecks.AsNoTracking();
        if (filter.RequestedBy is { } user) q = q.Where(c => c.RequestedByUserId == user);
        if (!string.IsNullOrEmpty(filter.SubjectType)) q = q.Where(c => c.SubjectType == filter.SubjectType);
        if (!string.IsNullOrEmpty(filter.Status)) q = q.Where(c => c.Status == filter.Status);
        if (!string.IsNullOrEmpty(filter.SubjectHash)) q = q.Where(c => c.SubjectHash == filter.SubjectHash);
        if (filter.From is { } from) q = q.Where(c => c.CreatedAt >= from);
        if (filter.To is { } to) q = q.Where(c => c.CreatedAt < to);

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, filter.Page);
        var size = Math.Clamp(filter.PageSize, 1, 100);
        var items = await q.OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);
        return (items, total);
    }

    public void Update(StaffCreditCheck check)
    {
        // Leave a tracked row to change detection, so a save writes only what changed.
        if (_context.Entry(check).State == EntityState.Detached)
            _context.StaffCreditChecks.Update(check);
    }
}

public class FeatureAccessRuleRepository : IFeatureAccessRuleRepository
{
    private readonly CRMSDbContext _context;

    public FeatureAccessRuleRepository(CRMSDbContext context) => _context = context;

    public Task<FeatureAccessRule?> GetByKeyAsync(string featureKey, CancellationToken ct = default)
        => _context.FeatureAccessRules.FirstOrDefaultAsync(r => r.FeatureKey == featureKey, ct);

    public Task<List<FeatureAccessRule>> GetAllAsync(CancellationToken ct = default)
        => _context.FeatureAccessRules.OrderBy(r => r.FeatureKey).ToListAsync(ct);

    public async Task AddAsync(FeatureAccessRule rule, CancellationToken ct = default)
        => await _context.FeatureAccessRules.AddAsync(rule, ct);

    public void Update(FeatureAccessRule rule)
    {
        if (_context.Entry(rule).State == EntityState.Detached)
            _context.FeatureAccessRules.Update(rule);
    }
}
