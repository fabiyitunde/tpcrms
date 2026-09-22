using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Interfaces;

public interface IRhshfAdvisoryRepository
{
    Task<RhshfAdvisory?> GetByRhshfCreditProfileIdAsync(Guid rhshfCreditProfileId, CancellationToken ct = default);
    Task<RhshfAdvisory?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(RhshfAdvisory advisory, CancellationToken ct = default);
    void Update(RhshfAdvisory advisory);
}
