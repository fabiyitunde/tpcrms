using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfCollateralRepository : IRhshfCollateralRepository
{
    private readonly CRMSDbContext _context;

    public RhshfCollateralRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RhshfCollateral>> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default)
        => await _context.RhshfCollaterals
            .Include(x => x.Documents)
            .Where(x => x.RhshfCreditProfileId == rhshfCreditProfileId && x.CycleNumber == cycleNumber)
            .OrderBy(x => x.RecordedAt)
            .ToListAsync(ct);

    public async Task<RhshfCollateral?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.RhshfCollaterals.Include(x => x.Documents).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddAsync(RhshfCollateral collateral, CancellationToken ct = default)
        => await _context.RhshfCollaterals.AddAsync(collateral, ct);

    public void Remove(RhshfCollateral collateral)
        => _context.RhshfCollaterals.Remove(collateral);

    public async Task<RhshfCollateralDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default)
        => await _context.RhshfCollateralDocuments.FirstOrDefaultAsync(x => x.Id == documentId, ct);
}
