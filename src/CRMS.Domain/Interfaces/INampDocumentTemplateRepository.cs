using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface INampDocumentTemplateRepository
{
    Task<NampDocumentTemplate?> GetByTypeAsync(NampDocumentType documentType, CancellationToken ct = default);
    Task<IReadOnlyList<NampDocumentTemplate>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(NampDocumentTemplate template, CancellationToken ct = default);
}
