using CRMS.Domain.Aggregates.FinancialStatement;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class FinancialStatementRepository : IFinancialStatementRepository
{
    private readonly CRMSDbContext _context;

    public FinancialStatementRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<FinancialStatement?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.FinancialStatements.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<FinancialStatement?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.FinancialStatements
            .Include(x => x.BalanceSheet)
            .Include(x => x.IncomeStatement)
            .Include(x => x.CashFlowStatement)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyList<FinancialStatement>> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.FinancialStatements
            .Include(x => x.BalanceSheet)
            .Include(x => x.IncomeStatement)
            .Include(x => x.CashFlowStatement)
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .OrderByDescending(x => x.FinancialYear)
            .ToListAsync(ct);
    }

    public async Task<FinancialStatement?> GetByLoanApplicationAndYearAsync(Guid loanApplicationId, int year, CancellationToken ct = default)
    {
        return await _context.FinancialStatements
            .Include(x => x.BalanceSheet)
            .Include(x => x.IncomeStatement)
            .Include(x => x.CashFlowStatement)
            .FirstOrDefaultAsync(x => x.LoanApplicationId == loanApplicationId && x.FinancialYear == year, ct);
    }

    public async Task AddAsync(FinancialStatement statement, CancellationToken ct = default)
    {
        await _context.FinancialStatements.AddAsync(statement, ct);
    }

    public void Update(FinancialStatement statement)
    {
        _context.FinancialStatements.Update(statement);
    }
}
