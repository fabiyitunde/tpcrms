using CRMS.Domain.Enums;

namespace CRMS.Domain.Constants;

public static class Roles
{
    // ── Corporate loan roles ─────────────────────────────────────
    public const string SystemAdmin = "SystemAdmin";
    public const string LoanOfficer = "LoanOfficer";
    public const string CreditOfficer = "CreditOfficer";
    public const string RiskManager = "RiskManager";
    public const string BranchApprover = "BranchApprover";
    public const string HOReviewer = "HOReviewer";
    public const string CommitteeMember = "CommitteeMember";
    public const string FinalApprover = "FinalApprover";
    public const string DisbursementOfficer = "DisbursementOfficer";
    public const string GMFinance = "GMFinance";
    public const string LegalOfficer = "LegalOfficer";
    public const string HeadOfLegal = "HeadOfLegal";
    public const string Auditor = "Auditor";
    public const string Customer = "Customer";

    // ── NAMP-specific roles ──────────────────────────────────────
    public const string BranchManager = "BranchManager";                     // Ratifies Branch committee decision
    public const string ZonalManager = "ZonalManager";                       // Ratifies Zonal committee decision
    public const string RegionalManager = "RegionalManager";                 // Ratifies Regional committee decision
    public const string MdCeo = "MdCeo";                                     // Ratifies HO committee decision (MD/CEO)
    public const string BranchCommitteeMember = "BranchCommitteeMember";     // Votes in Branch Credit Committee
    public const string ZonalCommitteeMember = "ZonalCommitteeMember";       // Votes in Zonal Credit Committee
    public const string RegionalCommitteeMember = "RegionalCommitteeMember"; // Votes in Regional Credit Committee
    public const string HOCommitteeMember = "HOCommitteeMember";             // Votes in HO Credit Committee
    public const string DeploymentOfficer = "DeploymentOfficer";             // Pre-deployment verification and equipment deployment tracking

