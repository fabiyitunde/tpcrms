using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Interfaces;

/// <summary>The farmers behind a FAC's credit case (CRMSQA#4). Written once per case, never edited.</summary>
public interface IRhshfScheduleFarmerRepository
{
    Task<int> CountForProfileAsync(Guid rhshfCreditProfileId, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<RhshfScheduleFarmer> farmers, CancellationToken ct = default);
}
