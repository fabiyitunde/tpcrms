using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// A member of the FAC's management team, declared by the FAC during profiling. BOA asks for their
/// CVs and telephone numbers alongside the directors'. Kept apart from <see cref="RhshfDirector"/>:
/// a manager holds no shares and is not a bureau subject, so none of the director's identity and
/// shareholding rules apply.
///
/// Edited in place, so it takes the database existence check in CRMSDbContext.SaveChangesAsync
/// rather than the blanket Modified-to-Added flip.
/// </summary>
public class RhshfManagementStaff : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Position { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public string? Email { get; private set; }

    /// <summary>See <see cref="RhshfDirector.IsChiefPromoter"/> — one person per case.</summary>
    public bool IsChiefPromoter { get; private set; }

    protected RhshfManagementStaff() { }

    public static Result<RhshfManagementStaff> Create(
        Guid rhshfCreditProfileId, string fullName, string position, string? phoneNumber, string? email)
    {
        var staff = new RhshfManagementStaff { RhshfCreditProfileId = rhshfCreditProfileId };
        var result = staff.Update(fullName, position, phoneNumber, email);
        return result.IsFailure ? Result.Failure<RhshfManagementStaff>(result.Error) : Result.Success(staff);
    }

    public Result Update(string fullName, string position, string? phoneNumber, string? email)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return Result.Failure("Enter the full name.");
        if (string.IsNullOrWhiteSpace(position))
            return Result.Failure("Enter the position held.");
        if (fullName.Trim().Length > 300 || position.Trim().Length > 150)
            return Result.Failure("The name or position is too long.");

        FullName = fullName.Trim();
        Position = position.Trim();
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        return Result.Success();
    }

    internal void SetChiefPromoter(bool value) => IsChiefPromoter = value;
}
