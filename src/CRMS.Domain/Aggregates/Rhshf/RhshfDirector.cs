using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// A director or shareholder of the FAC (Farmer Aggregation Company) on a RH-SHF case.
///
/// Reverses the v1 "no directors" decision (design doc §6 #3): that decision was taken because the
/// portal payload carries no director data, but it also capped the credit bureau at a single
/// company-level check. Directors are captured in CRMS instead — from the SmartComply CAC lookup,
/// or by hand when CAC data is incomplete — which is what makes per-director bureau checks possible.
///
/// BVN and ShareholdingPercent are frequently absent from CAC and completed manually; both survive a
/// CAC refresh (see <see cref="RefreshCacFields"/>).
///
/// Mirrors NampDirector, minus its BvnVerified flag and MarkBvnVerified() — those are dead in NAMP
/// (nothing in the application layer ever calls them), so they are deliberately not replicated here.
/// </summary>
public class RhshfDirector : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }

    // ── Provenance ─────────────────────────────────────────────────────────
    public long? CacDirectorId { get; private set; }   // SmartComply director id — used to match on refresh
    public bool SourcedFromCac { get; private set; }

    // ── Identity ───────────────────────────────────────────────────────────
    public string FullName { get; private set; } = string.Empty;
    public string? Surname { get; private set; }
    public string? FirstName { get; private set; }
    public string? OtherName { get; private set; }
    public string? Gender { get; private set; }
    public string? DateOfBirth { get; private set; }
    public string? Nationality { get; private set; }
    public string? Occupation { get; private set; }

    // ── Contact ────────────────────────────────────────────────────────────
    public string? Email { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? Address { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }

    // ── Corporate role ─────────────────────────────────────────────────────
    public bool IsChairman { get; private set; }
    public string? DateOfAppointment { get; private set; }
    public string? AffiliateType { get; private set; }
    public string? RoleStatus { get; private set; }

    // ── Shares ─────────────────────────────────────────────────────────────
    public string? TypeOfShares { get; private set; }
    public long? NumSharesAllotted { get; private set; }
    public decimal? ShareholdingPercent { get; private set; }   // Manually completed — not from CAC

    // ── Identity documents (manually completed) ────────────────────────────
    public string? Bvn { get; private set; }
    public string? IdentityNumber { get; private set; }

    /// <summary>True when this director was declared by the FAC (during profiling, incl. its own
    /// core-banking pull). Staff may not delete these — only CAC/stale rows and their own manual
    /// additions — so an officer cannot quietly remove what the applicant submitted.</summary>
    public bool DeclaredByFac { get; private set; }

    protected RhshfDirector() { }

    /// <summary>Creates a director from a SmartComply CAC record.</summary>
    public static RhshfDirector FromCac(
        Guid rhshfCreditProfileId, long? cacDirectorId, string fullName,
        string? surname, string? firstName, string? otherName, string? gender, string? dateOfBirth,
        string? nationality, string? occupation, string? email, string? phoneNumber,
        string? address, string? city, string? state,
        bool isChairman, string? dateOfAppointment, string? affiliateType, string? roleStatus,
        string? typeOfShares, long? numSharesAllotted, string? identityNumber)
        => new()
        {
            RhshfCreditProfileId = rhshfCreditProfileId,
            CacDirectorId = cacDirectorId,
            SourcedFromCac = true,
            FullName = string.IsNullOrWhiteSpace(fullName) ? "(Unnamed)" : fullName,
            Surname = surname,
            FirstName = firstName,
            OtherName = otherName,
            Gender = gender,
            DateOfBirth = dateOfBirth,
            Nationality = nationality,
            Occupation = occupation,
            Email = email,
            PhoneNumber = phoneNumber,
            Address = address,
            City = city,
            State = state,
            IsChairman = isChairman,
            DateOfAppointment = dateOfAppointment,
            AffiliateType = affiliateType,
            RoleStatus = roleStatus,
            TypeOfShares = typeOfShares,
            NumSharesAllotted = numSharesAllotted,
            IdentityNumber = identityNumber,
        };

    /// <summary>Creates a director/shareholder entered manually (CAC returned nothing for them).</summary>
    public static Result<RhshfDirector> CreateManual(
        Guid rhshfCreditProfileId, string fullName, string? bvn, decimal? shareholdingPercent,
        bool isChairman, string? email, string? phoneNumber, bool declaredByFac = false)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return Result.Failure<RhshfDirector>("Director full name is required.");
        if (shareholdingPercent.HasValue && (shareholdingPercent < 0 || shareholdingPercent > 100))
            return Result.Failure<RhshfDirector>("Shareholding must be between 0 and 100.");
        if (!IsValidBvn(bvn))
            return Result.Failure<RhshfDirector>("BVN must be exactly 11 digits.");

        return Result.Success(new RhshfDirector
        {
            RhshfCreditProfileId = rhshfCreditProfileId,
            SourcedFromCac = false,
            FullName = fullName.Trim(),
            Bvn = string.IsNullOrWhiteSpace(bvn) ? null : bvn.Trim(),
            ShareholdingPercent = shareholdingPercent,
            IsChairman = isChairman,
            Email = email,
            PhoneNumber = phoneNumber,
            DeclaredByFac = declaredByFac,
        });
    }

    /// <summary>
    /// Refreshes CAC-sourced fields from a newer lookup. Deliberately does NOT touch <see cref="Bvn"/>
    /// or <see cref="ShareholdingPercent"/> — those are hand-completed and must survive a refresh,
    /// otherwise re-fetching CAC would silently wipe the BVNs the bureau checks depend on.
    /// </summary>
    public void RefreshCacFields(
        string fullName, string? surname, string? firstName, string? otherName, string? gender,
        string? dateOfBirth, string? nationality, string? occupation, string? email, string? phoneNumber,
        string? address, string? city, string? state,
        bool isChairman, string? dateOfAppointment, string? affiliateType, string? roleStatus,
        string? typeOfShares, long? numSharesAllotted, string? identityNumber)
    {
        FullName = string.IsNullOrWhiteSpace(fullName) ? FullName : fullName;
        Surname = surname;
        FirstName = firstName;
        OtherName = otherName;
        Gender = gender;
        DateOfBirth = dateOfBirth;
        Nationality = nationality;
        Occupation = occupation;
        Email = email;
        PhoneNumber = phoneNumber;
        Address = address;
        City = city;
        State = state;
        IsChairman = isChairman;
        DateOfAppointment = dateOfAppointment;
        AffiliateType = affiliateType;
        RoleStatus = roleStatus;
        TypeOfShares = typeOfShares;
        NumSharesAllotted = numSharesAllotted;
        IdentityNumber = identityNumber;
    }

    public Result UpdateBvn(string? bvn)
    {
        if (!IsValidBvn(bvn))
            return Result.Failure("BVN must be exactly 11 digits.");

        Bvn = string.IsNullOrWhiteSpace(bvn) ? null : bvn.Trim();
        return Result.Success();
    }

    public Result UpdateShareholding(decimal? shareholdingPercent)
    {
        if (shareholdingPercent.HasValue && (shareholdingPercent < 0 || shareholdingPercent > 100))
            return Result.Failure("Shareholding must be between 0 and 100.");

        ShareholdingPercent = shareholdingPercent;
        return Result.Success();
    }

    public Result UpdateDetails(string fullName, bool isChairman, string? email, string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return Result.Failure("Director full name is required.");

        FullName = fullName.Trim();
        IsChairman = isChairman;
        Email = email;
        PhoneNumber = phoneNumber;
        return Result.Success();
    }

    /// <summary>Blank is allowed (BVN is optional until bureau checks run); a present value must be
    /// exactly 11 digits. NAMP only checks length — digits are checked here too, since a non-numeric
    /// BVN cannot possibly resolve at the bureau and failing early beats a failed credit check.</summary>
    private static bool IsValidBvn(string? bvn)
    {
        if (string.IsNullOrWhiteSpace(bvn)) return true;
        var trimmed = bvn.Trim();
        return trimmed.Length == 11 && trimmed.All(char.IsDigit);
    }
}
