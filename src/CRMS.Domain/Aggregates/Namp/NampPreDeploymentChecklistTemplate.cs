using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Namp;

/// <summary>
/// Admin-configured checklist item for the NAMP pre-deployment verification stage.
/// Each active template is instantiated into a NampPreDeploymentChecklistItem when
/// "Begin Pre-Deployment Verification" is confirmed.
/// </summary>
public class NampPreDeploymentChecklistTemplate : Entity
{
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool RequiresDocumentUpload { get; private set; }
    public NampDocumentCategory? DocumentCategory { get; private set; }
    public bool IsMandatory { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    private NampPreDeploymentChecklistTemplate() { }

    public static Result<NampPreDeploymentChecklistTemplate> Create(
        string title,
        string? description,
        bool requiresDocumentUpload,
        NampDocumentCategory? documentCategory,
        bool isMandatory,
        int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure<NampPreDeploymentChecklistTemplate>("Title is required.");

        if (requiresDocumentUpload && documentCategory is null)
            return Result.Failure<NampPreDeploymentChecklistTemplate>("Document category is required when document upload is required.");

        return Result.Success(new NampPreDeploymentChecklistTemplate
        {
            Title = title.Trim(),
            Description = description?.Trim(),
            RequiresDocumentUpload = requiresDocumentUpload,
            DocumentCategory = documentCategory,
            IsMandatory = isMandatory,
            SortOrder = sortOrder,
            IsActive = true
        });
    }

    public Result Update(
        string title,
        string? description,
        bool requiresDocumentUpload,
        NampDocumentCategory? documentCategory,
        bool isMandatory,
        int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure("Title is required.");

        if (requiresDocumentUpload && documentCategory is null)
            return Result.Failure("Document category is required when document upload is required.");

        Title = title.Trim();
        Description = description?.Trim();
        RequiresDocumentUpload = requiresDocumentUpload;
        DocumentCategory = documentCategory;
        IsMandatory = isMandatory;
        SortOrder = sortOrder;
        return Result.Success();
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
