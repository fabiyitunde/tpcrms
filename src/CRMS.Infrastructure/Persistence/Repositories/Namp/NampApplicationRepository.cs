using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Namp;

public class NampApplicationRepository : INampApplicationRepository
{
    private readonly CRMSDbContext _context;

    public NampApplicationRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<NampApplication?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.NampApplications.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<NampApplication?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await _context.NampApplications
            .Include(x => x.Documents)
            .Include(x => x.StatusHistory.OrderByDescending(h => h.ChangedAt))
            .Include(x => x.Guarantors)
            .Include(x => x.Collaterals)
            .Include(x => x.FinancialStatements)
            .Include(x => x.PreDeploymentChecklist.OrderBy(i => i.SortOrder))
            .Include(x => x.Directors)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<NampApplication?> GetByCommitteeReviewIdAsync(Guid committeeReviewId, CancellationToken ct = default)
        => await _context.NampApplications
            .Include(x => x.Documents)
            .Include(x => x.StatusHistory.OrderByDescending(h => h.ChangedAt))
            .Include(x => x.Guarantors)
            .Include(x => x.Collaterals)
            .Include(x => x.FinancialStatements)
            .Include(x => x.PreDeploymentChecklist.OrderBy(i => i.SortOrder))
            .Include(x => x.Directors)
            .FirstOrDefaultAsync(x => x.CurrentCommitteeReviewId == committeeReviewId, ct);

    public async Task<NampApplication?> GetByApplicationReferenceAsync(string applicationReference, CancellationToken ct = default)
        => await _context.NampApplications.FirstOrDefaultAsync(x => x.ApplicationReference == applicationReference, ct);

    public async Task<NampApplication?> GetByApplicationReferenceWithHistoryAsync(string applicationReference, CancellationToken ct = default)
        => await _context.NampApplications
            .Include(x => x.StatusHistory.OrderBy(h => h.ChangedAt))
            .FirstOrDefaultAsync(x => x.ApplicationReference == applicationReference, ct);

    public async Task<NampApplication?> GetByApplicationNumberWithHistoryAsync(string applicationNumber, CancellationToken ct = default)
        => await _context.NampApplications
            .Include(x => x.StatusHistory.OrderBy(h => h.ChangedAt))
            .FirstOrDefaultAsync(x => x.ApplicationNumber == applicationNumber, ct);

    public async Task<IReadOnlyList<NampApplication>> GetByStatusAsync(NampApplicationStatus status, Guid? branchId = null, CancellationToken ct = default)
    {
        var query = _context.NampApplications.Where(x => x.Status == status);
        if (branchId.HasValue)
            query = query.Where(x => x.BranchId == branchId.Value);
        return await query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<NampApplication>> GetByStatusAndTierAsync(NampApplicationStatus status, NampCommitteeTier tier, Guid? branchId = null, CancellationToken ct = default)
    {
        var query = _context.NampApplications
            .Where(x => x.Status == status && x.CommitteeTier == tier);
        if (branchId.HasValue)
            query = query.Where(x => x.BranchId == branchId.Value);
        return await query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<NampApplication>> GetByBranchAsync(Guid branchId, CancellationToken ct = default)
        => await _context.NampApplications
            .Where(x => x.BranchId == branchId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<NampApplication>> GetByCommitteeMembershipAsync(Guid userId, CancellationToken ct = default)
    {
        var reviewIds = await _context.CommitteeMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.CommitteeReviewId)
            .ToListAsync(ct);

        if (reviewIds.Count == 0) return [];

        return await _context.NampApplications
            .Where(a => a.CurrentCommitteeReviewId.HasValue && reviewIds.Contains(a.CurrentCommitteeReviewId.Value))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<NampApplication>> GetByParticipationAsync(Guid userId, CancellationToken ct = default)
    {
        // Find all application IDs where this user has taken any action (captured in status history)
        var appIds = await _context.NampStatusHistory
            .Where(h => h.ChangedByUserId == userId)
            .Select(h => h.NampApplicationId)
            .Distinct()
            .ToListAsync(ct);

        if (appIds.Count == 0) return [];

        return await _context.NampApplications
            .Where(a => appIds.Contains(a.Id))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(NampApplication application, CancellationToken ct = default)
        => await _context.NampApplications.AddAsync(application, ct);

    public void Update(NampApplication application)
        => _context.NampApplications.Update(application);

    public async Task AddNampDocumentAsync(NampDocument document, CancellationToken ct = default)
        => await _context.NampDocuments.AddAsync(document, ct);

    public async Task<NampDocument?> GetNampDocumentByIdAsync(Guid documentId, CancellationToken ct = default)
        => await _context.NampDocuments.FirstOrDefaultAsync(d => d.Id == documentId, ct);

    public async Task AddDirectorAsync(NampDirector director, CancellationToken ct = default)
        => await _context.NampDirectors.AddAsync(director, ct);

    public async Task<NampDirector?> GetDirectorByIdAsync(Guid directorId, CancellationToken ct = default)
        => await _context.NampDirectors.FirstOrDefaultAsync(d => d.Id == directorId, ct);

    public void RemoveDirector(NampDirector director)
        => _context.NampDirectors.Remove(director);

    public async Task<NampFinancialAppraisalReport?> GetFinancialAppraisalReportAsync(Guid nampApplicationId, CancellationToken ct = default)
        => await _context.NampFinancialAppraisalReports
            .FirstOrDefaultAsync(r => r.NampApplicationId == nampApplicationId, ct);

    public async Task AddFinancialAppraisalReportAsync(NampFinancialAppraisalReport report, CancellationToken ct = default)
        => await _context.NampFinancialAppraisalReports.AddAsync(report, ct);

    public async Task AddPreDeploymentChecklistItemsAsync(IEnumerable<NampPreDeploymentChecklistItem> items, CancellationToken ct = default)
        => await _context.NampPreDeploymentChecklistItems.AddRangeAsync(items, ct);
}
