using CRMS.Domain.Aggregates.OfferLetter;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class OfferLetterRepository : IOfferLetterRepository
{
    private readonly CRMSDbContext _context;

    public OfferLetterRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<OfferLetter?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.OfferLetters.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<List<OfferLetter>> GetAllByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.OfferLetters
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .OrderByDescending(x => x.Version)
            .ToListAsync(ct);
    }

    public async Task<OfferLetter?> GetLatestByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.OfferLetters
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .Where(x => x.Status == OfferLetterStatus.Generated)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> GetMaxVersionAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        // MaxAsync returns null when there are no rows; default to 0 so callers get next version = 1.
        return await _context.OfferLetters
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .MaxAsync(x => (int?)x.Version, ct) ?? 0;
    }

    public async Task AddAsync(OfferLetter offerLetter, CancellationToken ct = default)
    {
        await _context.OfferLetters.AddAsync(offerLetter, ct);
    }

    public void Update(OfferLetter offerLetter)
    {
        _context.OfferLetters.Update(offerLetter);
    }
}
