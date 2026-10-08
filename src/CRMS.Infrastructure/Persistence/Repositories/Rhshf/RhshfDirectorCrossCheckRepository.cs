using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfDirectorCrossCheckRepository : IRhshfDirectorCrossCheckRepository
{
    private readonly CRMSDbContext _context;

    public RhshfDirectorCrossCheckRepository(CRMSDbContext context) => _context = context;

    public async Task<RhshfDirectorCrossCheck?> GetByProfileIdAsync(Guid rhshfCreditProfileId, CancellationToken ct = default)
        => await _context.RhshfDirectorCrossChecks
            .FirstOrDefaultAsync(x => x.RhshfCreditProfileId == rhshfCreditProfileId, ct);

    public async Task AddAsync(RhshfDirectorCrossCheck snapshot, CancellationToken ct = default)
        => await _context.RhshfDirectorCrossChecks.AddAsync(snapshot, ct);

    public void Update(RhshfDirectorCrossCheck snapshot)
        => _context.RhshfDirectorCrossChecks.Update(snapshot);
}
