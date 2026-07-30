namespace CRMS.Domain.Enums;

public enum LoanApplicationType
{
    Retail,
    Corporate,
    Namp,
}

public enum LoanApplicationStatus
{
    Draft,
    Submitted,
    DataGathering,
    BranchReview,
    BranchApproved,
    BranchReturned,
    BranchRejected,
    CreditAnalysis,
    HOReview,
    LegalReview,
    LegalApproval,
    CommitteeCirculation,
    CommitteeApproved,
    CommitteeRejected,
    FinalApproval,
    Approved,
    Rejected,
    OfferGenerated,
    OfferAccepted,
    SecurityPerfection,
    SecurityApproval,
    DisbursementPending,
    DisbursementBranchApproval,
    DisbursementHQApproval,
    Disbursed,
    Closed,
    Cancelled,

    // Streamlined corporate loan flow (2026-06-27)
    CreditReview,           // Replaces BranchReview/CreditAnalysis/HOReview/LegalReview/LegalApproval
    Ratification,           // Replaces FinalApproval/Approved — offer letter generated here
    Disbursement,           // Replaces DisbursementPending/DisbursementHQApproval
    RatificationDeclined,   // FinalApprover declined to ratify — terminal
}

public enum DocumentCategory
{
    BankStatement,
    AuditedFinancials,
    IdentityDocument,
    CompanyRegistration,
    BoardResolution,
    TaxClearance,
    CollateralDocument,
    SignedDisbursementMemo,
    SignedOfferLetter,
    SignedKfsAcknowledgement,
    Other
}

public enum OfferAcceptanceMethod
{
    InBranchSigning,
    Courier,
    Electronic
}

public enum DocumentStatus
{
    Pending,
    Uploaded,
    Verified,
    Rejected
}

public enum PartyType
{
    Director,
    Signatory,
    Guarantor,
    BeneficialOwner
}

//public enum OfferAcceptanceMethod
//{
//    InBranchSigning,
//    Courier,
//    Electronic
//}
