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
    //
    // AsSplitQuery is essential here: 13 collection Includes in a single SQL statement produce a
    // cartesian product (StatusHistory x PreDeploymentChecklist x StageConfirmations x Directors x
    // ...), which EF materialises and de-duplicates client-side — thousands of wide rows per profile
    // as those collections grow, and the dominant cause of slow RH-SHF page loads. Split query issues
    // one query per collection instead, so each returns only its real rows. Tracking and the
    // append-tracking fix above are unaffected (split queries still return tracked entities).
    private IQueryable<RhshfCreditProfile> Query()
        => _context.RhshfCreditProfiles
            .Include(x => x.EopLines)
            .Include(x => x.IssuedTokens)
            .Include(x => x.SupportingDocuments)
            .Include(x => x.Appraisals)
            .Include(x => x.RiskReviews)
            .Include(x => x.Ratifications)
            .Include(x => x.Disbursements)
            .Include(x => x.PreDeploymentChecklist)
            .Include(x => x.Directors)
            .Include(x => x.FarmPlans)
            .Include(x => x.FinancialAppraisals)
            .Include(x => x.StageConfirmations)
            .Include(x => x.StatusHistory)
            // The portal's itemised breakdown. Missing from this list when the breakdown was first
            // ingested, so every loaded case looked un-itemised: the offer letter printed the
            // crop-level "0 kg" fallback and listed already-financed charges as deductions.
            .Include(x => x.FinancingLines)
            .Include(x => x.FinancingCharges)
            .AsSplitQuery();

    public async Task AddAsync(RhshfCreditProfile profile, CancellationToken ct = default)
        => await _context.RhshfCreditProfiles.AddAsync(profile, ct);

    /// <summary>
    /// Filtered on stage alone, deliberately. It used to also require Status == UnderReview, which
    /// silently hid every post-disbursement case: booking flips the status to Approved while the stage
    /// moves to Active, so an Active queue would always have come back empty. The stage filter is
    /// sufficient on its own — every terminal path (declined, cancelled, returned to the FAC) clears
    /// InternalStage to null, so those cases cannot match any stage.
    /// </summary>
    public async Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default)
    {
        var query = Query().Where(x => x.InternalStage == stage);
        if (branchId.HasValue)
            query = query.Where(x => x.ResolvedBranchId == branchId.Value);

        return await query.OrderBy(x => x.UpdatedAt).ToListAsync(ct);
    }

    public async Task<RhshfSupportingDocument?> GetSupportingDocumentByIdAsync(Guid documentId, CancellationToken ct = default)
        => await _context.RhshfSupportingDocuments.FirstOrDefaultAsync(x => x.Id == documentId, ct);

    // A join projection rather than loading profiles: this runs on the FAC-facing documents stage,
    // where pulling whole profile graphs (13 collections each) to read one document row per case
    // would be absurd. AsNoTracking — the handler copies the bytes and creates a new row, it never
    // mutates the source.
    public async Task<IReadOnlyList<RhshfReusableDocument>> GetReusableDocumentsForFacAsync(
        Guid facId, Guid excludeProfileId, RhshfDocumentCategory category, CancellationToken ct = default)
        => await (from doc in _context.RhshfSupportingDocuments.AsNoTracking()
                  join profile in _context.RhshfCreditProfiles.AsNoTracking()
                      on doc.RhshfCreditProfileId equals profile.Id
                  where profile.FacId == facId
                        && profile.Id != excludeProfileId
                        && doc.Category == category
                  orderby doc.UploadedAt descending
                  select new RhshfReusableDocument(
                      doc.Id, doc.Category, doc.FileName, doc.ContentType, doc.StoragePath,
                      doc.SizeBytes, doc.UploadedAt, profile.Reference, profile.SessionName, profile.Status))
            .ToListAsync(ct);

    // No Includes — the admin "all" list only needs root columns, and this can return the whole book.
    public async Task<IReadOnlyList<RhshfCreditProfile>> GetAllForListAsync(Guid? branchId, CancellationToken ct = default)
    {
        var query = _context.RhshfCreditProfiles.AsNoTracking();
        if (branchId.HasValue)
            query = query.Where(x => x.ResolvedBranchId == branchId.Value);
        return await query.OrderByDescending(x => x.UpdatedAt).ToListAsync(ct);
    }
}
