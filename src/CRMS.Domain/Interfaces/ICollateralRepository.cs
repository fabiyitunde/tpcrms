using CRMS.Domain.Aggregates.Collateral;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface ICollateralRepository
{
    Task<Collateral?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Collateral?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Collateral>> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task<IReadOnlyList<Collateral>> GetByStatusAsync(CollateralStatus status, CancellationToken ct = default);
    Task<Collateral?> GetByReferenceAsync(string reference, CancellationToken ct = default);
    Task AddAsync(Collateral collateral, CancellationToken ct = default);
    void Update(Collateral collateral);
    void Delete(Collateral collateral);
}

public interface ICollateralDocumentRepository
{
    Task<CollateralDocument?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CollateralDocument>> GetByCollateralIdAsync(Guid collateralId, CancellationToken ct = default);
    Task AddAsync(CollateralDocument document, CancellationToken ct = default);
    void Delete(CollateralDocument document);
}
