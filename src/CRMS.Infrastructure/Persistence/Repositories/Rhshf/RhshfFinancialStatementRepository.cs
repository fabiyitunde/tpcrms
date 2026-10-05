using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfFinancialStatementRepository : IRhshfFinancialStatementRepository
{
    private readonly CRMSDbContext _context;

    public RhshfFinancialStatementRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RhshfFinancialStatement>> GetByProfileIdAsync(Guid rhshfCreditProfileId, CancellationToken ct = default)
        => await _context.RhshfFinancialStatements
            .Where(s => s.RhshfCreditProfileId == rhshfCreditProfileId)
            .OrderByDescending(s => s.FinancialYear)
            .ToListAsync(ct);

    public async Task<RhshfFinancialStatement?> GetByProfileAndYearAsync(Guid rhshfCreditProfileId, int financialYear, CancellationToken ct = default)
        => await _context.RhshfFinancialStatements
            .FirstOrDefaultAsync(s => s.RhshfCreditProfileId == rhshfCreditProfileId && s.FinancialYear == financialYear, ct);

    public async Task<RhshfFinancialStatement?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.RhshfFinancialStatements.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task AddAsync(RhshfFinancialStatement statement, CancellationToken ct = default)
        => await _context.RhshfFinancialStatements.AddAsync(statement, ct);

    public void Remove(RhshfFinancialStatement statement)
        => _context.RhshfFinancialStatements.Remove(statement);
}
