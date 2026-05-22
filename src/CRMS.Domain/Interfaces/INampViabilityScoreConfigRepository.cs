using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface INampViabilityScoreConfigRepository
{
    Task<IReadOnlyList<NampViabilityScoreConfig>> GetAllAsync(CancellationToken ct = default);
    Task<NampViabilityScoreConfig?> GetByRatingAsync(NampViabilityRating rating, CancellationToken ct = default);
    Task<NampViabilityScoreConfig?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(NampViabilityScoreConfig config, CancellationToken ct = default);
    void Update(NampViabilityScoreConfig config);
}
