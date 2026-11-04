using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Interfaces;

/// <summary>The farmers behind a FAC's credit case (CRMSQA#4). Written once per case and never edited,
/// except when the portal amends the case's EOPs (CRMSQA#3).</summary>
public interface IRhshfScheduleFarmerRepository
{
    Task<int> CountForProfileAsync(Guid rhshfCreditProfileId, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<RhshfScheduleFarmer> farmers, CancellationToken ct = default);

    /// <summary>Takes a case's farmers off, saved with the rest of the unit of work. With
    /// <paramref name="keepEopIds"/>, farmers on those EOPs, and farmers on no EOP, stay.</summary>
    Task RemoveForProfileAsync(Guid rhshfCreditProfileId, IReadOnlyCollection<Guid>? keepEopIds = null, CancellationToken ct = default);
}
