namespace CRMS.Application.OfferAcceptance.DTOs;

public record DisbursementChecklistDto(
    Guid LoanApplicationId,
    bool AllPrecedentResolved,
    List<ChecklistItemDto> Items
);

public record ChecklistItemDto(
    Guid Id,
    Guid TemplateItemId,
    string ItemName,
    string Description,
    bool IsMandatory,
    string ConditionType,
    int? SubsequentDueDays,
    bool RequiresDocumentUpload,
    bool RequiresLegalRatification,
    bool CanBeWaived,
    int SortOrder,
    string Status,
    bool IsResolved,
    bool BlocksDisbursement,
    // Satisfaction
    Guid? SatisfiedByUserId,
    DateTime? SatisfiedAt,
    Guid? EvidenceDocumentId,
    // Legal
    Guid? LegalRatifiedByUserId,
    DateTime? LegalRatifiedAt,
    string? LegalReturnReason,
    // Waiver
    Guid? WaiverProposedByUserId,
    DateTime? WaiverProposedAt,
    string? WaiverReason,
    Guid? WaiverRatifiedByUserId,
    DateTime? WaiverRatifiedAt,
    string? WaiverRejectionReason,
    // CS due date
    DateTime? DueDate,
    DateTime? OriginalDueDate,
    string? ExtensionReason
);

public record ChecklistTemplateItemDto(
    Guid Id,
    Guid LoanProductId,
    string ItemName,
    string Description,
    bool IsMandatory,
    string ConditionType,
    int? SubsequentDueDays,
    bool RequiresDocumentUpload,
    bool RequiresLegalRatification,
    bool CanBeWaived,
    int SortOrder,
    bool IsActive
);
