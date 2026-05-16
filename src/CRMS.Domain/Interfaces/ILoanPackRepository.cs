using CRMS.Domain.Aggregates.LoanPack;

namespace CRMS.Domain.Interfaces;

public interface ILoanPackRepository
{
    Task<LoanPack?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<LoanPack?> GetLatestByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task<IReadOnlyList<LoanPack>> GetAllByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    /// <summary>Returns the highest version number ever persisted (including Failed records), or 0 if none exist.</summary>
    Task<int> GetMaxVersionAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task AddAsync(LoanPack loanPack, CancellationToken ct = default);
    void Update(LoanPack loanPack);
}
