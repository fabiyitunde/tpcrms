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
            .Include(x => x.Documents)
            .Where(x => x.RhshfCreditProfileId == rhshfCreditProfileId && x.CycleNumber == cycleNumber)
            .OrderByDescending(x => x.GeneratedAt)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(RhshfOffer offer, CancellationToken ct = default)
        => await _context.RhshfOffers.AddAsync(offer, ct);

    public async Task<RhshfOfferDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default)
        => await _context.RhshfOfferDocuments.FirstOrDefaultAsync(x => x.Id == documentId, ct);
}
