using CRMS.Domain.Aggregates.Consent;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface IConsentRecordRepository
{
    Task<ConsentRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);
    
    /// <summary>
    /// Gets a valid consent record by subject identifier (BVN for individuals, RC number for business).
    /// Searches both BVN and NIN fields to support both individual and business consent records.
    /// </summary>
    Task<ConsentRecord?> GetValidConsentAsync(string subjectIdentifier, ConsentType consentType, CancellationToken ct = default);
    
    Task<List<ConsentRecord>> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task AddAsync(ConsentRecord consent, CancellationToken ct = default);
    void Update(ConsentRecord consent);
}
