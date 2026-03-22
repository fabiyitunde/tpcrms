using CRMS.Domain.Aggregates.OfferLetter;

namespace CRMS.Domain.Interfaces;

public interface IOfferLetterRepository
{
    Task<OfferLetter?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<OfferLetter?> GetLatestByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task<int> GetVersionCountAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task AddAsync(OfferLetter offerLetter, CancellationToken ct = default);
    void Update(OfferLetter offerLetter);
}
