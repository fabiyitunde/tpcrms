using CRMS.Domain.Aggregates.Committee;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Committee;

public class StandingCommitteeRepository : IStandingCommitteeRepository
{
    private readonly CRMSDbContext _context;

    public StandingCommitteeRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<StandingCommittee?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.StandingCommittees
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<StandingCommittee?> GetByCommitteeTypeAsync(CommitteeType type, CancellationToken ct = default)
    {
        return await _context.StandingCommittees
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.CommitteeType == type && c.IsActive, ct);
    }

    public async Task<StandingCommittee?> GetForAmountAsync(decimal amount, CancellationToken ct = default)
    {
        return await _context.StandingCommittees
            .Include(c => c.Members)
            .Where(c => c.IsActive
                && c.MinAmountThreshold <= amount
                && (!c.MaxAmountThreshold.HasValue || c.MaxAmountThreshold.Value >= amount))
            .OrderByDescending(c => c.MinAmountThreshold)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<StandingCommittee>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var query = _context.StandingCommittees
            .Include(c => c.Members)
            .AsQueryable();

        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        return await query.OrderBy(c => c.MinAmountThreshold).ToListAsync(ct);
    }

    public async Task AddAsync(StandingCommittee committee, CancellationToken ct = default)
    {
        await _context.StandingCommittees.AddAsync(committee, ct);
    }

    public void Update(StandingCommittee committee)
    {
        // Disable auto-detect changes to prevent EF Core from prematurely discovering new child
        // entities (new members) via collection snapshot diff and incorrectly marking them as
        // Modified (UPDATE) instead of Added (INSERT). Same pattern as LoanApplicationRepository.
        _context.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            var entry = _context.Entry(committee);

            if (entry.State == EntityState.Detached)
            {
                // Entity not yet tracked — start tracking via Update(), then fix any new members
                // that EF Core would incorrectly mark as Modified (non-empty Guid keys look like
                // existing rows to EF Core's graph traversal).
                var newMembers = committee.Members
                    .Where(m => _context.Entry(m).State == EntityState.Detached).ToList();

                _context.StandingCommittees.Update(committee);

                foreach (var m in newMembers)
                    _context.Entry(m).State = EntityState.Added;
            }
            else
            {
                // Entity already tracked — explicitly mark new members as Added so SaveChanges
                // generates INSERT instead of UPDATE.
                foreach (var m in committee.Members)
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
