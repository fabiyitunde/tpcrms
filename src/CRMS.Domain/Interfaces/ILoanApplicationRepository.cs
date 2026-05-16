using CRMS.Domain.Aggregates.LoanApplication;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface ILoanApplicationRepository
{
    Task<LoanApplication?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<LoanApplication?> GetByIdNoTrackingAsync(Guid id, CancellationToken ct = default);
    Task<LoanApplication?> GetByIdWithPartiesAsync(Guid id, CancellationToken ct = default);
    Task<LoanApplication?> GetByApplicationNumberAsync(string applicationNumber, CancellationToken ct = default);
    Task<IReadOnlyList<LoanApplication>> GetByStatusAsync(LoanApplicationStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<LoanApplication>> GetByStatusFilteredAsync(LoanApplicationStatus status, IReadOnlyList<Guid>? visibleBranchIds, CancellationToken ct = default);
    Task<IReadOnlyList<LoanApplication>> GetByInitiatorAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<LoanApplication>> GetPendingBranchReviewAsync(Guid? branchId = null, CancellationToken ct = default);
    Task<IReadOnlyList<LoanApplication>> GetPendingBranchReviewFilteredAsync(IReadOnlyList<Guid>? visibleBranchIds, CancellationToken ct = default);
    Task<IReadOnlyList<LoanApplication>> GetByAccountNumberAsync(string accountNumber, CancellationToken ct = default);
    Task<LoanApplication?> GetByIdWithChecklistAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(LoanApplication application, CancellationToken ct = default);
    void Update(LoanApplication application);
}

public interface ILoanApplicationDocumentRepository
{
    Task AddAsync(LoanApplicationDocument document, CancellationToken ct = default);
    Task<LoanApplicationDocument?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
