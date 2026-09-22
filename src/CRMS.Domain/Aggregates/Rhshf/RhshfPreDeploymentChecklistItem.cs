using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// A per-cycle instance of a RhshfPreDeploymentChecklistTemplate, seeded onto the RhshfCreditProfile
/// the moment it enters PreDeploymentVerification — child entity of the profile aggregate, same
/// pattern as RhshfAppraisal/RhshfRiskReview, not a standalone aggregate root.
/// </summary>
public class RhshfPreDeploymentChecklistItem : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }
    public int CycleNumber { get; private set; }
    public Guid TemplateItemId { get; private set; }

    // Snapshot from the template at time of seeding.
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsMandatory { get; private set; }
    public int SortOrder { get; private set; }

    // Confirmation state.
    public bool? IsConfirmed { get; private set; }
    public Guid? ConfirmedByUserId { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public string? Notes { get; private set; }

    protected RhshfPreDeploymentChecklistItem() { }

    public static RhshfPreDeploymentChecklistItem FromTemplate(
        Guid rhshfCreditProfileId, int cycleNumber, RhshfPreDeploymentChecklistTemplate template)
        => new()
        {
            RhshfCreditProfileId = rhshfCreditProfileId,
            CycleNumber = cycleNumber,
            TemplateItemId = template.Id,
            Title = template.Title,
            Description = template.Description,
            IsMandatory = template.IsMandatory,
            SortOrder = template.SortOrder,
        };

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
