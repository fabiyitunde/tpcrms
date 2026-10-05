using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfCommentRepository : IRhshfCommentRepository
{
    private readonly CRMSDbContext _context;

    public RhshfCommentRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RhshfComment>> GetByProfileIdAsync(Guid rhshfCreditProfileId, CancellationToken ct = default)
        => await _context.RhshfComments
            .Where(c => c.RhshfCreditProfileId == rhshfCreditProfileId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(RhshfComment comment, CancellationToken ct = default)
        => await _context.RhshfComments.AddAsync(comment, ct);
}
