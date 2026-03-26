using CRMS.Domain.Aggregates.StatementAnalysis;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class BankStatementRepository : IBankStatementRepository
{
    private readonly CRMSDbContext _context;

    public BankStatementRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<BankStatement?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.BankStatements.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<BankStatement?> GetByIdWithTransactionsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.BankStatements
            .Include(x => x.Transactions)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyList<BankStatement>> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.BankStatements
            .Include(x => x.Transactions)
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .OrderByDescending(x => x.PeriodEnd)
            .ToListAsync(ct);
    }

    public async Task<bool> HasStatementsAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.BankStatements
            .AnyAsync(x => x.LoanApplicationId == loanApplicationId, ct);
    }

    public async Task<IReadOnlyList<BankStatement>> GetByAccountNumberAsync(string accountNumber, CancellationToken ct = default)
    {
        return await _context.BankStatements
            .Where(x => x.AccountNumber == accountNumber)
            .OrderByDescending(x => x.PeriodEnd)
            .ToListAsync(ct);
    }

    public async Task AddAsync(BankStatement statement, CancellationToken ct = default)
    {
        await _context.BankStatements.AddAsync(statement, ct);
    }

    public void Update(BankStatement statement)
    {
        var entry = _context.Entry(statement);

        if (entry.State == EntityState.Detached)
        {
            // Entity not yet tracked — capture new transactions first so they don't
            // get incorrectly marked as Modified (non-empty Guid keys look like existing rows).
            var newTransactions = statement.Transactions
                .Where(t => _context.Entry(t).State == EntityState.Detached).ToList();

            _context.BankStatements.Update(statement);

            foreach (var txn in newTransactions)
                _context.Entry(txn).State = EntityState.Added;
        }
        else
        {
            // Entity already tracked — EF Core detects property changes automatically.
            // Only attach new child transactions that aren't tracked yet.
            foreach (var txn in statement.Transactions)
                if (_context.Entry(txn).State == EntityState.Detached)
                    _context.Entry(txn).State = EntityState.Added;
        }
    }

    public void Delete(BankStatement statement)
    {
        _context.BankStatements.Remove(statement);
    }
}
