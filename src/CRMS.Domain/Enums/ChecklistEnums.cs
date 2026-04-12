namespace CRMS.Domain.Enums;

/// <summary>
/// Whether a checklist condition must be met before disbursement (Precedent)
/// or can be perfected within a stipulated period after disbursement (Subsequent).
/// </summary>
public enum ConditionType
{
    /// <summary>Must be satisfied or waived before OfferAccepted confirmation.</summary>
    Precedent,

    /// <summary>
    /// Does not block disbursement. Due date = DisbursedAt + SubsequentDueDays.
    /// Monitored post-disbursement with tiered notifications and escalation.
    /// </summary>
    Subsequent
}

/// <summary>
/// Lifecycle status of a single DisbursementChecklistItem on a loan application.
/// </summary>
public enum ChecklistItemStatus
{
    /// <summary>Not yet acted on.</summary>
    Pending,

    /// <summary>LoanOfficer has uploaded the document; awaiting LegalOfficer ratification.</summary>
    PendingLegalReview,

    /// <summary>LegalOfficer rejected the submitted document (with reason). LoanOfficer must re-upload.</summary>
    LegalReturned,

    /// <summary>Item fulfilled — document uploaded and (where required) ratified by Legal.</summary>
    Satisfied,

    /// <summary>LoanOfficer has proposed a waiver; awaiting RiskManager ratification.</summary>
    WaiverPending,

    /// <summary>Waiver approved by RiskManager. Item counts as resolved for gate purposes.</summary>
    Waived,

    /// <summary>
    /// CS item whose DueDate has passed without being satisfied.
    /// Set automatically by CsMonitoringBackgroundService.
    /// </summary>
    Overdue,

    /// <summary>LoanOfficer has requested a due-date extension; awaiting RiskManager approval.</summary>
    ExtensionPending
}
