using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Interfaces;

public interface IRhshfEligibilityCheckRepository
{
    Task<IReadOnlyList<RhshfEligibilityCheck>> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default);
    Task AddAsync(RhshfEligibilityCheck check, CancellationToken ct = default);
}
