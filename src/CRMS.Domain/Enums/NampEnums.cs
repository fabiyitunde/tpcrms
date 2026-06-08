namespace CRMS.Domain.Enums;

public enum NampApplicationStatus
{
    // ── Pre-stage ──────────────────────────────────────────────
    Received,               // System: payload ingested, saved to staging table
    RecallPending,          // Waiting for Loan Officer to recall from staging queue

    // ── Stage 1: Loan Officer ──────────────────────────────────
    Draft,                  // Recalled from staging; being reviewed by Loan Officer
    Submitted,              // Submitted for Financial Appraisal

    // ── Stage 2: Financial Appraisal ──────────────────────────
    FinancialAppraisal,     // Credit Officer reviewing
    FinancialDeclined,      // Terminal: failed financial appraisal

    // ── Stage 3: Committee ────────────────────────────────────
    BranchCommitteeCirculation,
    BranchCommitteeDeclined,    // Terminal

    ZonalCommitteeCirculation,
    ZonalCommitteeDeclined,     // Terminal

    RegionalCommitteeCirculation,
    RegionalCommitteeDeclined,  // Terminal

    HOCommitteeCirculation,
    HOCommitteeDeclined,        // Terminal

    // ── Stage 4: Ratification & Offer ─────────────────────────
    Ratification,           // Final Approver (at relevant tier) ratifying committee vote
    RatificationDeclined,   // Terminal: Final Approver declined to ratify
    OfferGenerated,         // Offer letter generated; awaiting applicant countersignature
    OfferAccepted,          // Applicant countersigned; Loan Officer uploaded docs
    OfferLapsed,            // Terminal: applicant did not countersign within SLA

    // ── Stage 5: Pre-Deployment Verification ──────────────────
    PreDeploymentVerification,  // Deployment Officer checking 4 gate conditions

    // ── Stage 6: Deployment ───────────────────────────────────
    Deployment,             // Deployment Officer tracking equipment delivery and GPS activation

    // ── Stage 7: Active ───────────────────────────────────────
    Active,                 // GPS confirmed; PAYS repayment cycle running

    // ── Terminal ───────────────────────────────────────────────
    Closed,                 // Full PAYS repayment completed
    Declined,               // Generic decline used for outbound NAMP callback
}

public enum NampApplicantCategory
{
    YouthAgripreneur,
    WomenAgripreneur,
    AgroServiceCompany,
}

public enum NampCommitteeTier
{
    Branch,
    Zonal,
    Regional,
    HeadOffice,
}

public enum NampCallbackStatus
{
    Received,
    Approved,
    Declined,
    Active,
}

/// <summary>
/// Tracks which stage a document was uploaded at, for context on the Documents tab.
/// </summary>
public enum NampDocumentStage
{
    Origination,
    FinancialAppraisal,
    PreDeploymentVerification,
    Deployment,
    Offer,
    Other,
}

public enum NampDocumentCategory
{
    General,              // Default / unclassified
    SitePhoto,
    FinancialModel,       // Required at Financial Appraisal (at least 1)
    CreditReport,
    SupportingDocument,
    SignedNampOfferLetter, // Countersigned offer letter uploaded post-ratification
    EquityDepositReceipt,  // Gate 1: Equity deposit payment evidence
    LeaseAgreement,        // Gate 2: Signed lease/hire-purchase agreement
    GpsConsentForm,        // Gate 3: Signed GPS tracking consent form
    InsuranceCertificate,  // Gate 4: NAIC insurance certificate
}

public enum NampRepaymentCapacityRating
{
    Strong,
    Adequate,
    Marginal,
    Insufficient,
}

public enum NampCreditRecommendation
{
    Pass,
    Fail,
}

public enum NampRepaymentSource
{
    PrimaryIncome,
    RentalHireRevenue,
    Mixed,
    CompanyCashFlow,
}
