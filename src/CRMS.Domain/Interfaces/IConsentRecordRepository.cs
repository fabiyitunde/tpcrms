using CRMS.Domain.Aggregates.Consent;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface IConsentRecordRepository
{
    Task<ConsentRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ConsentRecord?> GetValidConsentAsync(string bvn, ConsentType consentType, CancellationToken ct = default);
    Task<List<ConsentRecord>> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task AddAsync(ConsentRecord consent, CancellationToken ct = default);
    void Update(ConsentRecord consent);
}
