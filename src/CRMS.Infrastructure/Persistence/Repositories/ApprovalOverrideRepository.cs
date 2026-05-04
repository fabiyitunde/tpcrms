using CRMS.Domain.Aggregates.LoanApplication;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class ApprovalOverrideRepository : IApprovalOverrideRepository
{
    private readonly CRMSDbContext _context;

    public ApprovalOverrideRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ApprovalOverrideRecord record, CancellationToken ct = default)
    {
        await _context.ApprovalOverrideRecords.AddAsync(record, ct);
    }

    public async Task<IReadOnlyList<ApprovalOverrideRecord>> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.ApprovalOverrideRecords
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<ApprovalOverrideRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.ApprovalOverrideRecords.FindAsync([id], ct);
    }

    public void Update(ApprovalOverrideRecord record)
    {
        _context.ApprovalOverrideRecords.Update(record);
    }
}
