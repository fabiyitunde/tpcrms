using CRMS.Domain.Aggregates.Configuration;

namespace CRMS.Domain.Interfaces;

public interface IScoringParameterRepository
{
    Task<ScoringParameter?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ScoringParameter?> GetByKeyAsync(string category, string parameterKey, CancellationToken ct = default);
    Task<IReadOnlyList<ScoringParameter>> GetAllActiveAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ScoringParameter>> GetByCategoryAsync(string category, CancellationToken ct = default);
    Task<IReadOnlyList<ScoringParameter>> GetPendingChangesAsync(CancellationToken ct = default);
    Task<Dictionary<string, decimal>> GetAllActiveAsLookupAsync(CancellationToken ct = default);
    Task AddAsync(ScoringParameter parameter, CancellationToken ct = default);
    void Update(ScoringParameter parameter);
    
    // History
    Task AddHistoryAsync(ScoringParameterHistory history, CancellationToken ct = default);
    Task<IReadOnlyList<ScoringParameterHistory>> GetHistoryByParameterIdAsync(Guid parameterId, CancellationToken ct = default);
    Task<IReadOnlyList<ScoringParameterHistory>> GetRecentHistoryAsync(int count = 50, CancellationToken ct = default);
}
