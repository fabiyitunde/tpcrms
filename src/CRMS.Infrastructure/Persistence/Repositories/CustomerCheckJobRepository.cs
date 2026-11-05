using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class CustomerCheckJobRepository : ICustomerCheckJobRepository
{
    private readonly CRMSDbContext _context;

    public CustomerCheckJobRepository(CRMSDbContext context) => _context = context;

    public async Task AddAsync(CustomerCheckJob job, CancellationToken ct = default)
        => await _context.CustomerCheckJobs.AddAsync(job, ct);

    public Task<CustomerCheckJob?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.CustomerCheckJobs.FirstOrDefaultAsync(j => j.Id == id, ct);

    public Task<CustomerCheckJob?> FindByRequestIdAsync(Guid consumerId, string requestId, DateTime since, CancellationToken ct = default)
        => _context.CustomerCheckJobs
            .Where(j => j.ConsumerId == consumerId && j.RequestId == requestId && j.CreatedAt >= since)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<int> CountOpenAsync(Guid consumerId, CancellationToken ct = default)
        => _context.CustomerCheckJobs.CountAsync(
            j => j.ConsumerId == consumerId && (j.Status == CustomerCheckJob.Queued || j.Status == CustomerCheckJob.Processing), ct);

    public async Task<Guid?> ClaimNextAsync(DateTime now, DateTime staleBefore, CancellationToken ct = default)
    {
        // Oldest first. A few candidates are read, then each is claimed with a conditional update
        // so two workers (or two instances) can never take the same job: the update only lands
        // if the row is still in the state the read saw.
        var candidates = await _context.CustomerCheckJobs
            .Where(j => j.Status == CustomerCheckJob.Queued
                || (j.Status == CustomerCheckJob.Processing && j.StartedAt != null && j.StartedAt < staleBefore))
            .OrderBy(j => j.CreatedAt)
            .Select(j => j.Id)
            .Take(5)
            .ToListAsync(ct);

        foreach (var id in candidates)
        {
            var claimed = await _context.CustomerCheckJobs
                .Where(j => j.Id == id
                    && (j.Status == CustomerCheckJob.Queued
                        || (j.Status == CustomerCheckJob.Processing && j.StartedAt != null && j.StartedAt < staleBefore)))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, CustomerCheckJob.Processing)
                    .SetProperty(j => j.StartedAt, now)
                    .SetProperty(j => j.RunAttempts, j => j.RunAttempts + 1), ct);
            if (claimed == 1)
                return id;
        }
        return null;
    }

    public Task<List<Guid>> GetDueCallbackIdsAsync(DateTime now, int take, CancellationToken ct = default)
        => _context.CustomerCheckJobs
            .Where(j => j.CallbackStatus == CustomerCheckJob.CallbackPending && j.NextCallbackAt != null && j.NextCallbackAt <= now
                && (j.Status == CustomerCheckJob.Completed || j.Status == CustomerCheckJob.Failed))
            .OrderBy(j => j.NextCallbackAt)
            .Select(j => j.Id)
            .Take(take)
            .ToListAsync(ct);

    public async Task<(List<CustomerCheckJob> Items, int Total)> SearchAsync(CustomerCheckJobFilter filter, CancellationToken ct = default)
    {
        var q = _context.CustomerCheckJobs.AsQueryable();
        if (filter.ConsumerId is { } consumerId) q = q.Where(j => j.ConsumerId == consumerId);
        if (filter.From is { } from) q = q.Where(j => j.CreatedAt >= from);
        if (filter.To is { } to) q = q.Where(j => j.CreatedAt < to);
        if (!string.IsNullOrEmpty(filter.Status)) q = q.Where(j => j.Status == filter.Status);
        if (!string.IsNullOrEmpty(filter.RequestId)) q = q.Where(j => j.RequestId == filter.RequestId);
        if (!string.IsNullOrEmpty(filter.BvnHash)) q = q.Where(j => j.BvnHash == filter.BvnHash);

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, filter.Page);
        var size = Math.Max(1, filter.PageSize);
        var items = await q.OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);
        return (items, total);
    }

    public void Update(CustomerCheckJob job) => _context.CustomerCheckJobs.Update(job);
}
