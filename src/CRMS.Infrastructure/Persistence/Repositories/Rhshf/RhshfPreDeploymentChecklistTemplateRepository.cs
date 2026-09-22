using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfPreDeploymentChecklistTemplateRepository : IRhshfPreDeploymentChecklistTemplateRepository
{
    private readonly CRMSDbContext _context;

    public RhshfPreDeploymentChecklistTemplateRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RhshfPreDeploymentChecklistTemplate>> GetAllAsync(CancellationToken ct = default)
        => await _context.RhshfPreDeploymentChecklistTemplates.OrderBy(x => x.SortOrder).ToListAsync(ct);

    public async Task<IReadOnlyList<RhshfPreDeploymentChecklistTemplate>> GetActiveAsync(CancellationToken ct = default)
        => await _context.RhshfPreDeploymentChecklistTemplates
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

    public async Task<RhshfPreDeploymentChecklistTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.RhshfPreDeploymentChecklistTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddAsync(RhshfPreDeploymentChecklistTemplate template, CancellationToken ct = default)
        => await _context.RhshfPreDeploymentChecklistTemplates.AddAsync(template, ct);

    public void Update(RhshfPreDeploymentChecklistTemplate template)
        => _context.RhshfPreDeploymentChecklistTemplates.Update(template);
}
