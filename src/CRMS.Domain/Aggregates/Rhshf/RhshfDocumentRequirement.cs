using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// Admin-configured "this document must be attached before a case can be submitted" rule.
///
/// The FAC profiling form previously had no required-document concept at all: a single uncategorised
/// file input, unlimited uploads, and nothing stopping submission with zero attachments. Credit
/// officers then discovered the gap at appraisal, after the case had already entered the pipeline.
///
/// Same template shape as RhshfPreDeploymentChecklistTemplate — admin-editable, mandatory flag,
/// sort order, soft-deactivated rather than deleted so historical cases stay explicable.
/// </summary>
public class RhshfDocumentRequirement : Entity
{
    public RhshfDocumentCategory Category { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsMandatory { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    protected RhshfDocumentRequirement() { }

    public static Result<RhshfDocumentRequirement> Create(
        RhshfDocumentCategory category, string title, string? description, bool isMandatory, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure<RhshfDocumentRequirement>("Title is required.");

        return Result.Success(new RhshfDocumentRequirement
        {
            Category = category,
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
