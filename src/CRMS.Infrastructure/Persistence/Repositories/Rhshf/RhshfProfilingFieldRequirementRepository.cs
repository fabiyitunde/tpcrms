using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfProfilingFieldRequirementRepository : IRhshfProfilingFieldRequirementRepository
{
    private readonly CRMSDbContext _context;

    public RhshfProfilingFieldRequirementRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RhshfProfilingFieldRequirement>> GetAllAsync(CancellationToken ct = default)
        => await _context.RhshfProfilingFieldRequirements.OrderBy(x => x.SortOrder).ToListAsync(ct);

    public async Task<RhshfProfilingFieldRequirement?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.RhshfProfilingFieldRequirements.FirstOrDefaultAsync(x => x.Id == id, ct);
}
