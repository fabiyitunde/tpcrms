using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Interfaces;

public interface IRhshfCommentRepository
{
    Task<IReadOnlyList<RhshfComment>> GetByProfileIdAsync(Guid rhshfCreditProfileId, CancellationToken ct = default);
    Task AddAsync(RhshfComment comment, CancellationToken ct = default);
}
