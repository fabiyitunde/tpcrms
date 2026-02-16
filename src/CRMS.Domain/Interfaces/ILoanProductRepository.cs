using CRMS.Domain.Aggregates.ProductCatalog;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface ILoanProductRepository
{
    Task<LoanProduct?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<LoanProduct?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<LoanProduct>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LoanProduct>> GetByStatusAsync(ProductStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<LoanProduct>> GetByTypeAsync(LoanProductType type, CancellationToken ct = default);
    Task<IReadOnlyList<LoanProduct>> GetActiveByTypeAsync(LoanProductType type, CancellationToken ct = default);
    Task<bool> ExistsAsync(string code, CancellationToken ct = default);
    Task AddAsync(LoanProduct product, CancellationToken ct = default);
    Task UpdateAsync(LoanProduct product, CancellationToken ct = default);
}
