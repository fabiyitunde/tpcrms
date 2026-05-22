using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Namp;

public class NampPreDeploymentChecklistTemplateRepository : INampPreDeploymentChecklistTemplateRepository
{
    private readonly CRMSDbContext _context;

    public NampPreDeploymentChecklistTemplateRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<NampPreDeploymentChecklistTemplate>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.NampPreDeploymentChecklistTemplates
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Title)
            .ToListAsync(ct);
    }

    public async Task<NampPreDeploymentChecklistTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.NampPreDeploymentChecklistTemplates
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task AddAsync(NampPreDeploymentChecklistTemplate template, CancellationToken ct = default)
    {
        await _context.NampPreDeploymentChecklistTemplates.AddAsync(template, ct);
    }
}
