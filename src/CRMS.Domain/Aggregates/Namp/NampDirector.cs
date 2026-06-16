using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Namp;

/// <summary>
/// A director or shareholder of an Agro-Service company NAMP applicant.
/// Sourced from the SmartComply CAC lookup, or added manually when CAC data is incomplete.
/// BVN and ShareholdingPercent are frequently absent from CAC and completed by hand —
/// these are preserved across CAC refreshes (see <see cref="RefreshCacFields"/>).
/// </summary>
public class NampDirector : Entity
{
    public Guid NampApplicationId { get; private set; }

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
    public bool BvnVerified { get; private set; }

    private NampDirector() { }

    /// <summary>Creates a director from a SmartComply CAC record.</summary>
    public static NampDirector FromCac(
        Guid nampApplicationId,
        long? cacDirectorId,
        string fullName,
        string? surname,
        string? firstName,
        string? otherName,
        string? gender,
        string? dateOfBirth,
        string? nationality,
        string? occupation,
        string? email,
        string? phoneNumber,
        string? address,
        string? city,
        string? state,
        bool isChairman,
        string? dateOfAppointment,
        string? affiliateType,
        string? roleStatus,
        string? typeOfShares,
        long? numSharesAllotted,
        string? identityNumber)
    {
        return new NampDirector
        {
            NampApplicationId = nampApplicationId,
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
    }

    /// <summary>Creates a director/shareholder entered manually (CAC returned nothing for them).</summary>
    public static Result<NampDirector> CreateManual(
        Guid nampApplicationId,
        string fullName,
        string? bvn,
        decimal? shareholdingPercent,
        bool isChairman,
        string? email,
        string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return Result.Failure<NampDirector>("Director full name is required.");

        if (shareholdingPercent.HasValue && (shareholdingPercent < 0 || shareholdingPercent > 100))
            return Result.Failure<NampDirector>("Shareholding must be between 0 and 100.");

        if (!string.IsNullOrWhiteSpace(bvn) && bvn.Trim().Length != 11)
            return Result.Failure<NampDirector>("BVN must be exactly 11 digits.");

        return Result.Success(new NampDirector
        {
            NampApplicationId = nampApplicationId,
            SourcedFromCac = false,
            FullName = fullName.Trim(),
            Bvn = string.IsNullOrWhiteSpace(bvn) ? null : bvn.Trim(),
            ShareholdingPercent = shareholdingPercent,
            IsChairman = isChairman,
            Email = email,
            PhoneNumber = phoneNumber,
        });
    }

    /// <summary>
    /// Refreshes the CAC-sourced fields from a newer CAC lookup. Deliberately does NOT touch
    /// <see cref="Bvn"/> or <see cref="ShareholdingPercent"/> — those are completed by hand and must survive a refresh.
    /// </summary>
    public void RefreshCacFields(
        string fullName,
        string? surname,
        string? firstName,
        string? otherName,
        string? gender,
        string? dateOfBirth,
        string? nationality,
        string? occupation,
        string? email,
        string? phoneNumber,
        string? address,
        string? city,
        string? state,
        bool isChairman,
        string? dateOfAppointment,
        string? affiliateType,
        string? roleStatus,
        string? typeOfShares,
        long? numSharesAllotted,
        string? identityNumber)
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
        if (!string.IsNullOrWhiteSpace(bvn) && bvn.Trim().Length != 11)
            return Result.Failure("BVN must be exactly 11 digits.");

        Bvn = string.IsNullOrWhiteSpace(bvn) ? null : bvn.Trim();
        BvnVerified = false;
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

    public void MarkBvnVerified()
    {
        BvnVerified = true;
    }
}
