using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfCallbackAttemptRepository : IRhshfCallbackAttemptRepository
{
    private readonly CRMSDbContext _context;

    public RhshfCallbackAttemptRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RhshfCallbackAttempt>> GetDueAsync(DateTime asOf, CancellationToken ct = default)
        => await _context.RhshfCallbackAttempts
            .Where(x => !x.Succeeded && x.NextRetryAt != null && x.NextRetryAt <= asOf)
            .OrderBy(x => x.NextRetryAt)
            .ToListAsync(ct);

    public async Task AddAsync(RhshfCallbackAttempt attempt, CancellationToken ct = default)
        => await _context.RhshfCallbackAttempts.AddAsync(attempt, ct);
}
