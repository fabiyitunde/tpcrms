using CRMS.Domain.Aggregates.LoanApplication;

namespace CRMS.Domain.Interfaces;

public interface IApprovalOverrideRepository
{
    Task AddAsync(ApprovalOverrideRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<ApprovalOverrideRecord>> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task<ApprovalOverrideRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Update(ApprovalOverrideRecord record);
}
