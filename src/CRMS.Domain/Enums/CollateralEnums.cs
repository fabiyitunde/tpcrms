namespace CRMS.Domain.Enums;

public enum CollateralType
{
    RealEstate,
    Vehicle,
    Equipment,
    Inventory,
    CashDeposit,
    FixedDeposit,
    TreasuryBills,
    Stocks,
    Bonds,
    Invoice,
    ContractProceeds,
    InsurancePolicy,
    Other
}

public enum CollateralStatus
{
    Proposed,
    UnderValuation,
    Valued,
    Approved,
    Rejected,
    Perfected,
    Released,
    Substituted
}

public enum PerfectionStatus
{
    NotStarted,
    InProgress,
    DocumentsCollected,
    LienRegistered,
    Perfected,
    PartiallyPerfected,
    Failed
}

public enum ValuationStatus
{
    Pending,
    InProgress,
    Completed,
    Disputed,
    Expired
}

public enum ValuationType
{
    Initial,
    Revaluation,
    ForcedSale,
    MarketValue,
    Insurance
}

public enum LienType
{
    FirstCharge,
    SecondCharge,
    FloatingCharge,
    FixedCharge,
    Pledge,
    Hypothecation,
    Assignment
}

public enum GuarantorType
{
    Individual,
    Corporate,
    Director,
    Shareholder,
    ThirdParty,
    Government
}

public enum GuaranteeType
{
    Unlimited,
    Limited,
    Joint,
    JointAndSeveral,
    Continuing
}

public enum GuarantorStatus
{
    Proposed,
    PendingVerification,
    CreditCheckPending,
    CreditCheckCompleted,
    Approved,
    Rejected,
    Active,
    Released,
    Defaulted
}
