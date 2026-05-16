using CRMS.Domain.Aggregates.StatementAnalysis;

namespace CRMS.Domain.Interfaces;

public interface IBankStatementRepository
{
    Task<BankStatement?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<BankStatement?> GetByIdWithTransactionsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BankStatement>> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task<bool> HasStatementsAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task<IReadOnlyList<BankStatement>> GetByAccountNumberAsync(string accountNumber, CancellationToken ct = default);
    Task AddAsync(BankStatement statement, CancellationToken ct = default);
    void Update(BankStatement statement);
    void AttachNewTransactions(IEnumerable<Aggregates.StatementAnalysis.StatementTransaction> transactions);
    void Delete(BankStatement statement);
}
