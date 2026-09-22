using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfAppraisalThresholdsRepository : IRhshfAppraisalThresholdsRepository
{
    private readonly CRMSDbContext _context;

    public RhshfAppraisalThresholdsRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<RhshfAppraisalThresholds?> GetActiveAsync(CancellationToken ct = default)
        => await _context.RhshfAppraisalThresholds
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(RhshfAppraisalThresholds thresholds, CancellationToken ct = default)
        => await _context.RhshfAppraisalThresholds.AddAsync(thresholds, ct);

    public void Update(RhshfAppraisalThresholds thresholds)
        => _context.RhshfAppraisalThresholds.Update(thresholds);
}
