using CRMS.Domain.Aggregates.FinancialStatement;

namespace CRMS.Domain.Interfaces;

public interface IFinancialStatementRepository
{
    Task<FinancialStatement?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<FinancialStatement?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<FinancialStatement>> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task<FinancialStatement?> GetByLoanApplicationAndYearAsync(Guid loanApplicationId, int year, CancellationToken ct = default);
    Task AddAsync(FinancialStatement statement, CancellationToken ct = default);
    void Update(FinancialStatement statement);
}
