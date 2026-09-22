using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Interfaces;

public interface IRhshfAppraisalThresholdsRepository
{
    /// <summary>The thresholds currently in force. Null when none are configured — callers must
    /// treat that as a blocking configuration error rather than silently substituting defaults,
    /// since inventing credit policy at runtime is exactly what this table exists to prevent.</summary>
    Task<RhshfAppraisalThresholds?> GetActiveAsync(CancellationToken ct = default);
    Task AddAsync(RhshfAppraisalThresholds thresholds, CancellationToken ct = default);
    void Update(RhshfAppraisalThresholds thresholds);
}
