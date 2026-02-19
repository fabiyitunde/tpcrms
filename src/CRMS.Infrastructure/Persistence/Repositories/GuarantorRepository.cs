using CRMS.Domain.Aggregates.Guarantor;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class GuarantorRepository : IGuarantorRepository
{
    private readonly CRMSDbContext _context;

    public GuarantorRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<Guarantor?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Guarantors.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Guarantor?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Guarantors
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyList<Guarantor>> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.Guarantors
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guarantor>> GetByBVNAsync(string bvn, CancellationToken ct = default)
    {
        return await _context.Guarantors
            .Where(x => x.BVN == bvn)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guarantor>> GetByStatusAsync(GuarantorStatus status, CancellationToken ct = default)
    {
        return await _context.Guarantors
            .Where(x => x.Status == status)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Guarantor?> GetByReferenceAsync(string reference, CancellationToken ct = default)
    {
        return await _context.Guarantors
            .FirstOrDefaultAsync(x => x.GuarantorReference == reference, ct);
    }

    public async Task<int> GetActiveGuaranteeCountByBVNAsync(string bvn, CancellationToken ct = default)
    {
        return await _context.Guarantors
            .CountAsync(x => x.BVN == bvn && x.Status == GuarantorStatus.Active, ct);
    }

    public async Task AddAsync(Guarantor guarantor, CancellationToken ct = default)
    {
        await _context.Guarantors.AddAsync(guarantor, ct);
    }

    public void Update(Guarantor guarantor)
    {
        _context.Guarantors.Update(guarantor);
    }

    public void Delete(Guarantor guarantor)
    {
        _context.Guarantors.Remove(guarantor);
    }
}
