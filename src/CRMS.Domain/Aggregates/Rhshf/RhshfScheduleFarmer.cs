using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// One farmer on the schedule behind a FAC's credit case, as the portal sent it (CRMSQA#4 Q2). The
/// CRMS used to hold only the farmer count per EOP; with the farmers themselves, the party-exposure
/// lookup can say a person is a beneficiary of a FAC's facility.
/// <para>
/// Kept in its own table rather than as a collection on <see cref="RhshfCreditProfile"/>: a case
/// can carry hundreds of farmers, and the profile is loaded whole on every case page. Only the
/// identifiers the lookup needs are kept — no name, phone or address.
/// </para>
/// </summary>
public class RhshfScheduleFarmer : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }
    /// <summary>The approved EOP the farmer is scheduled under, when the portal says.</summary>
    public Guid? EopId { get; private set; }
    /// <summary>The portal's own id for the farmer, for tracing a row back to its register.</summary>
    public string? FarmerRef { get; private set; }
    public string? Bvn { get; private set; }
    public string? Nin { get; private set; }
    public string? Nuban { get; private set; }

    protected RhshfScheduleFarmer() { }

    private RhshfScheduleFarmer(Guid profileId, Guid? eopId, string? farmerRef, string? bvn, string? nin, string? nuban)
    {
        RhshfCreditProfileId = profileId;
        EopId = eopId;
        FarmerRef = string.IsNullOrWhiteSpace(farmerRef) ? null : farmerRef.Trim()[..Math.Min(farmerRef.Trim().Length, 100)];
        Bvn = bvn;
        Nin = nin;
        Nuban = nuban;
    }

    public const int MaxFarmers = 20_000;

    /// <summary>
    /// Checks the whole schedule and builds its rows, or says why it was refused. All or nothing: a
    /// schedule with one bad entry is refused whole, so a case never holds half a farmer list.
    /// <para>
    /// Every farmer needs at least one identifier, each in its exact form. The same farmer may not
    /// appear twice under one EOP. When <paramref name="knownEopIds"/> is given, every EOP named must
    /// be one of the case's. Errors name the entry by position, never by its identifiers.
    /// </para>
    /// </summary>
    public static Result<List<RhshfScheduleFarmer>> BuildSchedule(
        Guid profileId, IReadOnlyList<RhshfScheduleFarmerInput> entries, IReadOnlyCollection<Guid>? knownEopIds)
    {
        if (entries.Count > MaxFarmers)
            return Result.Failure<List<RhshfScheduleFarmer>>($"farmerSchedule may hold at most {MaxFarmers:N0} farmers.");

        var rows = new List<RhshfScheduleFarmer>(entries.Count);
        var seen = new HashSet<string>();
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var bvn = Identifiers.Digits(e.Bvn);
            var nin = Identifiers.Digits(e.Nin);
            var nuban = Identifiers.Digits(e.Nuban);

            if (bvn is null && nin is null && nuban is null)
                return Fail(i, "has no BVN, NIN or account number.");
            if (bvn is not null && !IsDigits(bvn, 11)) return Fail(i, "has a BVN that is not 11 digits.");
            if (nin is not null && !IsDigits(nin, 11)) return Fail(i, "has a NIN that is not 11 digits.");
            if (nuban is not null && !IsDigits(nuban, 10)) return Fail(i, "has an account number that is not 10 digits.");
            if (e.EopId is { } eop && knownEopIds is { Count: > 0 } && !knownEopIds.Contains(eop))
                return Fail(i, "names an EOP that is not on this case.");

            var key = $"{e.EopId}|{bvn ?? nin ?? nuban}";
            if (!seen.Add(key))
                return Fail(i, "repeats a farmer already listed under the same EOP.");

            rows.Add(new RhshfScheduleFarmer(profileId, e.EopId, e.FarmerRef, bvn, nin, nuban));
        }
        return Result.Success(rows);
    }

    private static Result<List<RhshfScheduleFarmer>> Fail(int index, string why)
        => Result.Failure<List<RhshfScheduleFarmer>>($"farmerSchedule[{index}] {why}");

    private static bool IsDigits(string value, int length) => value.Length == length && value.All(char.IsAsciiDigit);
}

public record RhshfScheduleFarmerInput(Guid? EopId, string? FarmerRef, string? Bvn, string? Nin, string? Nuban);
