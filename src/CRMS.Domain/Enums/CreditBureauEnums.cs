namespace CRMS.Domain.Enums;

public enum CreditBureauProvider
{
    CreditRegistry,
    FirstCentral,
    CRC
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
