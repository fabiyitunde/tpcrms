using CRMS.Domain.Aggregates.Committee;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface ICommitteeReviewRepository
{
    Task<CommitteeReview?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CommitteeReview?> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task<CommitteeReview?> GetActiveByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task<IReadOnlyList<CommitteeReview>> GetByStatusAsync(CommitteeReviewStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<CommitteeReview>> GetByMemberUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<CommitteeReview>> GetPendingVotesByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<CommitteeReview>> GetOverdueAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CommitteeReview>> GetByCommitteeTypeAsync(CommitteeType type, CancellationToken ct = default);
    Task AddAsync(CommitteeReview review, CancellationToken ct = default);
    void Update(CommitteeReview review);
}
