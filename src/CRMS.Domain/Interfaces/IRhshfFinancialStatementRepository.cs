using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Interfaces;

public interface IRhshfFinancialStatementRepository
{
    Task<IReadOnlyList<RhshfFinancialStatement>> GetByProfileIdAsync(Guid rhshfCreditProfileId, CancellationToken ct = default);
    Task<RhshfFinancialStatement?> GetByProfileAndYearAsync(Guid rhshfCreditProfileId, int financialYear, CancellationToken ct = default);
    Task<RhshfFinancialStatement?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(RhshfFinancialStatement statement, CancellationToken ct = default);
    void Remove(RhshfFinancialStatement statement);
}
