using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.ProductCatalog;

/// <summary>
/// A single conditions-precedent/subsequent item that is part of a loan product's
/// disbursement checklist template. Admin-configurable per product.
/// Instantiated into DisbursementChecklistItem records when an offer letter is issued.
/// </summary>
public class DisbursementChecklistTemplate : Entity
{
    public Guid LoanProductId { get; private set; }
    public string ItemName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsMandatory { get; private set; }
    public ConditionType ConditionType { get; private set; }

    /// <summary>
    /// For Subsequent conditions: days after DisbursedAt before the item is considered overdue.
    /// Null for Precedent items.
    /// </summary>
    public int? SubsequentDueDays { get; private set; }

    /// <summary>LoanOfficer must upload an evidence document when satisfying this item.</summary>
    public bool RequiresDocumentUpload { get; private set; }

    /// <summary>LegalOfficer must ratify after the document is uploaded.</summary>
    public bool RequiresLegalRatification { get; private set; }

    /// <summary>Whether a waiver can be proposed for this item.</summary>
    public bool CanBeWaived { get; private set; }

    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    private DisbursementChecklistTemplate() { }

    internal static Result<DisbursementChecklistTemplate> Create(
        Guid loanProductId,
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
        if (string.IsNullOrWhiteSpace(itemName))
            return Result.Failure<DisbursementChecklistTemplate>("Item name is required");

        if (conditionType == ConditionType.Subsequent && (subsequentDueDays == null || subsequentDueDays <= 0))
            return Result.Failure<DisbursementChecklistTemplate>("Subsequent conditions must have a positive due-days value");

        if (conditionType == ConditionType.Precedent && subsequentDueDays != null)
            return Result.Failure<DisbursementChecklistTemplate>("Precedent conditions cannot have a subsequent due-days value");

        return Result.Success(new DisbursementChecklistTemplate
        {
            LoanProductId = loanProductId,
            ItemName = itemName.Trim(),
            Description = description.Trim(),
            IsMandatory = isMandatory,
            ConditionType = conditionType,
            SubsequentDueDays = subsequentDueDays,
            RequiresDocumentUpload = requiresDocumentUpload,
            RequiresLegalRatification = requiresLegalRatification,
            CanBeWaived = canBeWaived,
            SortOrder = sortOrder,
            IsActive = true
        });
    }

    public Result Update(
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
        if (string.IsNullOrWhiteSpace(itemName))
            return Result.Failure("Item name is required");

        if (conditionType == ConditionType.Subsequent && (subsequentDueDays == null || subsequentDueDays <= 0))
            return Result.Failure("Subsequent conditions must have a positive due-days value");

        if (conditionType == ConditionType.Precedent && subsequentDueDays != null)
            return Result.Failure("Precedent conditions cannot have a subsequent due-days value");

        ItemName = itemName.Trim();
        Description = description.Trim();
        IsMandatory = isMandatory;
        ConditionType = conditionType;
        SubsequentDueDays = subsequentDueDays;
        RequiresDocumentUpload = requiresDocumentUpload;
        RequiresLegalRatification = requiresLegalRatification;
        CanBeWaived = canBeWaived;
        SortOrder = sortOrder;

        return Result.Success();
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
