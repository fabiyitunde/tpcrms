using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfRoutingConfigRepository : IRhshfRoutingConfigRepository
{
    private readonly CRMSDbContext _context;

    public RhshfRoutingConfigRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RhshfRoutingConfig>> GetAllAsync(CancellationToken ct = default)
        => await _context.RhshfRoutingConfigs
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.MinEopValue)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RhshfRoutingConfig>> GetActiveConfigsAsync(CancellationToken ct = default)
        => await _context.RhshfRoutingConfigs
            .Where(x => x.IsActive)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.MinEopValue)
            .ToListAsync(ct);

    public async Task<RhshfRoutingConfig?> ResolveAsync(decimal totalEopValue, CancellationToken ct = default)
        => await _context.RhshfRoutingConfigs
            .Where(x => x.IsActive && x.MinEopValue <= totalEopValue && x.MaxEopValue >= totalEopValue)
            .OrderBy(x => x.Priority)
            .FirstOrDefaultAsync(ct);

    public async Task<RhshfRoutingConfig?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.RhshfRoutingConfigs.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddAsync(RhshfRoutingConfig config, CancellationToken ct = default)
        => await _context.RhshfRoutingConfigs.AddAsync(config, ct);

    public void Update(RhshfRoutingConfig config)
        => _context.RhshfRoutingConfigs.Update(config);
}
