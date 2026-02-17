using CRMS.Domain.Aggregates.LoanPack;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class LoanPackRepository : ILoanPackRepository
{
    private readonly CRMSDbContext _context;

    public LoanPackRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<LoanPack?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.LoanPacks.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<LoanPack?> GetLatestByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.LoanPacks
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .Where(x => x.Status == LoanPackStatus.Generated)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<LoanPack>> GetAllByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.LoanPacks
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .OrderByDescending(x => x.Version)
            .ToListAsync(ct);
    }

    public async Task<int> GetVersionCountAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.LoanPacks
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .CountAsync(ct);
    }

    public async Task AddAsync(LoanPack loanPack, CancellationToken ct = default)
    {
        await _context.LoanPacks.AddAsync(loanPack, ct);
    }

    public void Update(LoanPack loanPack)
    {
        _context.LoanPacks.Update(loanPack);
    }
}
