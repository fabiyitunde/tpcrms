namespace CRMS.Web.Intranet.Models;

/// <summary>
/// String constants for LoanApplication status values, matching the LoanApplicationStatus domain enum.
/// Use these instead of inline string literals to prevent silent typo bugs.
/// </summary>
public static class AppStatus
{
    public const string Draft = "Draft";
    public const string BranchReview = "BranchReview";
    public const string HOReview = "HOReview";
    public const string CreditAnalysis = "CreditAnalysis";
    public const string CommitteeCirculation = "CommitteeCirculation";
    public const string CommitteeApproved = "CommitteeApproved";
    public const string CommitteeRejected = "CommitteeRejected";
    public const string FinalApproval = "FinalApproval";
    public const string Approved = "Approved";
    public const string OfferGenerated = "OfferGenerated";
    public const string OfferAccepted = "OfferAccepted";
    public const string Rejected = "Rejected";
    public const string Disbursed = "Disbursed";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";
}
