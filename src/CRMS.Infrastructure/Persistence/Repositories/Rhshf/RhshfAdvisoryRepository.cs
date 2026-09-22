using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfAdvisoryRepository : IRhshfAdvisoryRepository
{
    private readonly CRMSDbContext _context;

    public RhshfAdvisoryRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<RhshfAdvisory?> GetByRhshfCreditProfileIdAsync(Guid rhshfCreditProfileId, CancellationToken ct = default)
        => await _context.RhshfAdvisories
            .Where(x => x.RhshfCreditProfileId == rhshfCreditProfileId)
            .OrderByDescending(x => x.GeneratedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<RhshfAdvisory?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.RhshfAdvisories.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddAsync(RhshfAdvisory advisory, CancellationToken ct = default)
        => await _context.RhshfAdvisories.AddAsync(advisory, ct);

    public void Update(RhshfAdvisory advisory)
        => _context.RhshfAdvisories.Update(advisory);
}
