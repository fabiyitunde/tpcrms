using CRMS.Domain.Aggregates.Committee;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface ICommitteeReviewRepository
{
    Task<CommitteeReview?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CommitteeReview?> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task<CommitteeReview?> GetActiveByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    /// <summary>Every review, whatever its status — what the review list shows when no status filter is set.</summary>
    Task<IReadOnlyList<CommitteeReview>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<CommitteeReview>> GetByStatusAsync(CommitteeReviewStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<CommitteeReview>> GetByMemberUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<CommitteeReview>> GetPendingVotesByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<CommitteeReview>> GetOverdueAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CommitteeReview>> GetByCommitteeTypeAsync(CommitteeType type, CancellationToken ct = default);
    /// <summary>
    /// Every committee round recorded against an application number, newest first — the current one
    /// and any superseded by a return for rework.
    /// <para>
    /// Keyed on the application number, not the application id: NAMP creates its reviews with
    /// <c>loanApplicationId: Guid.Empty</c> because a NAMP application is not a LoanApplication, so
    /// the number is the only link back. Application numbers are generated per track and are unique
    /// system-wide, so this does not cross tracks.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<CommitteeReview>> GetAllByApplicationNumberAsync(
        string applicationNumber, CancellationToken ct = default);

    Task AddAsync(CommitteeReview review, CancellationToken ct = default);
    void Update(CommitteeReview review);
}
