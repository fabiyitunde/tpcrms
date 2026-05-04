using CRMS.Domain.Enums;

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
    public const string GMFinance = "GMFinance";
    public const string LegalOfficer = "LegalOfficer";
    public const string HeadOfLegal = "HeadOfLegal";
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
        GMFinance,
        LegalOfficer,
        HeadOfLegal,
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
        { Operations, "Prepares disbursement memo and books loan in core banking" },
        { GMFinance, "GM Finance / HQ Treasury — final funds-release authoriser" },
        { LegalOfficer, "Prepares legal opinion and security documents" },
        { HeadOfLegal, "Countersigns legal opinion before committee circulation" },
        { Auditor, "Read-only audit access" },
        { Customer, "Self-service retail loan applicant" }
    };

    /// <summary>
    /// Defines what visibility scope each role has for viewing loan applications.
    /// Branch-level roles can only see applications in their branch.
    /// HO roles can see all applications globally.
    /// </summary>
    public static readonly Dictionary<string, VisibilityScope> RoleVisibilityScopes = new()
    {
        { SystemAdmin, VisibilityScope.Global },      // Superuser - sees all
        { LoanOfficer, VisibilityScope.Branch },      // Sees applications in their branch
        { CreditOfficer, VisibilityScope.Global },    // HO role - sees all
        { RiskManager, VisibilityScope.Global },      // HO role - sees all
        { BranchApprover, VisibilityScope.Branch },   // Approves applications in their branch
        { HOReviewer, VisibilityScope.Global },       // HO role - sees all
        { CommitteeMember, VisibilityScope.Global },  // Committee can see all (for voting)
        { FinalApprover, VisibilityScope.Global },    // HO role - sees all
        { Operations, VisibilityScope.Global },       // HO role - sees all
        { GMFinance, VisibilityScope.Global },        // HQ role - sees all
        { LegalOfficer, VisibilityScope.Global },     // HO role - sees all
        { HeadOfLegal, VisibilityScope.Global },      // HO role - sees all
        { Auditor, VisibilityScope.Global },          // Read-only audit - sees all
        { Customer, VisibilityScope.Own }             // Customers see only their own applications
    };

    /// <summary>
    /// Gets the visibility scope for a role. Defaults to Branch if role not found.
    /// </summary>
    public static VisibilityScope GetVisibilityScope(string role)
    {
        return RoleVisibilityScopes.GetValueOrDefault(role, VisibilityScope.Branch);
    }

    /// <summary>
    /// Checks if a role has global visibility (can see all applications).
    /// </summary>
    public static bool HasGlobalVisibility(string role)
    {
        return GetVisibilityScope(role) == VisibilityScope.Global;
    }
}
