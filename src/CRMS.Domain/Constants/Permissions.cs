namespace CRMS.Domain.Constants;

public static class Permissions
{
    public static class Products
    {
        public const string View = "products.view";
        public const string Create = "products.create";
        public const string Edit = "products.edit";
        public const string Delete = "products.delete";
    }

    public static class CorporateLoan
    {
        public const string Initiate = "corporateloan.initiate";
        public const string View = "corporateloan.view";
        public const string Edit = "corporateloan.edit";
        public const string BranchApprove = "corporateloan.branchapprove";
        public const string HOReview = "corporateloan.horeview";
        public const string CommitteeVote = "corporateloan.committeevote";
        public const string FinalApprove = "corporateloan.finalapprove";
    }

    public static class RetailLoan
    {
        public const string View = "retailloan.view";
        public const string Decide = "retailloan.decide";
        public const string Override = "retailloan.override";
    }

    public static class Bureau
    {
        public const string View = "bureau.view";
        public const string Request = "bureau.request";
    }

    public static class Disbursement
    {
        public const string Execute = "disbursement.execute";
    }

    public static class Reports
    {
        public const string View = "reports.view";
        public const string Export = "reports.export";
    }

    public static class Audit
    {
        public const string View = "audit.view";
    }

    public static class Users
    {
        public const string View = "users.view";
        public const string Manage = "users.manage";
        public const string Delete = "users.delete";
    }

    public static class Roles
    {
        public const string Manage = "roles.manage";
    }

    public static readonly (string Code, string Name, string Module)[] AllPermissions =
    [
        (Products.View, "View loan products", "Products"),
        (Products.Create, "Create loan products", "Products"),
        (Products.Edit, "Edit loan products", "Products"),
        (Products.Delete, "Delete loan products", "Products"),
        
        (CorporateLoan.Initiate, "Initiate corporate loan", "CorporateLoan"),
        (CorporateLoan.View, "View corporate loans", "CorporateLoan"),
        (CorporateLoan.Edit, "Edit loan applications", "CorporateLoan"),
        (CorporateLoan.BranchApprove, "Branch level approval", "CorporateLoan"),
        (CorporateLoan.HOReview, "Head office review", "CorporateLoan"),
        (CorporateLoan.CommitteeVote, "Committee voting", "CorporateLoan"),
        (CorporateLoan.FinalApprove, "Final approval", "CorporateLoan"),
        
        (RetailLoan.View, "View retail loans", "RetailLoan"),
        (RetailLoan.Decide, "Make credit decisions", "RetailLoan"),
        (RetailLoan.Override, "Override decisions", "RetailLoan"),
        
        (Bureau.View, "View bureau reports", "Bureau"),
        (Bureau.Request, "Request bureau checks", "Bureau"),
        
        (Disbursement.Execute, "Execute disbursements", "Disbursement"),
        
        (Reports.View, "View reports", "Reports"),
        (Reports.Export, "Export reports", "Reports"),
        
        (Audit.View, "View audit logs", "Audit"),
        
        (Users.View, "View users", "Users"),
        (Users.Manage, "Create/edit users", "Users"),
        (Users.Delete, "Delete users", "Users"),
        
        (Roles.Manage, "Manage roles and permissions", "Roles")
    ];
}
