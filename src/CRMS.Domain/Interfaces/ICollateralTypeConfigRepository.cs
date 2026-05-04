using CRMS.Domain.Aggregates.Collateral;

namespace CRMS.Domain.Interfaces;

public interface ICollateralTypeConfigRepository
{
    Task<CollateralTypeConfig?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CollateralTypeConfig?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<List<CollateralTypeConfig>> GetAllAsync(CancellationToken ct = default);
    Task<List<CollateralTypeConfig>> GetActiveAsync(CancellationToken ct = default);
    Task AddAsync(CollateralTypeConfig config, CancellationToken ct = default);
    void Update(CollateralTypeConfig config);
}
