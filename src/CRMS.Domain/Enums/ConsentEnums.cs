namespace CRMS.Domain.Enums;

public enum ConsentType
{
    CreditBureauCheck,
    DataProcessing,
    Marketing,
    ThirdPartySharing
}

public enum ConsentStatus
{
    Active,
    Expired,
    Revoked
}

public enum ConsentCaptureMethod
{
    Digital,
    Physical,
    Verbal,
    API
}
