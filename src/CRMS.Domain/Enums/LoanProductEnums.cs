namespace CRMS.Domain.Enums;

public enum LoanProductType
{
    Retail,
    Corporate
}

public enum ProductStatus
{
    Draft,
    Active,
    Suspended,
    Discontinued
}

public enum EligibilityRuleType
{
    Age,
    MinIncome,
    MaxIncome,
    Geography,
    Sector,
    CreditScore,
    ExistingExposure,
    DebtToIncome,
    EmploymentType,
    BusinessAge
}

public enum ComparisonOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    GreaterOrEqual,
    LessOrEqual,
    In,
    NotIn,
    Between
}

public enum DocumentType
{
    BankStatement,
    AuditedFinancials,
    IdentityDocument,
    AddressProof,
    IncomeProof,
    BusinessRegistration,
    BoardResolution,
    TaxClearance,
    CAC,
    MemorandumOfAssociation
}
