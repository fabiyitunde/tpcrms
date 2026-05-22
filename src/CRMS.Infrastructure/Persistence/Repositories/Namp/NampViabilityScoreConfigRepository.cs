using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Namp;

public class NampViabilityScoreConfigRepository : INampViabilityScoreConfigRepository
{
    private readonly CRMSDbContext _context;

    public NampViabilityScoreConfigRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<NampViabilityScoreConfig>> GetAllAsync(CancellationToken ct = default)
        => await _context.NampViabilityScoreConfigs.ToListAsync(ct);

    public async Task<NampViabilityScoreConfig?> GetByRatingAsync(NampViabilityRating rating, CancellationToken ct = default)
        => await _context.NampViabilityScoreConfigs
            .FirstOrDefaultAsync(c => c.ViabilityRating == rating && c.IsActive, ct);

    public async Task<NampViabilityScoreConfig?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.NampViabilityScoreConfigs.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task AddAsync(NampViabilityScoreConfig config, CancellationToken ct = default)
        => await _context.NampViabilityScoreConfigs.AddAsync(config, ct);

    public void Update(NampViabilityScoreConfig config)
        => _context.NampViabilityScoreConfigs.Update(config);
}
