using CRMS.Domain.Aggregates.Collateral;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class CollateralTypeConfigRepository : ICollateralTypeConfigRepository
{
    private readonly CRMSDbContext _context;

    public CollateralTypeConfigRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<CollateralTypeConfig?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.CollateralTypeConfigs.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<CollateralTypeConfig?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await _context.CollateralTypeConfigs.FirstOrDefaultAsync(x => x.Code == code, ct);

    public async Task<List<CollateralTypeConfig>> GetAllAsync(CancellationToken ct = default)
        => await _context.CollateralTypeConfigs.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(ct);

    public async Task<List<CollateralTypeConfig>> GetActiveAsync(CancellationToken ct = default)
        => await _context.CollateralTypeConfigs
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);

    public async Task AddAsync(CollateralTypeConfig config, CancellationToken ct = default)
        => await _context.CollateralTypeConfigs.AddAsync(config, ct);

    public void Update(CollateralTypeConfig config)
        => _context.CollateralTypeConfigs.Update(config);
}
