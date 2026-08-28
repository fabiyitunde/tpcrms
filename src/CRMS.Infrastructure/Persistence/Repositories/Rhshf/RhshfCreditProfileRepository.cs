using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfCreditProfileRepository : IRhshfCreditProfileRepository
{
    private readonly CRMSDbContext _context;

    public RhshfCreditProfileRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<RhshfCreditProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await Query().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<RhshfCreditProfile?> GetByReferenceAsync(string reference, CancellationToken ct = default)
        => await Query().FirstOrDefaultAsync(x => x.Reference == reference, ct);

    public async Task<RhshfCreditProfile?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default)
        => await Query().FirstOrDefaultAsync(x => x.SubmissionId == submissionId, ct);

    // All collections are eager-loaded on every read: any handler that appends to a collection
    // navigation on an already-persisted profile needs it tracked from the start, otherwise EF
    // Core mis-tracks the new child as Modified instead of Added (see CRMSDbContext.SaveChangesAsync's
    // compensating fix, mirroring the existing NAMP pattern).
    private IQueryable<RhshfCreditProfile> Query()
        => _context.RhshfCreditProfiles
            .Include(x => x.EopLines)
            .Include(x => x.IssuedTokens)
            .Include(x => x.SupportingDocuments)
            .Include(x => x.Appraisals)
            .Include(x => x.RiskReviews)
            .Include(x => x.Ratifications);

    public async Task AddAsync(RhshfCreditProfile profile, CancellationToken ct = default)
        => await _context.RhshfCreditProfiles.AddAsync(profile, ct);

    public async Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default)
    {
        var query = Query().Where(x => x.Status == RhshfCaseStatus.UnderReview && x.InternalStage == stage);
        if (branchId.HasValue)
            query = query.Where(x => x.ResolvedBranchId == branchId.Value);

        return await query.OrderBy(x => x.UpdatedAt).ToListAsync(ct);
    }
}
