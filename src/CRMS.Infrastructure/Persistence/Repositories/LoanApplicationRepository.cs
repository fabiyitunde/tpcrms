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

    public async Task<IReadOnlyList<LA.LoanApplication>> GetByAccountNumberAsync(string accountNumber, CancellationToken ct = default)
    {
        return await _context.LoanApplications
            .Where(x => x.AccountNumber == accountNumber)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(LA.LoanApplication application, CancellationToken ct = default)
    {
        await _context.LoanApplications.AddAsync(application, ct);
    }

    public void Update(LA.LoanApplication application)
    {
        _context.LoanApplications.Update(application);
    }
}
