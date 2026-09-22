using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Interfaces;

public interface IRhshfDocumentRequirementRepository
{
    Task<IReadOnlyList<RhshfDocumentRequirement>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RhshfDocumentRequirement>> GetActiveAsync(CancellationToken ct = default);
    Task<RhshfDocumentRequirement?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(RhshfDocumentRequirement requirement, CancellationToken ct = default);
    void Update(RhshfDocumentRequirement requirement);
}
