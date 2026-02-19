using CRMS.Domain.Aggregates.Guarantor;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface IGuarantorRepository
{
    Task<Guarantor?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Guarantor?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Guarantor>> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task<IReadOnlyList<Guarantor>> GetByBVNAsync(string bvn, CancellationToken ct = default);
    Task<IReadOnlyList<Guarantor>> GetByStatusAsync(GuarantorStatus status, CancellationToken ct = default);
    Task<Guarantor?> GetByReferenceAsync(string reference, CancellationToken ct = default);
    Task<int> GetActiveGuaranteeCountByBVNAsync(string bvn, CancellationToken ct = default);
    Task AddAsync(Guarantor guarantor, CancellationToken ct = default);
    void Update(Guarantor guarantor);
    void Delete(Guarantor guarantor);
}
