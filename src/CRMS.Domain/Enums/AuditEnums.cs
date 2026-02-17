namespace CRMS.Domain.Enums;

public enum AuditAction
{
    // CRUD
    Create,
    Read,
    Update,
    Delete,
    
    // Loan Application
    Submit,
    Approve,
    Reject,
    Return,
    Escalate,
    
    // Workflow
    StatusChange,
    Assign,
    Unassign,
    
    // Committee
    Vote,
    Comment,
    Decision,
    
    // Credit
    CreditCheck,
    BureauRequest,
    BureauResponse,
    
    // Financial
    StatementUpload,
    StatementAnalysis,
    RatioCalculation,
    
    // Advisory
    AdvisoryGenerated,
    ScoreCalculated,
    
    // Configuration
    ConfigChange,
    ConfigApprove,
    ConfigReject,
    
    // Security
    Login,
    Logout,
    LoginFailed,
    PasswordChange,
    PasswordReset,
    RoleChange,
    PermissionChange,
    
    // Document
    DocumentUpload,
    DocumentDownload,
    DocumentDelete,
    DocumentVerify,
    
    // Integration
    ExternalApiCall,
    ExternalApiResponse,
    
    // Override
    Override,
    
    // Export
    Export,
    Print
}

public enum AuditCategory
{
    Authentication,
    Authorization,
    LoanApplication,
    CreditBureau,
    FinancialAnalysis,
    StatementAnalysis,
    Collateral,
    Guarantor,
    Workflow,
    Committee,
    Advisory,
    Configuration,
    Document,
    Integration,
    Security,
    DataAccess,
    System
}

public enum SensitiveDataType
{
    BVN,
    NIN,
    CreditReport,
    BankStatement,
    FinancialStatement,
    PersonalInformation,
    ContactInformation,
    EmploymentInformation,
    IncomeInformation,
    CollateralDetails,
    GuarantorDetails
}

public enum DataAccessType
{
    View,
    Download,
    Export,
    Print,
    Share
}
