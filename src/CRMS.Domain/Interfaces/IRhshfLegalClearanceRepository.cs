using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Interfaces;

public interface IRhshfLegalClearanceRepository
{
    Task<IReadOnlyList<RhshfLegalClearance>> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default);
    Task AddAsync(RhshfLegalClearance clearance, CancellationToken ct = default);
}
