using CRMS.Domain.Aggregates.Advisory;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class CreditAdvisoryRepository : ICreditAdvisoryRepository
{
    private readonly CRMSDbContext _context;

    public CreditAdvisoryRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<CreditAdvisory?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.CreditAdvisories.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<CreditAdvisory?> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.CreditAdvisories
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .OrderByDescending(x => x.GeneratedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CreditAdvisory?> GetLatestByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.CreditAdvisories
            .Where(x => x.LoanApplicationId == loanApplicationId && x.Status == Domain.Enums.AdvisoryStatus.Completed)
            .OrderByDescending(x => x.GeneratedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<CreditAdvisory>> GetAllByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.CreditAdvisories
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .OrderByDescending(x => x.GeneratedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(CreditAdvisory advisory, CancellationToken ct = default)
    {
        await _context.CreditAdvisories.AddAsync(advisory, ct);
    }

    public void Update(CreditAdvisory advisory)
    {
        _context.CreditAdvisories.Update(advisory);
    }
}
