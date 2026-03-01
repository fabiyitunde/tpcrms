using CRMS.Domain.Aggregates.Consent;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class ConsentRecordRepository : IConsentRecordRepository
{
    private readonly CRMSDbContext _context;

    public ConsentRecordRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<ConsentRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.ConsentRecords.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<ConsentRecord?> GetValidConsentAsync(string subjectIdentifier, ConsentType consentType, CancellationToken ct = default)
    {
        // Search both BVN (individuals) and NIN (business RC numbers) fields
        return await _context.ConsentRecords
            .Where(c => (c.BVN == subjectIdentifier || c.NIN == subjectIdentifier)
                     && c.ConsentType == consentType 
                     && c.Status == ConsentStatus.Active
                     && c.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(c => c.ConsentGivenAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<ConsentRecord>> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.ConsentRecords
            .Where(c => c.LoanApplicationId == loanApplicationId)
            .OrderByDescending(c => c.ConsentGivenAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(ConsentRecord consent, CancellationToken ct = default)
    {
        await _context.ConsentRecords.AddAsync(consent, ct);
    }

    public void Update(ConsentRecord consent)
    {
        _context.ConsentRecords.Update(consent);
    }
}
