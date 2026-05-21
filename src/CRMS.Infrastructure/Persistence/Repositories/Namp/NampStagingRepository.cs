using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Namp;

public class NampStagingRepository : INampStagingRepository
{
    private readonly CRMSDbContext _context;

    public NampStagingRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<NampStagingRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.NampStagingRecords.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<NampStagingRecord?> GetByApplicationReferenceAsync(string applicationReference, CancellationToken ct = default)
        => await _context.NampStagingRecords.FirstOrDefaultAsync(x => x.ApplicationReference == applicationReference, ct);

    public async Task<IReadOnlyList<NampStagingRecord>> GetPendingRecallAsync(Guid? branchId = null, CancellationToken ct = default)
    {
        var query = _context.NampStagingRecords.Where(x => !x.IsRecalled);

        if (branchId.HasValue)
            query = query.Where(x => x.ResolvedBranchId == branchId.Value);

        return await query.OrderByDescending(x => x.ReceivedAt).ToListAsync(ct);
    }

    public async Task<bool> ExistsByReferenceAsync(string applicationReference, CancellationToken ct = default)
        => await _context.NampStagingRecords.AnyAsync(x => x.ApplicationReference == applicationReference, ct);

    public async Task AddAsync(NampStagingRecord record, CancellationToken ct = default)
        => await _context.NampStagingRecords.AddAsync(record, ct);

    public void Update(NampStagingRecord record)
        => _context.NampStagingRecords.Update(record);
}
