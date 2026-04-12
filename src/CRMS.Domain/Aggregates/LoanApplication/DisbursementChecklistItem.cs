using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.LoanApplication;

/// <summary>
/// A per-application instance of a checklist item, copied from the loan product's
/// DisbursementChecklistTemplate when the offer letter is issued to the customer.
/// Tracks the full lifecycle of each condition from Pending through to Satisfied or Waived.
/// </summary>
public class DisbursementChecklistItem : Entity
{
    public Guid LoanApplicationId { get; private set; }

    /// <summary>The template item this was copied from (snapshot FK — kept for audit/traceability).</summary>
    public Guid TemplateItemId { get; private set; }

    // Snapshot of template fields at time of offer issuance
    public string ItemName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsMandatory { get; private set; }
    public ConditionType ConditionType { get; private set; }
    public int? SubsequentDueDays { get; private set; }
    public bool RequiresDocumentUpload { get; private set; }
    public bool RequiresLegalRatification { get; private set; }
    public bool CanBeWaived { get; private set; }
    public int SortOrder { get; private set; }

    // Current status
    public ChecklistItemStatus Status { get; private set; }

    // Satisfaction audit
    public Guid? SatisfiedByUserId { get; private set; }
    public DateTime? SatisfiedAt { get; private set; }

    // Evidence document (uploaded when satisfying)
    public Guid? EvidenceDocumentId { get; private set; }

    // Legal ratification audit
    public Guid? LegalRatifiedByUserId { get; private set; }
    public DateTime? LegalRatifiedAt { get; private set; }
    public string? LegalReturnReason { get; private set; }

    // Waiver audit
    public Guid? WaiverProposedByUserId { get; private set; }
    public DateTime? WaiverProposedAt { get; private set; }
    public string? WaiverReason { get; private set; }
    public Guid? WaiverRatifiedByUserId { get; private set; }
    public DateTime? WaiverRatifiedAt { get; private set; }
    public string? WaiverRejectionReason { get; private set; }

    // CS due-date management
    public DateTime? DueDate { get; private set; }
    public Guid? ExtensionRequestedByUserId { get; private set; }
    public DateTime? ExtensionRequestedAt { get; private set; }
    public string? ExtensionReason { get; private set; }
    public DateTime? OriginalDueDate { get; private set; }
    public Guid? ExtensionRatifiedByUserId { get; private set; }
    public DateTime? ExtensionRatifiedAt { get; private set; }

    private DisbursementChecklistItem() { }

    /// <summary>Creates a new checklist item copied from a template item.</summary>
    internal static DisbursementChecklistItem FromTemplate(
        Guid loanApplicationId,
        Guid templateItemId,
        string itemName,
        string description,
        bool isMandatory,
        ConditionType conditionType,
        int? subsequentDueDays,
        bool requiresDocumentUpload,
        bool requiresLegalRatification,
        bool canBeWaived,
        int sortOrder)
    {
        return new DisbursementChecklistItem
        {
            LoanApplicationId = loanApplicationId,
            TemplateItemId = templateItemId,
            ItemName = itemName,
            Description = description,
            IsMandatory = isMandatory,
            ConditionType = conditionType,
            SubsequentDueDays = subsequentDueDays,
            RequiresDocumentUpload = requiresDocumentUpload,
            RequiresLegalRatification = requiresLegalRatification,
            CanBeWaived = canBeWaived,
            SortOrder = sortOrder,
            Status = ChecklistItemStatus.Pending
        };
    }

    // -------------------------------------------------------------------------
    // Satisfy (non-legal items — LoanOfficer satisfies directly)
    // -------------------------------------------------------------------------

    public Result Satisfy(Guid userId, Guid? evidenceDocumentId)
    {
        if (Status is ChecklistItemStatus.Satisfied or ChecklistItemStatus.Waived)
            return Result.Failure("Item is already resolved");

        if (RequiresLegalRatification)
            return Result.Failure("This item requires Legal ratification — use SubmitForLegalReview instead");

        if (RequiresDocumentUpload && evidenceDocumentId == null)
            return Result.Failure("An evidence document must be uploaded to satisfy this item");

        Status = ChecklistItemStatus.Satisfied;
        SatisfiedByUserId = userId;
        SatisfiedAt = DateTime.UtcNow;
        EvidenceDocumentId = evidenceDocumentId;

        return Result.Success();
    }

    // -------------------------------------------------------------------------
    // Legal review path
    // -------------------------------------------------------------------------

    public Result SubmitForLegalReview(Guid userId, Guid evidenceDocumentId)
    {
        if (Status is ChecklistItemStatus.Satisfied or ChecklistItemStatus.Waived)
            return Result.Failure("Item is already resolved");

        if (!RequiresLegalRatification)
            return Result.Failure("This item does not require Legal ratification");

        Status = ChecklistItemStatus.PendingLegalReview;
        EvidenceDocumentId = evidenceDocumentId;
        SatisfiedByUserId = userId; // The uploader — will be overwritten on Legal ratification
        SatisfiedAt = null;

        return Result.Success();
    }

    public Result RatifyByLegal(Guid legalOfficerUserId)
    {
        if (Status != ChecklistItemStatus.PendingLegalReview)
            return Result.Failure("Item is not pending legal review");

        Status = ChecklistItemStatus.Satisfied;
        LegalRatifiedByUserId = legalOfficerUserId;
        LegalRatifiedAt = DateTime.UtcNow;
        SatisfiedAt = DateTime.UtcNow;
        LegalReturnReason = null;

        return Result.Success();
    }

