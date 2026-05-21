using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface INampWorkflowConfigRepository
{
    Task<NampWorkflowConfig?> GetByStatusAsync(NampApplicationStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<NampWorkflowConfig>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(NampWorkflowConfig config, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<NampWorkflowConfig> configs, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
}

public interface INampWorkflowInstanceRepository
{
    Task<NampWorkflowInstance?> GetByNampApplicationIdAsync(Guid nampApplicationId, CancellationToken ct = default);
    Task<IReadOnlyList<NampWorkflowInstance>> GetByRoleAsync(string assignedRole, CancellationToken ct = default);
    Task<IReadOnlyList<NampWorkflowInstance>> GetSLABreachedAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NampWorkflowInstance>> GetOverdueAsync(CancellationToken ct = default);
    Task AddAsync(NampWorkflowInstance instance, CancellationToken ct = default);
    void Update(NampWorkflowInstance instance);
}
