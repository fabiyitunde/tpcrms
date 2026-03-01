namespace CRMS.Domain.Enums;

public enum CreditBureauProvider
{
    CreditRegistry,
    FirstCentral,
    CRC,
    SmartComply
}

public enum SmartComplyReportType
{
    // Individual Credit Reports
    FirstCentralSummary,
    FirstCentralFull,
    FirstCentralScore,
    CreditRegistrySummary,
    CreditRegistryFull,
    CreditRegistryAdvanced,
    CRCScore,
    CRCHistory,
    CRCFull,
    CreditPremium,
    
    // Business Credit Reports
    CRCBusinessHistory,
    FirstCentralBusiness,
    PremiumBusiness,
    
    // Loan Fraud Check
    IndividualLoanFraudCheck,
    BusinessLoanFraudCheck
}

public enum SmartComplyKycType
{
    // Nigeria KYC
    BVN,
    BVNAdvanced,
    BVNWithFace,
    NIN,
    NINWithFace,
    VNIN,
    TIN,
    CAC,
    CACAdvanced,
    DriversLicense,
    Passport,
    VotersId,
    NUBAN,
    PhoneNumberBasic,
    PhoneNumberAdvanced
}

public enum BureauReportStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    NotFound
}

public enum AccountStatus
{
    Performing,
    NonPerforming,
    WrittenOff,
    Closed,
    Unknown
}

public enum DelinquencyLevel
{
    Current,
    Days1To30,
    Days31To60,
    Days61To90,
    Days91To120,
    Days121To150,
    Days151To180,
    Days181To360,
    Over360Days
}

public enum LegalStatus
{
    None,
    Litigation,
    Foreclosure,
    Bankruptcy,
    Other
}

public enum SubjectType
{
    Individual,
    Business
}
