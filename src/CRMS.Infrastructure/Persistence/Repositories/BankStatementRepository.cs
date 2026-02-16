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
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .OrderByDescending(x => x.PeriodEnd)
            .ToListAsync(ct);
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
        _context.BankStatements.Update(statement);
    }
}
