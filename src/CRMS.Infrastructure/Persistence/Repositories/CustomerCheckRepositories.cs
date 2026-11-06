using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class ApiConsumerRepository : IApiConsumerRepository
{
    private readonly CRMSDbContext _context;

    public ApiConsumerRepository(CRMSDbContext context) => _context = context;

    // A consumer row is written from several places that do not share a DbContext: an
    // administrator's intranet session (one context for hours), the page a partner opens from a
    // setup link, and the API's job worker (one context for a whole check, minutes). Two rules
    // keep them from undoing each other, and above all from putting an old key back:
    //   1. A consumer loaded to be changed is first refreshed from the database, so the change
    //      is made to the row as it is now, not as this context first saw it.
    //   2. A save writes only the columns that changed (see Update), never the whole row.

    public async Task<ApiConsumer?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var held = _context.ApiConsumers.Local.FirstOrDefault(x => x.Id == id);
        if (held is null)
            return await _context.ApiConsumers.FirstOrDefaultAsync(x => x.Id == id, ct);

        var entry = _context.Entry(held);
        // Something this context has not saved yet is its own work in progress; leave it.
        if (entry.State != EntityState.Unchanged)
            return held;
        await entry.ReloadAsync(ct);
        // Reload detaches an entity whose row has gone.
        return entry.State == EntityState.Detached ? null : held;
    }

    public Task<ApiConsumer?> GetByKeyHashAsync(string keyHash, CancellationToken ct = default)
        => _context.ApiConsumers.FirstOrDefaultAsync(x => x.KeyHash == keyHash, ct);

    // Used only to ask "is the name taken, and by whom": read fresh, kept out of the tracker.
    public Task<ApiConsumer?> GetByNameAsync(string name, CancellationToken ct = default)
        => _context.ApiConsumers.AsNoTracking().FirstOrDefaultAsync(x => x.Name == name, ct);

    // For display: read fresh every time, so the list never shows a copy this context held on to.
    public Task<List<ApiConsumer>> GetAllAsync(CancellationToken ct = default)
        => _context.ApiConsumers.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);

    public async Task AddAsync(ApiConsumer consumer, CancellationToken ct = default)
        => await _context.ApiConsumers.AddAsync(consumer, ct);

    /// <summary>
    /// A consumer this context loaded is already tracked: the save finds what changed and writes
    /// those columns alone. It must not be marked modified as a whole, because that writes every
    /// column back as this context loaded it, and a key or secret replaced elsewhere in the
    /// meantime would be silently restored.
    /// </summary>
    public void Update(ApiConsumer consumer)
    {
        if (_context.Entry(consumer).State == EntityState.Detached)
            _context.ApiConsumers.Update(consumer);
    }
}

public class CustomerCheckRepository : ICustomerCheckRepository
{
    private readonly CRMSDbContext _context;

    public CustomerCheckRepository(CRMSDbContext context) => _context = context;

    public async Task AddLogAsync(CustomerCheckLog log, CancellationToken ct = default)
        => await _context.CustomerCheckLogs.AddAsync(log, ct);

    public Task<CustomerBureauReport?> FindBureauReportAsync(string bvnHash, string bureau, DateTime notBefore, CancellationToken ct = default)
        => _context.CustomerBureauReports
            .Where(x => x.BvnHash == bvnHash && x.Bureau == bureau && x.FetchedAt >= notBefore)
            .OrderByDescending(x => x.FetchedAt)
            .FirstOrDefaultAsync(ct);

    public async Task AddBureauReportAsync(CustomerBureauReport report, CancellationToken ct = default)
        => await _context.CustomerBureauReports.AddAsync(report, ct);
}

public class ApiConsumerSetupLinkRepository : IApiConsumerSetupLinkRepository
{
    private readonly CRMSDbContext _context;

    public ApiConsumerSetupLinkRepository(CRMSDbContext context) => _context = context;

    public async Task AddAsync(ApiConsumerSetupLink link, CancellationToken ct = default)
        => await _context.ApiConsumerSetupLinks.AddAsync(link, ct);

    // Read without tracking: the state of a link is changed only by the conditional updates
    // below, which bypass the change tracker, so a tracked copy would only ever go stale.
    public Task<ApiConsumerSetupLink?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default)
        => _context.ApiConsumerSetupLinks.AsNoTracking().FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);

    public Task<List<ApiConsumerSetupLink>> GetByConsumerAsync(Guid consumerId, int take, CancellationToken ct = default)
        => _context.ApiConsumerSetupLinks.AsNoTracking()
            .Where(x => x.ConsumerId == consumerId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task<List<ApiConsumerSetupLink>> GetLatestPerConsumerAsync(CancellationToken ct = default)
    {
        // Two plain statements rather than a correlated one: the newest time per consumer, then
        // those rows. Links are few, a handful per consumer.
        var newest = await _context.ApiConsumerSetupLinks.AsNoTracking()
            .GroupBy(x => x.ConsumerId)
            .Select(g => new { ConsumerId = g.Key, At = g.Max(x => x.CreatedAt) })
            .ToListAsync(ct);
        if (newest.Count == 0)
            return [];

        var times = newest.Select(n => n.At).Distinct().ToList();
        var rows = await _context.ApiConsumerSetupLinks.AsNoTracking()
            .Where(x => times.Contains(x.CreatedAt))
            .ToListAsync(ct);
        return rows.Where(r => newest.Any(n => n.ConsumerId == r.ConsumerId && n.At == r.CreatedAt)).ToList();
    }

    public Task<int> RevokeOpenAsync(Guid consumerId, DateTime now, CancellationToken ct = default)
        => _context.ApiConsumerSetupLinks
            .Where(x => x.ConsumerId == consumerId && x.OpenedAt == null && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);

    public async Task<bool> TryClaimAsync(Guid linkId, DateTime now, string? ipAddress, CancellationToken ct = default)
    {
        var ip = ipAddress is { Length: > 64 } ? ipAddress[..64] : ipAddress;
        var claimed = await _context.ApiConsumerSetupLinks
            .Where(x => x.Id == linkId && x.OpenedAt == null && x.RevokedAt == null && x.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.OpenedAt, now)
                .SetProperty(x => x.OpenedFromIp, ip), ct);
        return claimed == 1;
    }

    public Task ReleaseAsync(Guid linkId, CancellationToken ct = default)
        => _context.ApiConsumerSetupLinks
            .Where(x => x.Id == linkId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.OpenedAt, (DateTime?)null)
                .SetProperty(x => x.OpenedFromIp, (string?)null), ct);
}
