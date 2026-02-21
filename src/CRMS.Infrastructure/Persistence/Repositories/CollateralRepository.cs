using CRMS.Domain.Aggregates.Collateral;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class CollateralRepository : ICollateralRepository
{
    private readonly CRMSDbContext _context;

    public CollateralRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<Collateral?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Collaterals.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Collateral?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Collaterals
            .Include(x => x.Valuations)
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyList<Collateral>> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.Collaterals
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Collateral>> GetByStatusAsync(CollateralStatus status, CancellationToken ct = default)
    {
        return await _context.Collaterals
            .Where(x => x.Status == status)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Collateral?> GetByReferenceAsync(string reference, CancellationToken ct = default)
    {
        return await _context.Collaterals
            .FirstOrDefaultAsync(x => x.CollateralReference == reference, ct);
    }

    public async Task AddAsync(Collateral collateral, CancellationToken ct = default)
    {
        await _context.Collaterals.AddAsync(collateral, ct);
    }

    public void Update(Collateral collateral)
    {
        _context.Collaterals.Update(collateral);
    }

    public void Delete(Collateral collateral)
    {
        _context.Collaterals.Remove(collateral);
    }
}

public class CollateralDocumentRepository : ICollateralDocumentRepository
{
    private readonly CRMSDbContext _context;

    public CollateralDocumentRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<CollateralDocument?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.CollateralDocuments.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyList<CollateralDocument>> GetByCollateralIdAsync(Guid collateralId, CancellationToken ct = default)
    {
        return await _context.CollateralDocuments
            .Where(x => x.CollateralId == collateralId)
            .OrderByDescending(x => x.UploadedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(CollateralDocument document, CancellationToken ct = default)
    {
        await _context.CollateralDocuments.AddAsync(document, ct);
    }

    public void Delete(CollateralDocument document)
    {
        _context.CollateralDocuments.Remove(document);
    }
}
