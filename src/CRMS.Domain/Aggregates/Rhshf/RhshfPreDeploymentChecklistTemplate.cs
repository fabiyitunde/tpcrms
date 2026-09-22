using CRMS.Domain.Common;

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

    protected RhshfPreDeploymentChecklistTemplate() { }

    public static Result<RhshfPreDeploymentChecklistTemplate> Create(
        string title, string? description, bool isMandatory, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure<RhshfPreDeploymentChecklistTemplate>("Title is required.");

        return Result.Success(new RhshfPreDeploymentChecklistTemplate
        {
            Title = title.Trim(),
            Description = description?.Trim(),
            IsMandatory = isMandatory,
            SortOrder = sortOrder,
            IsActive = true,
        });
    }

    public Result Update(string title, string? description, bool isMandatory, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure("Title is required.");

        Title = title.Trim();
        Description = description?.Trim();
        IsMandatory = isMandatory;
        SortOrder = sortOrder;
        return Result.Success();
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
