namespace CRMS.Infrastructure.ExternalServices.SmartComply;

public static class SmartComplyEndpoints
{
    // Individual Credit Report Endpoints
    public static class Individual
    {
        public const string FirstCentralSummary = "/api/onboarding/individual/first_central_summary/";
        public const string FirstCentralFull = "/api/onboarding/individual/first_central_full/";
        public const string FirstCentralScore = "/api/onboarding/individual/first_central_score/";
        public const string CreditRegistrySummary = "/api/onboarding/individual/credit_registry_summary/";
        public const string CreditRegistryFull = "/api/onboarding/individual/credit_registry_full/";
        public const string CreditRegistryAdvanced = "/api/onboarding/individual/credit_registry_advanced/";
        public const string CRCScore = "/api/onboarding/individual/crc_score/";
        public const string CRCHistory = "/api/onboarding/individual/crc_history/";
        public const string CRCFull = "/api/onboarding/individual/crc_full/";
        public const string CreditPremium = "/api/onboarding/individual/credit_premium/";
    }
    
    // Business Credit Report Endpoints
    public static class Business
    {
        public const string CRCHistory = "/api/onboarding/business/crc/";
        public const string FirstCentral = "/api/onboarding/business/first_central/";
        public const string Premium = "/api/onboarding/business/premium/";
    }
    
    // Loan Fraud Check Endpoints
    public static class LoanFraud
    {
        public const string FraudCheck = "/api/v1/loan/fraud_check/";
    }
    
    // KYC/Identity Verification Endpoints - Nigeria
    public static class KycNigeria
    {
        public const string BVN = "/api/onboarding/nigeria_kyc/bvn/";
        public const string BVNAdvanced = "/api/onboarding/nigeria_kyc/bvn_advanced/";
        public const string BVNWithFace = "/api/onboarding/nigeria_kyc/bvn_with_face/";
        public const string NIN = "/api/onboarding/nigeria_kyc/nin/";
        public const string NINWithFace = "/api/onboarding/nigeria_kyc/nin_with_face/";
        public const string VNIN = "/api/onboarding/nigeria_kyc/vnin/";
        public const string TIN = "/api/onboarding/nigeria_kyc/tin/";
        public const string CAC = "/api/onboarding/nigeria_kyc/cac/";
        public const string CACAdvanced = "/api/onboarding/nigeria_kyc/cac_advanced/";
        public const string DriversLicense = "/api/onboarding/nigeria_kyc/drivers_license/";
        public const string Passport = "/api/onboarding/nigeria_kyc/passport/";
        public const string VotersId = "/api/onboarding/nigeria_kyc/voters_id/";
        public const string NUBAN = "/api/onboarding/nigeria_kyc/nuban/";
        public const string PhoneNumberBasic = "/api/onboarding/nigeria_kyc/phone_number_basic/";
        public const string PhoneNumberAdvanced = "/api/onboarding/nigeria_kyc/phone_number_advanced/";
    }
}
