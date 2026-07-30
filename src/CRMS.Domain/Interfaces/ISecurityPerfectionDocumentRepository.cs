using CRMS.Domain.Aggregates.LoanApplication;

namespace CRMS.Domain.Interfaces;

public interface ISecurityPerfectionDocumentRepository
{
    Task AddAsync(SecurityPerfectionDocument document, CancellationToken ct = default);
    Task<SecurityPerfectionDocument?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SecurityPerfectionDocument>> GetByApplicationIdAsync(Guid applicationId, CancellationToken ct = default);
    void Delete(SecurityPerfectionDocument document);
}
