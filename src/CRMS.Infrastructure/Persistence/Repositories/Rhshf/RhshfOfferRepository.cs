using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfOfferRepository : IRhshfOfferRepository
{
    private readonly CRMSDbContext _context;

    public RhshfOfferRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<RhshfOffer?> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default)
        => await _context.RhshfOffers
            .FirstOrDefaultAsync(x => x.RhshfCreditProfileId == rhshfCreditProfileId && x.CycleNumber == cycleNumber, ct);

    public async Task AddAsync(RhshfOffer offer, CancellationToken ct = default)
        => await _context.RhshfOffers.AddAsync(offer, ct);
}
