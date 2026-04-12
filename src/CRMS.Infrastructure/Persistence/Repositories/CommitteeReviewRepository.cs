using CRMS.Domain.Aggregates.Committee;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class CommitteeReviewRepository : ICommitteeReviewRepository
{
    private readonly CRMSDbContext _context;

    public CommitteeReviewRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<CommitteeReview?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.CommitteeReviews
            .Include(r => r.Members)
            .Include(r => r.Comments.OrderByDescending(c => c.CreatedAt))
            .Include(r => r.Documents)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<CommitteeReview?> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.CommitteeReviews
            .Include(r => r.Members)
            .Include(r => r.Comments.OrderByDescending(c => c.CreatedAt))
            .Include(r => r.Documents)
            .OrderByDescending(r => r.CirculatedAt)
            .FirstOrDefaultAsync(r => r.LoanApplicationId == loanApplicationId, ct);
    }

    public async Task<CommitteeReview?> GetActiveByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.CommitteeReviews
            .Include(r => r.Members)
            .Include(r => r.Comments.OrderByDescending(c => c.CreatedAt))
            .Include(r => r.Documents)
            .Where(r => r.LoanApplicationId == loanApplicationId && 
                        r.Status != CommitteeReviewStatus.Closed)
            .OrderByDescending(r => r.CirculatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<CommitteeReview>> GetByStatusAsync(CommitteeReviewStatus status, CancellationToken ct = default)
    {
        return await _context.CommitteeReviews
            .Include(r => r.Members)
            .Where(r => r.Status == status)
            .OrderBy(r => r.DeadlineAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CommitteeReview>> GetByMemberUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.CommitteeReviews
            .Include(r => r.Members)
            .Include(r => r.Comments.OrderByDescending(c => c.CreatedAt).Take(5))
            .Where(r => r.Members.Any(m => m.UserId == userId) && r.Status != CommitteeReviewStatus.Closed)
            .OrderBy(r => r.DeadlineAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CommitteeReview>> GetPendingVotesByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.CommitteeReviews
            .Include(r => r.Members)
            .Where(r => r.Status == CommitteeReviewStatus.InProgress &&
                        r.Members.Any(m => m.UserId == userId && m.Vote == null))
            .OrderBy(r => r.DeadlineAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CommitteeReview>> GetOverdueAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _context.CommitteeReviews
            .Include(r => r.Members)
            .Where(r => r.Status == CommitteeReviewStatus.InProgress &&
                        r.DeadlineAt.HasValue && r.DeadlineAt < now)
            .OrderBy(r => r.DeadlineAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CommitteeReview>> GetByCommitteeTypeAsync(CommitteeType type, CancellationToken ct = default)
    {
        return await _context.CommitteeReviews
            .Include(r => r.Members)
            .Where(r => r.CommitteeType == type && r.Status != CommitteeReviewStatus.Closed)
            .OrderBy(r => r.DeadlineAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(CommitteeReview review, CancellationToken ct = default)
    {
        await _context.CommitteeReviews.AddAsync(review, ct);
    }

    public void Update(CommitteeReview review)
    {
        // Disable auto-detect changes to prevent EF Core from prematurely discovering new child
        // entities (new members) via collection snapshot diff and incorrectly marking them as
        // Modified (UPDATE) instead of Added (INSERT). Same pattern as LoanApplicationRepository
        // and StandingCommitteeRepository.
        _context.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            var entry = _context.Entry(review);

            if (entry.State == EntityState.Detached)
            {
                // Entity not yet tracked — start tracking via Update(), then fix any new members
                // that EF Core would incorrectly mark as Modified (non-empty Guid keys look like
                // existing rows to EF Core's graph traversal).
                var newMembers = review.Members
                    .Where(m => _context.Entry(m).State == EntityState.Detached).ToList();

                _context.CommitteeReviews.Update(review);

                foreach (var m in newMembers)
                    _context.Entry(m).State = EntityState.Added;
            }
            else
            {
                // Entity already tracked — explicitly mark new members as Added so SaveChanges
                // generates INSERT instead of UPDATE.
                foreach (var m in review.Members)
                    if (_context.Entry(m).State == EntityState.Detached)
                        _context.Entry(m).State = EntityState.Added;
            }
        }
        finally
        {
            _context.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }
}
