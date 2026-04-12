using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using LA = CRMS.Domain.Aggregates.LoanApplication;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class LoanApplicationRepository : ILoanApplicationRepository
{
    private readonly CRMSDbContext _context;

    public LoanApplicationRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<LA.LoanApplication?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.LoanApplications
            .Include(x => x.Documents)
            .Include(x => x.Parties)
            .Include(x => x.Comments)
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<LA.LoanApplication?> GetByIdNoTrackingAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.LoanApplications
            .AsNoTracking()
            .Include(x => x.Documents)
            .Include(x => x.Parties)
            .Include(x => x.Comments)
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<LA.LoanApplication?> GetByIdWithChecklistAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.LoanApplications
            .Include(x => x.ChecklistItems)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<LA.LoanApplication?> GetByIdWithPartiesAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.LoanApplications
            .Include(x => x.Parties)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<LA.LoanApplication?> GetByApplicationNumberAsync(string applicationNumber, CancellationToken ct = default)
    {
        return await _context.LoanApplications
            .AsNoTracking()
            .Include(x => x.Documents)
            .Include(x => x.Parties)
            .Include(x => x.Comments)
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.ApplicationNumber == applicationNumber, ct);
    }

    public async Task<IReadOnlyList<LA.LoanApplication>> GetByStatusAsync(LoanApplicationStatus status, CancellationToken ct = default)
    {
        return await _context.LoanApplications
            .AsNoTracking()
            .Where(x => x.Status == status)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LA.LoanApplication>> GetByStatusFilteredAsync(
        LoanApplicationStatus status,
        IReadOnlyList<Guid>? visibleBranchIds,
        CancellationToken ct = default)
    {
        var query = _context.LoanApplications
            .AsNoTracking()
            .Where(x => x.Status == status);

        // null means global visibility (no filter)
        if (visibleBranchIds != null)
            query = query.Where(x => x.BranchId.HasValue && visibleBranchIds.Contains(x.BranchId.Value));

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LA.LoanApplication>> GetByInitiatorAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.LoanApplications
            .AsNoTracking()
            .Where(x => x.InitiatedByUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LA.LoanApplication>> GetPendingBranchReviewAsync(Guid? branchId = null, CancellationToken ct = default)
    {
        var query = _context.LoanApplications
            .AsNoTracking()
            .Where(x => x.Status == LoanApplicationStatus.BranchReview);

        if (branchId.HasValue)
            query = query.Where(x => x.BranchId == branchId);

        return await query
            .OrderBy(x => x.SubmittedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LA.LoanApplication>> GetPendingBranchReviewFilteredAsync(
        IReadOnlyList<Guid>? visibleBranchIds,
        CancellationToken ct = default)
    {
        var query = _context.LoanApplications
            .AsNoTracking()
            .Where(x => x.Status == LoanApplicationStatus.BranchReview);

        // null means global visibility (no filter)
        if (visibleBranchIds != null)
            query = query.Where(x => x.BranchId.HasValue && visibleBranchIds.Contains(x.BranchId.Value));

        return await query
            .OrderBy(x => x.SubmittedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LA.LoanApplication>> GetByAccountNumberAsync(string accountNumber, CancellationToken ct = default)
    {
        return await _context.LoanApplications
            .AsNoTracking()
            .Where(x => x.AccountNumber == accountNumber)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.LoanApplications.AnyAsync(x => x.Id == id, ct);
    }

    public async Task AddAsync(LA.LoanApplication application, CancellationToken ct = default)
    {
        await _context.LoanApplications.AddAsync(application, ct);
    }

    public void Update(LA.LoanApplication application)
    {
        // Disable auto-detect changes to prevent EF Core from prematurely discovering new child
        // entities (e.g. new StatusHistory entries) via collection snapshot diff and incorrectly
        // marking them as Modified (UPDATE) instead of Added (INSERT). We rely on the explicit
        // state assignments below, and DetectChanges will run correctly inside SaveChangesAsync.
        _context.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            var entry = _context.Entry(application);

            if (entry.State == EntityState.Detached)
            {
                // Entity not yet tracked — start tracking via Update(), then fix any new children
                // that EF Core would incorrectly mark as Modified instead of Added
                // (non-empty Guid keys look like existing rows to EF Core's graph traversal).
                var newStatusHistory = application.StatusHistory
                    .Where(h => _context.Entry(h).State == EntityState.Detached).ToList();
                var newComments = application.Comments
                    .Where(c => _context.Entry(c).State == EntityState.Detached).ToList();
                var newDocuments = application.Documents
                    .Where(d => _context.Entry(d).State == EntityState.Detached).ToList();
                var newChecklistItems = application.ChecklistItems
                    .Where(ci => _context.Entry(ci).State == EntityState.Detached).ToList();

                _context.LoanApplications.Update(application);

                foreach (var h in newStatusHistory)
                    _context.Entry(h).State = EntityState.Added;
                foreach (var c in newComments)
                    _context.Entry(c).State = EntityState.Added;
                foreach (var d in newDocuments)
                    _context.Entry(d).State = EntityState.Added;
                foreach (var ci in newChecklistItems)
                    _context.Entry(ci).State = EntityState.Added;
            }
            else
            {
                // Entity is already tracked (loaded via GetByIdAsync in the same DbContext scope).
                // EF Core's change tracker already detects all property changes on the aggregate root.
                // Do NOT call DbSet.Update() here — it would mark every related entity (Parties,
                // Documents, existing StatusHistory) as Modified, generating unnecessary UPDATEs
                // that fail with RowVersion/constraint errors and prevent SaveChanges from completing.
                //
                // Just explicitly attach any new child entities that haven't been tracked yet.
                foreach (var h in application.StatusHistory)
                    if (_context.Entry(h).State == EntityState.Detached)
                        _context.Entry(h).State = EntityState.Added;
                foreach (var c in application.Comments)
                    if (_context.Entry(c).State == EntityState.Detached)
                        _context.Entry(c).State = EntityState.Added;
                foreach (var d in application.Documents)
                    if (_context.Entry(d).State == EntityState.Detached)
                        _context.Entry(d).State = EntityState.Added;
                foreach (var ci in application.ChecklistItems)
                    if (_context.Entry(ci).State == EntityState.Detached)
                        _context.Entry(ci).State = EntityState.Added;
            }
        }
        finally
        {
            _context.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }
}

public class LoanApplicationDocumentRepository : ILoanApplicationDocumentRepository
{
    private readonly CRMSDbContext _context;

    public LoanApplicationDocumentRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(LA.LoanApplicationDocument document, CancellationToken ct = default)
    {
        await _context.Set<LA.LoanApplicationDocument>().AddAsync(document, ct);
    }

    public async Task<LA.LoanApplicationDocument?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Set<LA.LoanApplicationDocument>().FirstOrDefaultAsync(x => x.Id == id, ct);
    }
}
