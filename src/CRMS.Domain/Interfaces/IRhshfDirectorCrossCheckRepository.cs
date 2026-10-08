using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Interfaces;

public interface IRhshfDirectorCrossCheckRepository
{
    /// <summary>The latest saved cross-check for a case (one row per profile), or null if never run.</summary>
    Task<RhshfDirectorCrossCheck?> GetByProfileIdAsync(Guid rhshfCreditProfileId, CancellationToken ct = default);
    Task AddAsync(RhshfDirectorCrossCheck snapshot, CancellationToken ct = default);
    void Update(RhshfDirectorCrossCheck snapshot);
}
