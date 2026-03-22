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

    public async Task<OfferLetter?> GetLatestByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.OfferLetters
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .Where(x => x.Status == OfferLetterStatus.Generated)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> GetVersionCountAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.OfferLetters
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .CountAsync(ct);
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
