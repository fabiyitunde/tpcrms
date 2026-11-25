using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Interfaces;

public interface IRhshfProfilingFieldRequirementRepository
{
    Task<IReadOnlyList<RhshfProfilingFieldRequirement>> GetAllAsync(CancellationToken ct = default);
    Task<RhshfProfilingFieldRequirement?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
