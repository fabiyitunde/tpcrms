using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfEligibilityCheckRepository : IRhshfEligibilityCheckRepository
{
    private readonly CRMSDbContext _context;

    public RhshfEligibilityCheckRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RhshfEligibilityCheck>> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default)
        => await _context.RhshfEligibilityChecks
            .Where(x => x.RhshfCreditProfileId == rhshfCreditProfileId && x.CycleNumber == cycleNumber)
            .OrderBy(x => x.Criterion)
            .ToListAsync(ct);

    public async Task AddAsync(RhshfEligibilityCheck check, CancellationToken ct = default)
        => await _context.RhshfEligibilityChecks.AddAsync(check, ct);
}
