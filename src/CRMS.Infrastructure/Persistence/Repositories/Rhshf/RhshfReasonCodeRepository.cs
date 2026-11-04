using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfReasonCodeRepository : IRhshfReasonCodeRepository
{
    private readonly CRMSDbContext _context;

    public RhshfReasonCodeRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RhshfReasonCode>> GetAllAsync(CancellationToken ct = default)
        => await _context.RhshfReasonCodes.AsNoTracking()
            .OrderBy(x => x.Type).ThenBy(x => x.SortOrder).ThenBy(x => x.Code)
            .ToListAsync(ct);

    public Task<RhshfReasonCode?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalised = (code ?? string.Empty).Trim().ToUpperInvariant();
        return _context.RhshfReasonCodes.FirstOrDefaultAsync(x => x.Code == normalised, ct);
    }

    public async Task AddAsync(RhshfReasonCode reasonCode, CancellationToken ct = default)
        => await _context.RhshfReasonCodes.AddAsync(reasonCode, ct);
}