    // ── NAMP Risk Officers (tier-scoped) ─────────────────────────────────
    public const string BranchRiskOfficer   = "BranchRiskOfficer";           // Reviews Branch-tier NAMP applications before committee
    public const string ZonalRiskOfficer    = "ZonalRiskOfficer";            // Reviews Zonal-tier NAMP applications before committee
    public const string RegionalRiskOfficer = "RegionalRiskOfficer";         // Reviews Regional-tier NAMP applications before committee
    public const string HORiskOfficer       = "HORiskOfficer";               // Reviews HO-tier NAMP applications before committee

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
        DisbursementOfficer,
        GMFinance,
        LegalOfficer,
        HeadOfLegal,
        Auditor,
        Customer,
        // NAMP
        BranchManager,
        ZonalManager,
        RegionalManager,
        MdCeo,
        BranchCommitteeMember,
        ZonalCommitteeMember,
        RegionalCommitteeMember,
        HOCommitteeMember,
        DeploymentOfficer,
        BranchRiskOfficer,
        ZonalRiskOfficer,
        RegionalRiskOfficer,
        HORiskOfficer,
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
        { DisbursementOfficer, "Prepares disbursement memo and books loan in core banking (Corporate and Retail)" },
        { GMFinance, "GM Finance / HQ Treasury — final funds-release authoriser" },
        { LegalOfficer, "Prepares legal opinion and security documents" },
        { HeadOfLegal, "Countersigns legal opinion before committee circulation" },
        { Auditor, "Read-only audit access" },
        { Customer, "Self-service retail loan applicant" },
        // Tier managers — ratify both NAMP and Corporate loan committee decisions
        { BranchManager, "Ratifies Branch Credit Committee decisions (NAMP and Corporate)" },
        { ZonalManager, "Ratifies Zonal Credit Committee decisions (NAMP and Corporate)" },
        { RegionalManager, "Ratifies Regional Credit Committee decisions (NAMP and Corporate)" },
        { MdCeo, "Ratifies HO Credit Committee decisions — MD/CEO (NAMP and Corporate)" },
        { BranchCommitteeMember, "Votes in Branch Credit Committee" },
        { ZonalCommitteeMember, "Votes in Zonal Credit Committee" },
        { RegionalCommitteeMember, "Votes in Regional Credit Committee" },
        { HOCommitteeMember, "Votes in HO Credit Committee" },
        { DeploymentOfficer, "Pre-deployment verification and equipment/funds disbursement (NAMP and Corporate)" },
        { BranchRiskOfficer,   "Reviews Branch-tier NAMP applications before committee circulation" },
        { ZonalRiskOfficer,    "Reviews Zonal-tier NAMP applications before committee circulation" },
        { RegionalRiskOfficer, "Reviews Regional-tier NAMP applications before committee circulation" },
        { HORiskOfficer,       "Reviews HO-tier NAMP applications before committee circulation" },
    };

    /// <summary>
    /// Defines what visibility scope each role has for viewing loan applications.
    /// Branch-level roles can only see applications in their branch.
    /// HO roles can see all applications globally.
    /// </summary>
    public static readonly Dictionary<string, VisibilityScope> RoleVisibilityScopes = new()
    {
        { SystemAdmin, VisibilityScope.Global },
        { LoanOfficer, VisibilityScope.Branch },
        { CreditOfficer, VisibilityScope.Branch },
        { RiskManager, VisibilityScope.Global },
        { BranchApprover, VisibilityScope.Branch },
        { HOReviewer, VisibilityScope.Global },
        { CommitteeMember, VisibilityScope.Global },
        { FinalApprover, VisibilityScope.Global },
        { DisbursementOfficer, VisibilityScope.Global },
        { GMFinance, VisibilityScope.Global },
        { LegalOfficer, VisibilityScope.Global },
        { HeadOfLegal, VisibilityScope.Global },
        { Auditor, VisibilityScope.Global },
        { Customer, VisibilityScope.Own },
        // NAMP
        { BranchManager, VisibilityScope.Branch },
        { ZonalManager, VisibilityScope.Global },
        { RegionalManager, VisibilityScope.Global },
        { MdCeo, VisibilityScope.Global },
        { BranchCommitteeMember, VisibilityScope.Branch },
        { ZonalCommitteeMember, VisibilityScope.Global },
        { RegionalCommitteeMember, VisibilityScope.Global },
        { HOCommitteeMember, VisibilityScope.Global },
        { DeploymentOfficer,    VisibilityScope.Global },
        { BranchRiskOfficer,    VisibilityScope.Branch },
        { ZonalRiskOfficer,     VisibilityScope.Global },
        { RegionalRiskOfficer,  VisibilityScope.Global },
        { HORiskOfficer,        VisibilityScope.Global },
    };

    /// <summary>
    /// The role responsible for ratifying a committee decision at each NAMP tier:
    /// Branch Manager ratifies the Branch committee, Zonal Manager the Zonal committee, etc.
    /// Falls back to the generic FinalApprover if the tier is unrecognised.
    /// </summary>
    public static string RiskOfficerRoleForTier(NampCommitteeTier tier) => tier switch
    {
        NampCommitteeTier.Branch     => BranchRiskOfficer,
        NampCommitteeTier.Zonal      => ZonalRiskOfficer,
        NampCommitteeTier.Regional   => RegionalRiskOfficer,
        NampCommitteeTier.HeadOffice => HORiskOfficer,
        _                            => HORiskOfficer,
    };

    public static string RatifierRoleForTier(NampCommitteeTier tier) => tier switch
    {
        NampCommitteeTier.Branch     => BranchManager,
        NampCommitteeTier.Zonal      => ZonalManager,
        NampCommitteeTier.Regional   => RegionalManager,
        NampCommitteeTier.HeadOffice => MdCeo,
        _                            => FinalApprover,
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
