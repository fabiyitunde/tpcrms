using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Namp;

/// <summary>
/// A per-application instance of a NampPreDeploymentChecklistTemplate.
/// Seeded from the active templates when pre-deployment verification begins.
/// </summary>
public class NampPreDeploymentChecklistItem : Entity
{
    public Guid NampApplicationId { get; private set; }
    public Guid TemplateItemId { get; private set; }

    // Snapshot from template at time of seeding
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool RequiresDocumentUpload { get; private set; }
    public NampDocumentCategory? DocumentCategory { get; private set; }
    public bool IsMandatory { get; private set; }
    public int SortOrder { get; private set; }

    // Confirmation state
    public bool? IsConfirmed { get; private set; }
    public Guid? ConfirmedByUserId { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public string? Notes { get; private set; }

    private NampPreDeploymentChecklistItem() { }

    public static NampPreDeploymentChecklistItem FromTemplate(
        Guid nampApplicationId,
        NampPreDeploymentChecklistTemplate template)
    {
        return new NampPreDeploymentChecklistItem
        {
            NampApplicationId = nampApplicationId,
            TemplateItemId = template.Id,
            Title = template.Title,
            Description = template.Description,
            RequiresDocumentUpload = template.RequiresDocumentUpload,
            DocumentCategory = template.DocumentCategory,
            IsMandatory = template.IsMandatory,
            SortOrder = template.SortOrder
        };
    }

    public void SetConfirmation(Guid userId, bool? isConfirmed, string? notes)
    {
        IsConfirmed = isConfirmed;
        Notes = notes;

        if (isConfirmed == true)
        {
            ConfirmedByUserId = userId;
            ConfirmedAt = DateTime.UtcNow;
        }
        else
        {
            ConfirmedByUserId = null;
            ConfirmedAt = null;
        }
    }

    public bool BlocksCompletion => IsMandatory && IsConfirmed != true;
}
