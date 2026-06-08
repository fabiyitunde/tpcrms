using CRMS.Domain.Aggregates.Namp;

namespace CRMS.Domain.Interfaces;

public interface INampAdvisoryRepository
{
    Task<NampAdvisory?> GetByNampApplicationIdAsync(Guid nampApplicationId, CancellationToken ct = default);
    Task<NampAdvisory?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(NampAdvisory advisory, CancellationToken ct = default);
    void Update(NampAdvisory advisory);
    void Remove(NampAdvisory advisory);
}
