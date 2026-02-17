namespace CRMS.Domain.Enums;

public enum RiskRating
{
    VeryLow,
    Low,
    Medium,
    High,
    VeryHigh
}

public enum AdvisoryRecommendation
{
    StrongApprove,
    Approve,
    ApproveWithConditions,
    Refer,
    Decline
}

public enum AdvisoryStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

public enum RiskCategory
{
    CreditHistory,
    FinancialHealth,
    CashflowStability,
    DebtServiceCapacity,
    CollateralCoverage,
    ManagementRisk,
    IndustryRisk,
    ConcentrationRisk
}

public enum ParameterDataType
{
    Decimal,
    Percentage,
    Integer,
    Score,
    Currency
}

public enum ParameterChangeStatus
{
    None,
    Pending,
    Approved,
    Rejected
}
