namespace CRMS.Domain.Interfaces;

/// <summary>
/// Which cases already hold a given EOP (CRMSQA#3). A new submission carrying an EOP that is on
/// another live case would finance the same farmers twice, so the submission is refused.
/// </summary>
public interface IRhshfEopHolderLookup
{
    /// <summary>
    /// The cases holding any of <paramref name="eopIds"/> that are still open or approved. A declined,
    /// cancelled or expired case does not count: the FAC may apply again after one.
    /// </summary>
    Task<IReadOnlyList<RhshfEopHolder>> GetLiveHoldersAsync(IReadOnlyCollection<Guid> eopIds, CancellationToken ct = default);
}

/// <summary>An EOP and the case that holds it.</summary>
public record RhshfEopHolder(Guid EopId, string? EopReference, string CaseReference);
