using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfScheduleFarmerRepository : IRhshfScheduleFarmerRepository
{
    private readonly CRMSDbContext _context;

    public RhshfScheduleFarmerRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public Task<int> CountForProfileAsync(Guid rhshfCreditProfileId, CancellationToken ct = default)
        => _context.RhshfScheduleFarmers.CountAsync(x => x.RhshfCreditProfileId == rhshfCreditProfileId, ct);

    public Task AddRangeAsync(IEnumerable<RhshfScheduleFarmer> farmers, CancellationToken ct = default)
        => _context.RhshfScheduleFarmers.AddRangeAsync(farmers, ct);
}
