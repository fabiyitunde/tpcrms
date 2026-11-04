using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Services;

/// <summary>
/// Collateral is recorded per review cycle, and nothing used to carry it into the next one. A case
/// sent back to the FAC therefore reopened the profiling form with no security declared, and the FAC
/// had to declare every instrument and upload its documents again before resubmitting. The security
/// has not changed, so the next cycle now starts with it as declared, documents included. Perfection
/// is not carried: Legal confirms it again in the new review. The FAC can still remove or add.
/// </summary>
public static class RhshfCollateralCarryForward
{
    /// <summary>For a case just returned to the FAC: copies the cycle under review into the one the
    /// FAC is preparing, unless that one already holds anything.</summary>
    public static Task CarryIntoNextCycleAsync(IRhshfCollateralRepository? repo, RhshfCreditProfile profile, CancellationToken ct)
        => profile.Status == RhshfCaseStatus.ProfilingInProgress && profile.CurrentCycleNumber > 0
            ? CarryAsync(repo, profile.Id, profile.CurrentCycleNumber, profile.ProfilingTargetCycleNumber, ct)
            : Task.CompletedTask;

    /// <summary>Copies one cycle's collateral into another that holds none. Queued with the unit of
    /// work; saved by the caller.</summary>
    public static async Task CarryAsync(IRhshfCollateralRepository? repo, Guid profileId, int fromCycle, int toCycle, CancellationToken ct)
    {
        if (repo is null || fromCycle <= 0 || fromCycle == toCycle)
            return;
        if ((await repo.GetByProfileAndCycleAsync(profileId, toCycle, ct)).Count > 0)
            return;

        foreach (var held in await repo.GetByProfileAndCycleAsync(profileId, fromCycle, ct))
            await repo.AddAsync(held.CopyToCycle(toCycle), ct);
    }
}
