namespace CRMS.Domain.Enums;

public enum LoanPackStatus
{
    Generating,
    Generated,
    Failed,
    Archived
}

public enum LoanPackSection
{
    CoverPage,
    ExecutiveSummary,
    CustomerProfile,
    DirectorsAndSignatories,
    BureauReports,
    FinancialStatements,
    FinancialRatios,
    CashflowAnalysis,
    CollateralDetails,
    GuarantorDetails,
    AIAdvisory,
    RiskScoreMatrix,
    Recommendations,
    WorkflowHistory,
    CommitteeComments,
    Appendices
}
