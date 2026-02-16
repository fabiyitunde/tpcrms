using CRMS.Domain.Aggregates.ProductCatalog;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class LoanProductRepository : ILoanProductRepository
{
    private readonly CRMSDbContext _context;

    public LoanProductRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<LoanProduct?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.LoanProducts
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<LoanProduct?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalizedCode = code.ToUpperInvariant();
        return await _context.LoanProducts
            .FirstOrDefaultAsync(p => p.Code == normalizedCode, ct);
    }

    public async Task<IReadOnlyList<LoanProduct>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.LoanProducts
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LoanProduct>> GetByStatusAsync(ProductStatus status, CancellationToken ct = default)
    {
        return await _context.LoanProducts
            .Where(p => p.Status == status)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LoanProduct>> GetByTypeAsync(LoanProductType type, CancellationToken ct = default)
    {
        return await _context.LoanProducts
            .Where(p => p.Type == type)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LoanProduct>> GetActiveByTypeAsync(LoanProductType type, CancellationToken ct = default)
    {
        return await _context.LoanProducts
            .Where(p => p.Type == type && p.Status == ProductStatus.Active)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsAsync(string code, CancellationToken ct = default)
    {
        var normalizedCode = code.ToUpperInvariant();
        return await _context.LoanProducts
            .AnyAsync(p => p.Code == normalizedCode, ct);
    }

    public async Task AddAsync(LoanProduct product, CancellationToken ct = default)
    {
        await _context.LoanProducts.AddAsync(product, ct);
    }

    public Task UpdateAsync(LoanProduct product, CancellationToken ct = default)
    {
        _context.LoanProducts.Update(product);
        return Task.CompletedTask;
    }
}
