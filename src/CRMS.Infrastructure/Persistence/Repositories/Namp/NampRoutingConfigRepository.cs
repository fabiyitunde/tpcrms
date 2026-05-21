using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Namp;

public class NampRoutingConfigRepository : INampRoutingConfigRepository
{
    private readonly CRMSDbContext _context;

    public NampRoutingConfigRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<NampRoutingConfig>> GetAllAsync(CancellationToken ct = default)
        => await _context.NampRoutingConfigs
            .OrderBy(x => x.ApplicantCategory)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.MinEquipmentValue)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<NampRoutingConfig>> GetActiveConfigsAsync(CancellationToken ct = default)
        => await _context.NampRoutingConfigs
            .Where(x => x.IsActive)
            .OrderBy(x => x.ApplicantCategory)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.MinEquipmentValue)
            .ToListAsync(ct);

    public async Task<NampRoutingConfig?> ResolveAsync(NampApplicantCategory category, decimal equipmentValue, CancellationToken ct = default)
        => await _context.NampRoutingConfigs
            .Where(x => x.IsActive
                && x.ApplicantCategory == category
                && x.MinEquipmentValue <= equipmentValue
                && x.MaxEquipmentValue >= equipmentValue)
            .OrderBy(x => x.Priority)
            .FirstOrDefaultAsync(ct);

    public async Task<NampRoutingConfig?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.NampRoutingConfigs.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddAsync(NampRoutingConfig config, CancellationToken ct = default)
        => await _context.NampRoutingConfigs.AddAsync(config, ct);

    public void Update(NampRoutingConfig config)
        => _context.NampRoutingConfigs.Update(config);
}
