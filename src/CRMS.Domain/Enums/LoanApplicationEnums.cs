namespace CRMS.Domain.Enums;

public enum LoanApplicationType
{
    Retail,
    Corporate
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
    Cancelled
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
    Other
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

public enum OfferAcceptanceMethod
{
    InBranchSigning,
    Courier,
    Electronic
}
