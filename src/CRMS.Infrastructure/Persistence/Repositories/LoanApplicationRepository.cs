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

    public async Task<LA.LoanApplication?> GetByIdWithPartiesAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.LoanApplications
            .Include(x => x.Parties)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<LA.LoanApplication?> GetByApplicationNumberAsync(string applicationNumber, CancellationToken ct = default)
    {
        return await _context.LoanApplications
            .Include(x => x.Documents)
            .Include(x => x.Parties)
            .Include(x => x.Comments)
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.ApplicationNumber == applicationNumber, ct);
    }

    public async Task<IReadOnlyList<LA.LoanApplication>> GetByStatusAsync(LoanApplicationStatus status, CancellationToken ct = default)
    {
        return await _context.LoanApplications
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
            .Where(x => x.InitiatedByUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LA.LoanApplication>> GetPendingBranchReviewAsync(Guid? branchId = null, CancellationToken ct = default)
    {
        var query = _context.LoanApplications
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
        // Capture new (untracked) child entities BEFORE Update() processes the graph.
        // EF Core marks entities with non-empty Guid keys as Modified (not Added),
        // because it can't distinguish "new with user-assigned key" from "existing".
        var newStatusHistory = application.StatusHistory
            .Where(h => _context.Entry(h).State == EntityState.Detached)
            .ToList();
        var newComments = application.Comments
            .Where(c => _context.Entry(c).State == EntityState.Detached)
            .ToList();
        var newDocuments = application.Documents
            .Where(d => _context.Entry(d).State == EntityState.Detached)
            .ToList();

        _context.LoanApplications.Update(application);

        // Re-mark new children as Added so they get INSERTed, not UPDATE-ignored.
        foreach (var h in newStatusHistory)
            _context.Entry(h).State = EntityState.Added;
        foreach (var c in newComments)
            _context.Entry(c).State = EntityState.Added;
        foreach (var d in newDocuments)
            _context.Entry(d).State = EntityState.Added;
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
