using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfGuarantorRepository : IRhshfGuarantorRepository
{
    private readonly CRMSDbContext _context;

    public RhshfGuarantorRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RhshfGuarantor>> GetByProfileIdAsync(Guid rhshfCreditProfileId, CancellationToken ct = default)
        => await _context.RhshfGuarantors
            .Where(g => g.RhshfCreditProfileId == rhshfCreditProfileId)
            .OrderBy(g => g.FullName)
            .ToListAsync(ct);

    public async Task<RhshfGuarantor?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.RhshfGuarantors.FirstOrDefaultAsync(g => g.Id == id, ct);

    public async Task AddAsync(RhshfGuarantor guarantor, CancellationToken ct = default)
        => await _context.RhshfGuarantors.AddAsync(guarantor, ct);

    public void Remove(RhshfGuarantor guarantor)
        => _context.RhshfGuarantors.Remove(guarantor);
}
