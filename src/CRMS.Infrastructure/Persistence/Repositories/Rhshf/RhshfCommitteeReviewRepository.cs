using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfCommitteeReviewRepository : IRhshfCommitteeReviewRepository
{
    private readonly CRMSDbContext _context;

    public RhshfCommitteeReviewRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<RhshfCommitteeReview?> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default)
        => await _context.RhshfCommitteeReviews
            .Include(x => x.Votes)
            .FirstOrDefaultAsync(x => x.RhshfCreditProfileId == rhshfCreditProfileId && x.CycleNumber == cycleNumber, ct);

    public async Task AddAsync(RhshfCommitteeReview review, CancellationToken ct = default)
        => await _context.RhshfCommitteeReviews.AddAsync(review, ct);
}
