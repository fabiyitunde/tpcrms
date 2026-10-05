using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Interfaces;

public interface IRhshfGuarantorRepository
{
    Task<IReadOnlyList<RhshfGuarantor>> GetByProfileIdAsync(Guid rhshfCreditProfileId, CancellationToken ct = default);
    Task<RhshfGuarantor?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(RhshfGuarantor guarantor, CancellationToken ct = default);
    void Remove(RhshfGuarantor guarantor);
}
