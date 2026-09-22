using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfDocumentRequirementRepository : IRhshfDocumentRequirementRepository
{
    private readonly CRMSDbContext _context;

    public RhshfDocumentRequirementRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RhshfDocumentRequirement>> GetAllAsync(CancellationToken ct = default)
        => await _context.RhshfDocumentRequirements.OrderBy(x => x.SortOrder).ToListAsync(ct);

    public async Task<IReadOnlyList<RhshfDocumentRequirement>> GetActiveAsync(CancellationToken ct = default)
        => await _context.RhshfDocumentRequirements
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

    public async Task<RhshfDocumentRequirement?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.RhshfDocumentRequirements.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddAsync(RhshfDocumentRequirement requirement, CancellationToken ct = default)
        => await _context.RhshfDocumentRequirements.AddAsync(requirement, ct);

    public void Update(RhshfDocumentRequirement requirement)
        => _context.RhshfDocumentRequirements.Update(requirement);
}
