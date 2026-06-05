using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface INampStagingRepository
{
    Task<NampStagingRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<NampStagingRecord?> GetByApplicationReferenceAsync(string applicationReference, CancellationToken ct = default);
    Task<NampStagingRecord?> GetByCrmsApplicationNumberAsync(string crmsApplicationNumber, CancellationToken ct = default);
    Task<IReadOnlyList<NampStagingRecord>> GetPendingRecallAsync(Guid? branchId = null, CancellationToken ct = default);
    Task<bool> ExistsByReferenceAsync(string applicationReference, CancellationToken ct = default);
    Task AddAsync(NampStagingRecord record, CancellationToken ct = default);
    void Update(NampStagingRecord record);
}

public interface INampRoutingConfigRepository
{
    Task<IReadOnlyList<NampRoutingConfig>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NampRoutingConfig>> GetActiveConfigsAsync(CancellationToken ct = default);
    Task<NampRoutingConfig?> ResolveAsync(NampApplicantCategory category, decimal equipmentValue, CancellationToken ct = default);
    Task<NampRoutingConfig?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(NampRoutingConfig config, CancellationToken ct = default);
    void Update(NampRoutingConfig config);
}
