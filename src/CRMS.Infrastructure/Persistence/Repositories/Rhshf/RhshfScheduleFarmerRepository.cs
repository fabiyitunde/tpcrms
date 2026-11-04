using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfScheduleFarmerRepository : IRhshfScheduleFarmerRepository
{
    private readonly CRMSDbContext _context;

    public RhshfScheduleFarmerRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public Task<int> CountForProfileAsync(Guid rhshfCreditProfileId, CancellationToken ct = default)
        => _context.RhshfScheduleFarmers.CountAsync(x => x.RhshfCreditProfileId == rhshfCreditProfileId, ct);

    public Task AddRangeAsync(IEnumerable<RhshfScheduleFarmer> farmers, CancellationToken ct = default)
        => _context.RhshfScheduleFarmers.AddRangeAsync(farmers, ct);

    public async Task RemoveForProfileAsync(Guid rhshfCreditProfileId, IReadOnlyCollection<Guid>? keepEopIds = null, CancellationToken ct = default)
    {
        var rows = await _context.RhshfScheduleFarmers
            .Where(x => x.RhshfCreditProfileId == rhshfCreditProfileId)
            .ToListAsync(ct);
        _context.RhshfScheduleFarmers.RemoveRange(keepEopIds is null
            ? rows
            : rows.Where(r => r.EopId is { } eop && !keepEopIds.Contains(eop)));
    }
}
