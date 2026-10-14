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

    /// <summary>Which approved category the guarantor's connection to the FAC falls under. Nullable
    /// only for rows written before the categories existed; required on every write now.</summary>
    public RhshfGuarantorRelationship? RelationshipCategory { get; private set; }

    /// <summary>
    /// The relationship as stated, when <see cref="RelationshipCategory"/> is
    /// <see cref="RhshfGuarantorRelationship.Other"/>. Null for every other category — kept clear on
    /// purpose, so a category changed away from Other cannot leave its old text showing beside it.
    /// <para>
    /// This is the original free-text Relationship column. Historical values that matched no category
    /// were backfilled here under Other, so nothing staff had typed was lost.
    /// </para>
    /// </summary>
    public string? RelationshipOther { get; private set; }

    /// <summary>
    /// The relationship as it should be read: the stated text for Other, the category name otherwise,
    /// and null for a pre-categories row that never had either.
    /// <para>
    /// Spaced out ("ParentCompany" reads as "Parent Company") so the three places this surfaces — the
    /// FAC's table, the staff Guarantors tab and the loan pack PDF — all match the wording on the
    /// dropdown that produced it. Formatted here rather than per-surface because the PDF generator
    /// lives in Infrastructure and cannot reach the web project's label helper.
    /// </para>
    /// </summary>
    public string? RelationshipDisplay => RelationshipCategory switch
    {
        null => RelationshipOther,
        RhshfGuarantorRelationship.Other => RelationshipOther,
        var category => System.Text.RegularExpressions.Regex.Replace(
            category.ToString()!, "(?<!^)([A-Z])", " $1"),
    };
    public string? PhoneNumber { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public decimal? GuaranteeAmount { get; private set; }
    public string? Notes { get; private set; }

    protected RhshfGuarantor() { }

    public static Result<RhshfGuarantor> Create(
        Guid rhshfCreditProfileId, RhshfGuarantorType type, string fullName, string? bvn, string? rcNumber,
        RhshfGuarantorRelationship? relationshipCategory, string? relationshipOther,
        string? phoneNumber, string? email, string? address, decimal? guaranteeAmount, string? notes)
    {
        var guarantor = new RhshfGuarantor { RhshfCreditProfileId = rhshfCreditProfileId };
        var result = guarantor.Apply(
            type, fullName, bvn, rcNumber, relationshipCategory, relationshipOther,
            phoneNumber, email, address, guaranteeAmount, notes);
        return result.IsFailure ? Result.Failure<RhshfGuarantor>(result.Error) : Result.Success(guarantor);
    }

    public Result Update(
        RhshfGuarantorType type, string fullName, string? bvn, string? rcNumber,
        RhshfGuarantorRelationship? relationshipCategory, string? relationshipOther,
        string? phoneNumber, string? email, string? address, decimal? guaranteeAmount, string? notes)
        => Apply(
            type, fullName, bvn, rcNumber, relationshipCategory, relationshipOther,
            phoneNumber, email, address, guaranteeAmount, notes);

    private Result Apply(
        RhshfGuarantorType type, string fullName, string? bvn, string? rcNumber,
        RhshfGuarantorRelationship? relationshipCategory, string? relationshipOther,
        string? phoneNumber, string? email, string? address, decimal? guaranteeAmount, string? notes)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return Result.Failure("Guarantor name is required.");
        if (!IsValidBvn(bvn))
            return Result.Failure("BVN must be exactly 11 digits.");
        if (!IsValidRcNumber(rcNumber))
            return Result.Failure("RC number must be 5 to 20 letters or digits (for example RC123456).");
        if (guaranteeAmount is < 0)
            return Result.Failure("Guarantee amount cannot be negative.");

        // Required from the point the categories existed. Old rows can hold null — the backfill
        // could not invent a category for text that matched none, and reading them must not fail —
        // but nothing may be written without one.
        if (relationshipCategory is null)
            return Result.Failure("Select the guarantor's relationship to the FAC.");
        if (relationshipCategory == RhshfGuarantorRelationship.Other && string.IsNullOrWhiteSpace(relationshipOther))
            return Result.Failure("State the relationship when choosing Other.");

        GuarantorType = type;
        FullName = fullName.Trim();
        // The bureau key for the other type is cleared, so a type switch can't leave a stale identifier
        // that would be checked against the wrong bureau.
        Bvn = type == RhshfGuarantorType.Individual ? Clean(bvn) : null;
        RcNumber = type == RhshfGuarantorType.Corporate ? NormaliseRcNumber(rcNumber) : null;
        RelationshipCategory = relationshipCategory;
        // Cleared for every category but Other, so changing away from Other cannot leave the old
        // specification showing beside a category that contradicts it.
        RelationshipOther = relationshipCategory == RhshfGuarantorRelationship.Other ? Clean(relationshipOther) : null;
        PhoneNumber = Clean(phoneNumber);
        Email = Clean(email);
        Address = Clean(address);
        GuaranteeAmount = guaranteeAmount;
        Notes = Clean(notes);
        return Result.Success();
    }

    /// <summary>
    /// Sets the category on a pre-categories row from its historical free text.
    /// <para>
    /// Backfill only, called once at startup by RhshfWorkflowSeeder. Deliberately bypasses the
    /// validation in <see cref="Apply"/>: it is reclassifying what is already stored, not accepting
    /// new input. Ordinary writes go through <see cref="Update"/>, which requires a category.
    /// </para>
    /// </summary>
    public void BackfillRelationship(RhshfGuarantorRelationship category, string? other)
    {
        RelationshipCategory = category;
        RelationshipOther = category == RhshfGuarantorRelationship.Other ? Clean(other) : null;
    }

    /// <summary>
    /// Maps historical free text onto a category. Anything unrecognised becomes Other, keeping the
    /// original text as the specification rather than discarding what staff recorded.
    /// </summary>
    public static (RhshfGuarantorRelationship Category, string? Other) ClassifyLegacyRelationship(string? text)
    {
        var normalised = new string((text ?? string.Empty).Where(char.IsLetter).ToArray()).ToLowerInvariant();

        return normalised switch
        {
            "director" or "directors" => (RhshfGuarantorRelationship.Director, null),
            "shareholder" or "shareholders" => (RhshfGuarantorRelationship.Shareholder, null),
            "businesspartner" or "partner" or "businesspartners" => (RhshfGuarantorRelationship.BusinessPartner, null),
            "parentcompany" or "holdingcompany" => (RhshfGuarantorRelationship.ParentCompany, null),
            "relatedcompany" or "associatedcompany" or "sistercompany" or "affiliate"
                => (RhshfGuarantorRelationship.RelatedCompany, null),
            _ => (RhshfGuarantorRelationship.Other, text),
        };
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsValidBvn(string? bvn)
    {
        if (string.IsNullOrWhiteSpace(bvn)) return true;
        var trimmed = bvn.Trim();
        return trimmed.Length == 11 && trimmed.All(char.IsDigit);
    }

    /// <summary>
    /// Deliberately loose. CRMS already treats RC, BN and IT prefixes and bare digits as valid company
    /// registrations (see FetchRhshfCacDetailsHandler.DeriveCompanyType), so a strict pattern would
    /// reject real ones. Blank passes here — it is required at submission, not at data entry.
    /// </summary>
    private static bool IsValidRcNumber(string? rcNumber)
    {
        if (string.IsNullOrWhiteSpace(rcNumber)) return true;
        var trimmed = NormaliseRcNumber(rcNumber)!;
        return trimmed.Length is >= 5 and <= 20 && trimmed.All(char.IsLetterOrDigit);
    }

    /// <summary>Upper-cased with spaces, slashes and dashes stripped, so "rc 123-456" and "RC123456"
    /// are the same registration to the bureau lookup.</summary>
    private static string? NormaliseRcNumber(string? rcNumber)
    {
        if (string.IsNullOrWhiteSpace(rcNumber)) return null;
        var stripped = new string(rcNumber.Where(c => !char.IsWhiteSpace(c) && c is not '-' and not '/').ToArray());
        return stripped.ToUpperInvariant();
    }
}
