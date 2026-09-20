using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfLegalClearanceRepository : IRhshfLegalClearanceRepository
{
    private readonly CRMSDbContext _context;

    public RhshfLegalClearanceRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RhshfLegalClearance>> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default)
        => await _context.RhshfLegalClearances
            .Where(x => x.RhshfCreditProfileId == rhshfCreditProfileId && x.CycleNumber == cycleNumber)
            .OrderBy(x => x.ClearedAt)
            .ToListAsync(ct);

    public async Task AddAsync(RhshfLegalClearance clearance, CancellationToken ct = default)
        => await _context.RhshfLegalClearances.AddAsync(clearance, ct);
}
