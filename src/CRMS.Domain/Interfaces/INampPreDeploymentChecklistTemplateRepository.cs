using CRMS.Domain.Aggregates.Namp;

namespace CRMS.Domain.Interfaces;

public interface INampPreDeploymentChecklistTemplateRepository
{
    Task<IReadOnlyList<NampPreDeploymentChecklistTemplate>> GetAllAsync(CancellationToken ct = default);
    Task<NampPreDeploymentChecklistTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(NampPreDeploymentChecklistTemplate template, CancellationToken ct = default);
}
