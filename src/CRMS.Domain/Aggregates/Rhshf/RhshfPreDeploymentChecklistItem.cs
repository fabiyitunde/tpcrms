using CRMS.Domain.Common;
using CRMS.Domain.Enums;

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

    /// <summary>How this item is satisfied (snapshot from the template). Drives both the UI and whether
    /// confirmation is an officer action (manual/account/collateral) or system-derived (offer docs).</summary>
    public RhshfPreDeploymentVerificationKind Kind { get; private set; }

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
            Kind = template.Kind,
        };

    /// <summary>True for items the officer confirms by hand (everything except the auto offer-documents
    /// kind, which the system derives).</summary>
    public bool IsOfficerConfirmable => Kind != RhshfPreDeploymentVerificationKind.OfferDocuments;

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

    /// <summary>System-derived satisfaction for the auto (offer-documents) kind — set from whether the
    /// signed offer package is on file. Keeps ConfirmedByUserId null (no human attested it) but stamps a
    /// note so the trail is explicit.</summary>
    public void SetAutoSatisfaction(bool satisfied)
    {
        IsConfirmed = satisfied;
        ConfirmedByUserId = null;
        ConfirmedAt = satisfied ? DateTime.UtcNow : null;
        Notes = satisfied
            ? "Verified automatically — signed offer documents on file."
            : "Awaiting the FAC's signed offer documents.";
    }

    public bool BlocksCompletion => IsMandatory && IsConfirmed != true;
}
