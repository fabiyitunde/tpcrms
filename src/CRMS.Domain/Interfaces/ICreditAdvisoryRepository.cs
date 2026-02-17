using CRMS.Domain.Aggregates.Advisory;

namespace CRMS.Domain.Interfaces;

public interface ICreditAdvisoryRepository
{
    Task<CreditAdvisory?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CreditAdvisory?> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task<CreditAdvisory?> GetLatestByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task<IReadOnlyList<CreditAdvisory>> GetAllByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task AddAsync(CreditAdvisory advisory, CancellationToken ct = default);
    void Update(CreditAdvisory advisory);
}
