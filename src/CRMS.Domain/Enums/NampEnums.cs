namespace CRMS.Domain.Enums;

public enum NampApplicationStatus
{
    // ── Pre-stage ──────────────────────────────────────────────
    Received,               // System: payload ingested, saved to staging table
    RecallPending,          // Waiting for Loan Officer to recall from staging queue

    // ── Stage 1: Loan Officer ──────────────────────────────────
    Draft,                  // Recalled from staging; being reviewed by Loan Officer
    Submitted,              // Submitted for Technical Appraisal

    // ── Stage 2: Technical Appraisal ──────────────────────────
    TechnicalAppraisal,     // Agricultural Engineer reviewing
    TechnicalDeclined,      // Terminal: failed technical appraisal

    // ── Stage 3: Financial Appraisal ──────────────────────────
    FinancialAppraisal,     // Credit Officer reviewing
    FinancialDeclined,      // Terminal: failed financial appraisal

    // ── Stage 4a: Branch Credit Committee ─────────────────────
    BranchCommitteeCirculation,
    BranchCommitteeDeclined,    // Terminal

    // ── Stage 4b: Zonal Credit Committee ──────────────────────
    ZonalCommitteeCirculation,
    ZonalCommitteeDeclined,     // Terminal

    // ── Stage 4c: Regional Credit Committee ───────────────────
    RegionalCommitteeCirculation,
    RegionalCommitteeDeclined,  // Terminal

    // ── Stage 4d: HO Credit Committee ─────────────────────────
    HOCommitteeCirculation,
    HOCommitteeDeclined,        // Terminal

    // ── Stage 5: Ratification & Offer ─────────────────────────
    Ratification,           // Final Approver (at relevant tier) ratifying committee vote
    RatificationDeclined,   // Terminal: Final Approver declined to ratify
    OfferGenerated,         // Offer letter generated; awaiting applicant countersignature
    OfferAccepted,          // Applicant countersigned; Loan Officer uploaded docs
    OfferLapsed,            // Terminal: applicant did not countersign within SLA

    // ── Stage 6: Pre-Deployment Verification ──────────────────
    PreDeploymentVerification,  // Compliance Officer checking 4 gate conditions

    // ── Stage 7: Training ─────────────────────────────────────
    Training,               // BOA Training Coordinator tracking; Heifer Nigeria delivering

    // ── Stage 8: Deployment ───────────────────────────────────
    Deployment,             // Heifer Nigeria / Vendor delivering; BOA Deployment Officer tracking

    // ── Stage 9: Active ───────────────────────────────────────
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
    TechnicalAppraisal,
    FinancialAppraisal,
    PreDeploymentVerification,
    Training,
    Deployment,
    Offer,
    Other,
}

public enum NampDocumentCategory
{
    General,              // Default / unclassified
    TechnicalReport,      // Required at Technical Appraisal (at least 1)
    SitePhoto,
    SoilTestResult,
    FinancialModel,       // Required at Financial Appraisal (at least 1)
    CreditReport,
    SupportingDocument,
    SignedNampOfferLetter, // Countersigned offer letter uploaded post-ratification
    EquityDepositReceipt,  // Gate 1: Equity deposit payment evidence
    LeaseAgreement,        // Gate 2: Signed lease/hire-purchase agreement
    GpsConsentForm,        // Gate 3: Signed GPS tracking consent form
    InsuranceCertificate,  // Gate 4: NAIC insurance certificate
}

public enum NampSoilConditionRating
{
    Excellent,
    Good,
    Fair,
    Poor,
}

public enum NampViabilityRating
{
    Viable,
    Marginal,
    NotViable,
}

public enum NampTechnicalRecommendation
{
    Pass,
    Fail,
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
