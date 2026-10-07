using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Interfaces;

public interface IRhshfCollateralRepository
{
    Task<IReadOnlyList<RhshfCollateral>> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default);
    Task<RhshfCollateral?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(RhshfCollateral collateral, CancellationToken ct = default);
    void Remove(RhshfCollateral collateral);

    /// <summary>Direct lookup for staff document download.</summary>
    Task<RhshfCollateralDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default);
}
