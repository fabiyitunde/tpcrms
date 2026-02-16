namespace CRMS.Domain.Constants;

public static class Roles
{
    public const string SystemAdmin = "SystemAdmin";
    public const string LoanOfficer = "LoanOfficer";
    public const string CreditOfficer = "CreditOfficer";
    public const string RiskManager = "RiskManager";
    public const string BranchApprover = "BranchApprover";
    public const string HOReviewer = "HOReviewer";
    public const string CommitteeMember = "CommitteeMember";
    public const string FinalApprover = "FinalApprover";
    public const string Operations = "Operations";
    public const string Auditor = "Auditor";
    public const string Customer = "Customer";

    public static readonly string[] AllRoles =
    [
        SystemAdmin,
        LoanOfficer,
        CreditOfficer,
        RiskManager,
        BranchApprover,
        HOReviewer,
        CommitteeMember,
        FinalApprover,
        Operations,
        Auditor,
        Customer
    ];

    public static readonly Dictionary<string, string> RoleDescriptions = new()
    {
        { SystemAdmin, "Full system access" },
        { LoanOfficer, "Initiates and manages corporate loan applications" },
        { CreditOfficer, "Reviews referred applications and makes credit decisions" },
        { RiskManager, "Senior staff with override authority and risk analysis" },
        { BranchApprover, "Branch-level approval authority for corporate loans" },
        { HOReviewer, "Head office review for corporate loans" },
        { CommitteeMember, "Committee voting and comments for corporate loans" },
        { FinalApprover, "Final loan approval authority" },
        { Operations, "Disbursement and booking operations" },
        { Auditor, "Read-only audit access" },
        { Customer, "Self-service retail loan applicant" }
    };
}
