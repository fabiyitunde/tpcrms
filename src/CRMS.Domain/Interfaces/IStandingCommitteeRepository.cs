using CRMS.Domain.Aggregates.Committee;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface IStandingCommitteeRepository
{
    Task<StandingCommittee?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<StandingCommittee?> GetByCommitteeTypeAsync(CommitteeType type, CancellationToken ct = default);
    Task<StandingCommittee?> GetForAmountAsync(decimal amount, CancellationToken ct = default);
    Task<IReadOnlyList<StandingCommittee>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);
    Task AddAsync(StandingCommittee committee, CancellationToken ct = default);
    void Update(StandingCommittee committee);
}
