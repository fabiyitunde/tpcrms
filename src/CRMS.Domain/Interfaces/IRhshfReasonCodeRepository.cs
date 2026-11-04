using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Interfaces;

/// <summary>The administrator-maintained reasons for returning or declining an RH-SHF case.</summary>
public interface IRhshfReasonCodeRepository
{
    /// <summary>Every code, active or not, in display order.</summary>
    Task<IReadOnlyList<RhshfReasonCode>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Case-insensitive; tracked, so an update persists on save.</summary>
    Task<RhshfReasonCode?> GetByCodeAsync(string code, CancellationToken ct = default);

    Task AddAsync(RhshfReasonCode reasonCode, CancellationToken ct = default);
}
