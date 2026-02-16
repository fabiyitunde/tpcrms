using CRMS.Domain.Aggregates.LoanApplication;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface ILoanApplicationRepository
{
    Task<LoanApplication?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<LoanApplication?> GetByApplicationNumberAsync(string applicationNumber, CancellationToken ct = default);
    Task<IReadOnlyList<LoanApplication>> GetByStatusAsync(LoanApplicationStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<LoanApplication>> GetByInitiatorAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<LoanApplication>> GetPendingBranchReviewAsync(Guid? branchId = null, CancellationToken ct = default);
    Task<IReadOnlyList<LoanApplication>> GetByAccountNumberAsync(string accountNumber, CancellationToken ct = default);
    Task AddAsync(LoanApplication application, CancellationToken ct = default);
    void Update(LoanApplication application);
}
