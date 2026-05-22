using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Namp;

public class NampAdvisoryRepository : INampAdvisoryRepository
{
    private readonly CRMSDbContext _context;

    public NampAdvisoryRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<NampAdvisory?> GetByNampApplicationIdAsync(Guid nampApplicationId, CancellationToken ct = default)
        => await _context.NampAdvisories
            .Where(a => a.NampApplicationId == nampApplicationId)
            .OrderByDescending(a => a.GeneratedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<NampAdvisory?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.NampAdvisories.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task AddAsync(NampAdvisory advisory, CancellationToken ct = default)
        => await _context.NampAdvisories.AddAsync(advisory, ct);

    public void Update(NampAdvisory advisory)
        => _context.NampAdvisories.Update(advisory);
}
