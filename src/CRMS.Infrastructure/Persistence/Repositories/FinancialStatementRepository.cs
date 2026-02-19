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
        // For tracked entities, we need to explicitly handle new child entities
        // because EF Core won't automatically detect them when the parent is already tracked
        
        // Check and add BalanceSheet if it's new
        if (statement.BalanceSheet != null)
        {
            var bsEntry = _context.Entry(statement.BalanceSheet);
            // If state is Detached or Added with no tracking, explicitly add it
            if (bsEntry.State == EntityState.Detached)
            {
                _context.Set<BalanceSheet>().Add(statement.BalanceSheet);
            }
            else if (bsEntry.State == EntityState.Unchanged)
            {
                // Already tracked and unchanged - this is fine
            }
        }
        
        // Check and add IncomeStatement if it's new
        if (statement.IncomeStatement != null)
        {
            var isEntry = _context.Entry(statement.IncomeStatement);
            if (isEntry.State == EntityState.Detached)
            {
                _context.Set<IncomeStatement>().Add(statement.IncomeStatement);
            }
        }
        
        // Check and add CashFlowStatement if it's new
        if (statement.CashFlowStatement != null)
        {
            var cfEntry = _context.Entry(statement.CashFlowStatement);
            if (cfEntry.State == EntityState.Detached)
            {
                _context.Set<CashFlowStatement>().Add(statement.CashFlowStatement);
            }
        }
        
        // Mark the parent as modified if it's tracked
        var entry = _context.Entry(statement);
        if (entry.State == EntityState.Unchanged)
        {
            entry.State = EntityState.Modified;
        }
        else if (entry.State == EntityState.Detached)
        {
            _context.FinancialStatements.Update(statement);
        }
    }

    public void Delete(FinancialStatement statement)
    {
        _context.FinancialStatements.Remove(statement);
    }
}
