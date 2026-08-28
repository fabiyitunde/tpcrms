using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Interfaces;

public interface IRhshfCommitteeReviewRepository
{
    Task<RhshfCommitteeReview?> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default);
    Task AddAsync(RhshfCommitteeReview review, CancellationToken ct = default);
}
