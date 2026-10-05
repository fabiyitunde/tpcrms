using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// A guarantor backing a RH-SHF facility. The portal sends none, so guarantors are captured by CRMS
/// staff. Standalone aggregate (not a profile child) so it can be managed independently of the profile
/// load path. An individual guarantor carries a BVN and is credit-checked individually; a corporate
/// guarantor carries an RC number and is checked as a business — both run through the same bureau
/// pipeline that already checks directors and the FAC company.
/// </summary>
public class RhshfGuarantor : AggregateRoot
{
    public Guid RhshfCreditProfileId { get; private set; }
    public RhshfGuarantorType GuarantorType { get; private set; }
    public string FullName { get; private set; } = string.Empty;

    /// <summary>Bureau key for an individual guarantor. Optional until bureau checks run; 11 digits.</summary>
    public string? Bvn { get; private set; }
    /// <summary>Bureau key for a corporate guarantor (business CRC lookup).</summary>
    public string? RcNumber { get; private set; }

    public string? Relationship { get; private set; }   // e.g. Director, Associate, Third Party
    public string? PhoneNumber { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public decimal? GuaranteeAmount { get; private set; }
    public string? Notes { get; private set; }

    protected RhshfGuarantor() { }

    public static Result<RhshfGuarantor> Create(
        Guid rhshfCreditProfileId, RhshfGuarantorType type, string fullName, string? bvn, string? rcNumber,
        string? relationship, string? phoneNumber, string? email, string? address, decimal? guaranteeAmount, string? notes)
    {
        var guarantor = new RhshfGuarantor { RhshfCreditProfileId = rhshfCreditProfileId };
        var result = guarantor.Apply(type, fullName, bvn, rcNumber, relationship, phoneNumber, email, address, guaranteeAmount, notes);
        return result.IsFailure ? Result.Failure<RhshfGuarantor>(result.Error) : Result.Success(guarantor);
    }

    public Result Update(
        RhshfGuarantorType type, string fullName, string? bvn, string? rcNumber, string? relationship,
        string? phoneNumber, string? email, string? address, decimal? guaranteeAmount, string? notes)
        => Apply(type, fullName, bvn, rcNumber, relationship, phoneNumber, email, address, guaranteeAmount, notes);

    private Result Apply(
        RhshfGuarantorType type, string fullName, string? bvn, string? rcNumber, string? relationship,
        string? phoneNumber, string? email, string? address, decimal? guaranteeAmount, string? notes)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return Result.Failure("Guarantor name is required.");
        if (!IsValidBvn(bvn))
            return Result.Failure("BVN must be exactly 11 digits.");
        if (guaranteeAmount is < 0)
            return Result.Failure("Guarantee amount cannot be negative.");

        GuarantorType = type;
        FullName = fullName.Trim();
        // The bureau key for the other type is cleared, so a type switch can't leave a stale identifier
        // that would be checked against the wrong bureau.
        Bvn = type == RhshfGuarantorType.Individual ? Clean(bvn) : null;
        RcNumber = type == RhshfGuarantorType.Corporate ? Clean(rcNumber) : null;
        Relationship = Clean(relationship);
        PhoneNumber = Clean(phoneNumber);
        Email = Clean(email);
        Address = Clean(address);
        GuaranteeAmount = guaranteeAmount;
        Notes = Clean(notes);
        return Result.Success();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsValidBvn(string? bvn)
    {
        if (string.IsNullOrWhiteSpace(bvn)) return true;
        var trimmed = bvn.Trim();
        return trimmed.Length == 11 && trimmed.All(char.IsDigit);
    }
}
