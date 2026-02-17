using CRMS.Domain.Aggregates.CreditBureau;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class BureauReportRepository : IBureauReportRepository
{
    private readonly CRMSDbContext _context;

    public BureauReportRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<BureauReport?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.BureauReports.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<BureauReport?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.BureauReports
            .Include(x => x.Accounts)
            .Include(x => x.ScoreFactors)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyList<BureauReport>> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.BureauReports
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .OrderByDescending(x => x.RequestedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BureauReport>> GetByBVNAsync(string bvn, CancellationToken ct = default)
    {
        return await _context.BureauReports
            .Where(x => x.BVN == bvn)
            .OrderByDescending(x => x.RequestedAt)
            .ToListAsync(ct);
    }

    public async Task<BureauReport?> GetLatestByBVNAsync(string bvn, CreditBureauProvider? provider = null, CancellationToken ct = default)
    {
        var query = _context.BureauReports
            .Where(x => x.BVN == bvn && x.Status == BureauReportStatus.Completed);

        if (provider.HasValue)
            query = query.Where(x => x.Provider == provider.Value);

        return await query
            .OrderByDescending(x => x.CompletedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(BureauReport report, CancellationToken ct = default)
    {
        await _context.BureauReports.AddAsync(report, ct);
    }

    public void Update(BureauReport report)
    {
        _context.BureauReports.Update(report);
    }
}
