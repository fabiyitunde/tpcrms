using CRMS.Domain.Aggregates.OfferLetter;

namespace CRMS.Domain.Interfaces;

public interface IOfferLetterRepository
{
    Task<OfferLetter?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<OfferLetter?> GetLatestByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task<List<OfferLetter>> GetAllByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    /// <summary>Returns the highest version number ever persisted (including Failed records), or 0 if none exist.</summary>
    Task<int> GetMaxVersionAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task AddAsync(OfferLetter offerLetter, CancellationToken ct = default);
    void Update(OfferLetter offerLetter);
}
