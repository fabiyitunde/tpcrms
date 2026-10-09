using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// Admin-configured gate item for the RH-SHF Pre-Deployment Verification stage (between Legal
/// Clearance and Disbursement) — mirrors NampPreDeploymentChecklistTemplate's shape, minus the
/// document-category linkage (RH-SHF has no generic per-category document system like NAMP's;
/// confirmation here is a manual DisbursementOfficer attestation, not an automatic document-exists
/// check). Each active template is instantiated into a RhshfPreDeploymentChecklistItem on the
/// RhshfCreditProfile the moment the case enters PreDeploymentVerification.
/// </summary>
public class RhshfPreDeploymentChecklistTemplate : Entity
{
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsMandatory { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>How this gate item is satisfied (auto from offer docs, account/collateral review, or a
    /// plain manual tick). Defaults to Manual, so pre-existing templates keep their current behaviour.</summary>
    public RhshfPreDeploymentVerificationKind Kind { get; private set; }

    protected RhshfPreDeploymentChecklistTemplate() { }

    public static Result<RhshfPreDeploymentChecklistTemplate> Create(
        string title, string? description, bool isMandatory, int sortOrder,
        RhshfPreDeploymentVerificationKind kind = RhshfPreDeploymentVerificationKind.Manual)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure<RhshfPreDeploymentChecklistTemplate>("Title is required.");

        return Result.Success(new RhshfPreDeploymentChecklistTemplate
        {
            Title = title.Trim(),
            Description = description?.Trim(),
            IsMandatory = isMandatory,
            SortOrder = sortOrder,
            Kind = kind,
            IsActive = true,
        });
    }

    public Result Update(string title, string? description, bool isMandatory, int sortOrder,
        RhshfPreDeploymentVerificationKind kind = RhshfPreDeploymentVerificationKind.Manual)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure("Title is required.");

        Title = title.Trim();
        Description = description?.Trim();
        IsMandatory = isMandatory;
        SortOrder = sortOrder;
        Kind = kind;
        return Result.Success();
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
