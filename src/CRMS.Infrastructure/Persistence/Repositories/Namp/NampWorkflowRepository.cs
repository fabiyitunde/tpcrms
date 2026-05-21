using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Namp;

public class NampWorkflowConfigRepository : INampWorkflowConfigRepository
{
    private readonly CRMSDbContext _context;

    public NampWorkflowConfigRepository(CRMSDbContext context) => _context = context;

    public async Task<NampWorkflowConfig?> GetByStatusAsync(NampApplicationStatus status, CancellationToken ct = default)
        => await _context.NampWorkflowConfigs.FirstOrDefaultAsync(x => x.Status == status, ct);

    public async Task<IReadOnlyList<NampWorkflowConfig>> GetAllAsync(CancellationToken ct = default)
        => await _context.NampWorkflowConfigs.OrderBy(x => x.SortOrder).ToListAsync(ct);

    public async Task AddAsync(NampWorkflowConfig config, CancellationToken ct = default)
        => await _context.NampWorkflowConfigs.AddAsync(config, ct);

    public async Task AddRangeAsync(IEnumerable<NampWorkflowConfig> configs, CancellationToken ct = default)
        => await _context.NampWorkflowConfigs.AddRangeAsync(configs, ct);

    public async Task<bool> AnyAsync(CancellationToken ct = default)
        => await _context.NampWorkflowConfigs.AnyAsync(ct);
}

public class NampWorkflowInstanceRepository : INampWorkflowInstanceRepository
{
    private readonly CRMSDbContext _context;

    public NampWorkflowInstanceRepository(CRMSDbContext context) => _context = context;

    public async Task<NampWorkflowInstance?> GetByNampApplicationIdAsync(Guid nampApplicationId, CancellationToken ct = default)
        => await _context.NampWorkflowInstances.FirstOrDefaultAsync(x => x.NampApplicationId == nampApplicationId, ct);

    public async Task<IReadOnlyList<NampWorkflowInstance>> GetByRoleAsync(string assignedRole, CancellationToken ct = default)
        => await _context.NampWorkflowInstances
            .Where(x => !x.IsCompleted && x.AssignedRole == assignedRole)
            .OrderBy(x => x.SLADueAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<NampWorkflowInstance>> GetSLABreachedAsync(CancellationToken ct = default)
        => await _context.NampWorkflowInstances
            .Where(x => !x.IsCompleted && x.IsSLABreached)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<NampWorkflowInstance>> GetOverdueAsync(CancellationToken ct = default)
        => await _context.NampWorkflowInstances
            .Where(x => !x.IsCompleted && x.SLADueAt.HasValue && x.SLADueAt < DateTime.UtcNow)
            .ToListAsync(ct);

    public async Task AddAsync(NampWorkflowInstance instance, CancellationToken ct = default)
        => await _context.NampWorkflowInstances.AddAsync(instance, ct);

    public void Update(NampWorkflowInstance instance)
        => _context.NampWorkflowInstances.Update(instance);
}
