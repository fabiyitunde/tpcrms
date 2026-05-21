using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Namp;

/// <summary>
/// A guarantor submitted via the NAMP webhook payload and unpacked at Loan Officer recall time.
/// </summary>
public class NampGuarantor : Entity
{
    public Guid NampApplicationId { get; private set; }

    // ── Identity ───────────────────────────────────────────────────────────
    public string FullName { get; private set; } = string.Empty;
    public string? Bvn { get; private set; }
    public string? Nin { get; private set; }
    public string? DateOfBirth { get; private set; }
    public string? Gender { get; private set; }

    // ── Corporate Identity (when GuarantorType == "Corporate") ─────────────
    public string? CompanyName { get; private set; }
    public string? CompanyRegistrationNumber { get; private set; }

    // ── Contact ────────────────────────────────────────────────────────────
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Address { get; private set; }

    // ── Type & Relationship ────────────────────────────────────────────────
    public string GuarantorType { get; private set; } = string.Empty;
    public string GuaranteeType { get; private set; } = string.Empty;
    public string? RelationshipToApplicant { get; private set; }
    public bool IsDirector { get; private set; }
    public bool IsShareholder { get; private set; }
    public decimal? ShareholdingPercentage { get; private set; }

    // ── Financial Standing ─────────────────────────────────────────────────
    public decimal? DeclaredNetWorth { get; private set; }
    public string? Occupation { get; private set; }
    public string? EmployerName { get; private set; }
    public decimal? MonthlyIncome { get; private set; }

    // ── Guarantee Details ──────────────────────────────────────────────────
    public decimal? GuaranteeLimit { get; private set; }
    public bool IsUnlimited { get; private set; }

    private NampGuarantor() { }

    public static NampGuarantor Create(
        Guid nampApplicationId,
        string fullName,
        string? bvn,
        string? nin,
        string? dateOfBirth,
        string? gender,
        string? companyName,
        string? companyRegistrationNumber,
        string? email,
        string? phone,
        string? address,
        string guarantorType,
        string guaranteeType,
        string? relationshipToApplicant,
        bool isDirector,
        bool isShareholder,
        decimal? shareholdingPercentage,
        decimal? declaredNetWorth,
        string? occupation,
        string? employerName,
        decimal? monthlyIncome,
        decimal? guaranteeLimit,
        bool isUnlimited)
    {
        return new NampGuarantor
        {
            NampApplicationId = nampApplicationId,
            FullName = fullName,
            Bvn = bvn,
            Nin = nin,
            DateOfBirth = dateOfBirth,
            Gender = gender,
            CompanyName = companyName,
            CompanyRegistrationNumber = companyRegistrationNumber,
            Email = email,
            Phone = phone,
            Address = address,
            GuarantorType = guarantorType,
            GuaranteeType = guaranteeType,
            RelationshipToApplicant = relationshipToApplicant,
            IsDirector = isDirector,
            IsShareholder = isShareholder,
            ShareholdingPercentage = shareholdingPercentage,
            DeclaredNetWorth = declaredNetWorth,
            Occupation = occupation,
            EmployerName = employerName,
            MonthlyIncome = monthlyIncome,
            GuaranteeLimit = guaranteeLimit,
            IsUnlimited = isUnlimited,
        };
    }
}
