using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Interfaces;

public interface IRhshfPreDeploymentChecklistTemplateRepository
{
    Task<IReadOnlyList<RhshfPreDeploymentChecklistTemplate>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RhshfPreDeploymentChecklistTemplate>> GetActiveAsync(CancellationToken ct = default);
    Task<RhshfPreDeploymentChecklistTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(RhshfPreDeploymentChecklistTemplate template, CancellationToken ct = default);
    void Update(RhshfPreDeploymentChecklistTemplate template);
}
