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

    /// <summary>Read fresh, never from this context's memory: the sender updates these rows from another
    /// context, and a staff session that queued a re-send kept showing it as waiting after the portal had
    /// accepted it, and would have refused a second re-send as still queued (CRMSQA#2).</summary>
    public async Task<IReadOnlyList<RhshfCallbackAttempt>> GetByProfileAsync(Guid rhshfCreditProfileId, CancellationToken ct = default)
        => await _context.RhshfCallbackAttempts
            .AsNoTracking()
            .Where(x => x.RhshfCreditProfileId == rhshfCreditProfileId)
            .ToListAsync(ct);

    public async Task AddAsync(RhshfCallbackAttempt attempt, CancellationToken ct = default)
        => await _context.RhshfCallbackAttempts.AddAsync(attempt, ct);
}
