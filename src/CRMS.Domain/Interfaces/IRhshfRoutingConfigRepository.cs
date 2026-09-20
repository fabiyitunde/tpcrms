using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Interfaces;

public interface IRhshfRoutingConfigRepository
{
    Task<IReadOnlyList<RhshfRoutingConfig>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RhshfRoutingConfig>> GetActiveConfigsAsync(CancellationToken ct = default);
    Task<RhshfRoutingConfig?> ResolveAsync(decimal totalEopValue, CancellationToken ct = default);
    Task<RhshfRoutingConfig?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(RhshfRoutingConfig config, CancellationToken ct = default);
    void Update(RhshfRoutingConfig config);
}