    public Result ReturnByLegal(Guid legalOfficerUserId, string reason)
    {
        if (Status != ChecklistItemStatus.PendingLegalReview)
            return Result.Failure("Item is not pending legal review");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("A rejection reason is required");

        Status = ChecklistItemStatus.LegalReturned;
        LegalRatifiedByUserId = legalOfficerUserId;
        LegalReturnReason = reason;
        EvidenceDocumentId = null; // Cleared — LoanOfficer must re-upload
        SatisfiedByUserId = null;

        return Result.Success();
    }

    // -------------------------------------------------------------------------
    // Waiver path (LoanOfficer proposes → RiskManager ratifies)
    // -------------------------------------------------------------------------

    public Result ProposeWaiver(Guid proposedByUserId, string reason)
    {
        if (!CanBeWaived)
            return Result.Failure("This item cannot be waived");

        if (Status is ChecklistItemStatus.Satisfied or ChecklistItemStatus.Waived)
            return Result.Failure("Item is already resolved");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("A waiver reason is required");

        Status = ChecklistItemStatus.WaiverPending;
        WaiverProposedByUserId = proposedByUserId;
        WaiverProposedAt = DateTime.UtcNow;
        WaiverReason = reason;

        return Result.Success();
    }

    public Result ApproveWaiver(Guid riskManagerUserId)
    {
        if (Status != ChecklistItemStatus.WaiverPending)
            return Result.Failure("Item does not have a pending waiver");

        Status = ChecklistItemStatus.Waived;
        WaiverRatifiedByUserId = riskManagerUserId;
        WaiverRatifiedAt = DateTime.UtcNow;
        WaiverRejectionReason = null;

        return Result.Success();
    }

    public Result RejectWaiver(Guid riskManagerUserId, string rejectionReason)
    {
        if (Status != ChecklistItemStatus.WaiverPending)
            return Result.Failure("Item does not have a pending waiver");

        if (string.IsNullOrWhiteSpace(rejectionReason))
            return Result.Failure("A rejection reason is required");

        Status = ChecklistItemStatus.Pending;
        WaiverRatifiedByUserId = riskManagerUserId;
        WaiverRejectionReason = rejectionReason;
        WaiverProposedByUserId = null;
        WaiverProposedAt = null;
        WaiverReason = null;

        return Result.Success();
    }

    // -------------------------------------------------------------------------
    // CS due-date management (set at disbursement, managed post-disbursement)
    // -------------------------------------------------------------------------

    /// <summary>Called at disbursement time to set the due date for Subsequent items.</summary>
    internal void SetDueDate(DateTime disbursedAt)
    {
        if (ConditionType != ConditionType.Subsequent || SubsequentDueDays == null)
            return;

        DueDate = disbursedAt.AddDays(SubsequentDueDays.Value);
    }

    /// <summary>Called by CsMonitoringBackgroundService when DueDate has passed.</summary>
    public void MarkOverdue()
    {
        if (Status == ChecklistItemStatus.Pending)
            Status = ChecklistItemStatus.Overdue;
    }

    public Result RequestExtension(Guid requestedByUserId, string reason)
    {
        if (ConditionType != ConditionType.Subsequent)
            return Result.Failure("Extensions only apply to Subsequent conditions");

        if (Status is not (ChecklistItemStatus.Pending or ChecklistItemStatus.Overdue))
            return Result.Failure("Extensions can only be requested for Pending or Overdue items");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("An extension reason is required");

        Status = ChecklistItemStatus.ExtensionPending;
        ExtensionRequestedByUserId = requestedByUserId;
        ExtensionRequestedAt = DateTime.UtcNow;
        ExtensionReason = reason;

        return Result.Success();
    }

    public Result ApproveExtension(Guid riskManagerUserId, int additionalDays)
    {
        if (Status != ChecklistItemStatus.ExtensionPending)
            return Result.Failure("Item does not have a pending extension request");

        if (additionalDays <= 0)
            return Result.Failure("Additional days must be greater than zero");

        OriginalDueDate ??= DueDate;
        DueDate = DateTime.UtcNow.AddDays(additionalDays);
        Status = ChecklistItemStatus.Pending;
        ExtensionRatifiedByUserId = riskManagerUserId;
        ExtensionRatifiedAt = DateTime.UtcNow;

        return Result.Success();
    }

    public Result RejectExtension(Guid riskManagerUserId)
    {
        if (Status != ChecklistItemStatus.ExtensionPending)
            return Result.Failure("Item does not have a pending extension request");

        // Restore to overdue if it was overdue when the request was made, else pending
        Status = DueDate.HasValue && DueDate < DateTime.UtcNow
            ? ChecklistItemStatus.Overdue
            : ChecklistItemStatus.Pending;

        ExtensionRatifiedByUserId = riskManagerUserId;
        ExtensionRatifiedAt = DateTime.UtcNow;

        return Result.Success();
    }

    // -------------------------------------------------------------------------
    // Computed helpers
    // -------------------------------------------------------------------------

    /// <summary>True if this CP item is fully resolved (counts toward the disbursement gate).</summary>
    public bool IsResolved =>
        Status is ChecklistItemStatus.Satisfied or ChecklistItemStatus.Waived;

    /// <summary>True if this item blocks the OfferAccepted confirmation gate.</summary>
    public bool BlocksDisbursement =>
        ConditionType == ConditionType.Precedent && IsMandatory && !IsResolved;
}
