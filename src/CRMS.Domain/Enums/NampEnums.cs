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

    // ── Stage 3: Risk Review ──────────────────────────────────
    RiskReview,             // Risk Officer reviewing before committee circulation
    RiskDeclined,           // Terminal: Risk Officer declined

    // ── Stage 4: Committee ────────────────────────────────────
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
    OfferAccepted,          // Applicant countersigned; immediately auto-routed to LegalClearance
    OfferLapsed,            // Terminal: applicant did not countersign within SLA

    // ── Stage 5: Legal Clearance ───────────────────────────────
    LegalClearance,         // Legal Officer reviewing before pre-deployment
    LegalReturned,          // Legal returned to LO for remediation; LO resubmits
    LegalDeclined,          // Terminal: Legal Officer hard-declined

    // ── Stage 6: Pre-Deployment Verification ──────────────────
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
    LegalClearance,
    Other,
}

public enum NampDocumentCategory
{
    General = 0,              // Default / unclassified
    SitePhoto = 1,
    FinancialModel = 2,       // Required at Financial Appraisal (at least 1)
    CreditReport = 3,
    SupportingDocument = 4,
    SignedNampOfferLetter = 5, // Countersigned offer letter uploaded post-ratification
    EquityDepositReceipt = 6,  // Gate 1: Equity deposit payment evidence
    LeaseAgreement = 7,        // Gate 2: Signed lease/hire-purchase agreement
    GpsConsentForm = 8,        // Gate 3: Signed GPS tracking consent form
    InsuranceCertificate = 9,  // Gate 4: NAIC insurance certificate
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

public enum NampDocumentType
{
    OfferLetter = 0,
    LeaseAgreement = 1,
    GpsConsentForm = 2,
}

public enum NampRepaymentSource
{
    PrimaryIncome,
    RentalHireRevenue,
    Mixed,
    CompanyCashFlow,
}
