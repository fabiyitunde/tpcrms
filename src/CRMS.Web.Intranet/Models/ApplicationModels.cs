using Microsoft.AspNetCore.Components.Forms;

namespace CRMS.Web.Intranet.Models;

public class LoanApplicationSummary
{
    public Guid Id { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal RequestedAmount { get; set; }
    public int TenorMonths { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusDisplay => FormatStatus(Status);
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string? BranchName { get; set; }

    private static string FormatStatus(string status)
    {
        return status switch
        {
            "Draft" => "Draft",
            "Submitted" => "Submitted",
            "DataGathering" => "Data Gathering",
            "BranchReview" => "Branch Review",
            "BranchApproved" => "Branch Approved",
            "BranchReturned" => "Returned",
            "BranchRejected" => "Branch Rejected",
            "CreditAnalysis" => "Credit Analysis",
            "HOReview" => "HO Review",
            "LegalReview" => "Legal Review",
            "LegalApproval" => "Legal Approval",
            "CommitteeCirculation" => "Committee",
            "CommitteeApproved" => "Committee Approved",
            "CommitteeRejected" => "Committee Rejected",
            "FinalApproval" => "Final Approval",
            "Approved" => "Approved",
            "Rejected" => "Rejected",
            "OfferGenerated" => "Offer Generated",
            "OfferAccepted" => "Offer Accepted",
            "SecurityPerfection" => "Security Perfection",
            "SecurityApproval" => "Security Approval",
            "DisbursementPending" => "Disbursement Pending",
            "DisbursementBranchApproval" => "Disbursement — Branch Auth",
            "DisbursementHQApproval" => "Disbursement — HQ Auth",
            "Disbursed" => "Disbursed",
            "Closed" => "Closed",
            "Cancelled" => "Cancelled",
            _ => status
        };
    }
}

public class LoanApplicationDetail
{
    public Guid Id { get; set; }

    public string ApplicationNumber { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public CustomerInfo Customer { get; set; } = new CustomerInfo();

    public LoanInfo Loan { get; set; } = new LoanInfo();

    public List<PartyInfo> Directors { get; set; } = new List<PartyInfo>();

    public List<PartyInfo> Signatories { get; set; } = new List<PartyInfo>();

    public List<DocumentInfo> Documents { get; set; } = new List<DocumentInfo>();

    public List<CollateralInfo> Collaterals { get; set; } = new List<CollateralInfo>();

    public List<GuarantorInfo> Guarantors { get; set; } = new List<GuarantorInfo>();

    public FinancialAnalysisInfo? FinancialAnalysis { get; set; }

    public List<FinancialStatementInfo> FinancialStatements { get; set; } = new List<FinancialStatementInfo>();

    public List<BureauReportInfo> BureauReports { get; set; } = new List<BureauReportInfo>();

    public List<BankStatementInfo> BankStatements { get; set; } = new List<BankStatementInfo>();

    public AdvisoryInfo? Advisory { get; set; }

    public List<WorkflowHistoryItem> WorkflowHistory { get; set; } = new List<WorkflowHistoryItem>();

    public CommitteeInfo? Committee { get; set; }

    public List<CommentInfo> Comments { get; set; } = new List<CommentInfo>();

    public DateTime CreatedAt { get; set; }

    public DateTime? LastUpdatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
}

public class CustomerInfo
{
    public string AccountNumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public DateTime? IncorporationDate { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    // Signatories from core banking (shown in New.razor for BVN entry)
    public List<SignatoryInput> Signatories { get; set; } = new();
    // Directors from core banking (used for discrepancy comparison against SmartComply CAC)
    public List<CbsDirectorInfo> CbsDirectors { get; set; } = new();
}

// Director info from core banking (CBS) — used only for discrepancy comparison
public class CbsDirectorInfo
{
    public string FullName { get; set; } = string.Empty;
    public string? BVN { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Address { get; set; }
}

public class LoanInfo
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal RequestedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public decimal InterestRate { get; set; }
    public string InterestRateType { get; set; } = string.Empty;
    public int TenorMonths { get; set; }
    public string Purpose { get; set; } = string.Empty;
}

public class PartyInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? RawBVN { get; set; }
    public string BvnMasked { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string PartyType { get; set; } = string.Empty;
    public decimal? ShareholdingPercentage { get; set; }
    public string? MandateType { get; set; }
    public bool HasBureauReport { get; set; }
    public string? BureauStatus { get; set; }
    public Guid? BureauReportId { get; set; }
}

public class DocumentInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? RejectionReason { get; set; }
}

public class CollateralInfo
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? IndicativeValue { get; set; }
    public decimal MarketValue { get; set; }
    public decimal ForcedSaleValue { get; set; }
    public decimal LoanToValue { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? LastValuationDate { get; set; }
    public string? RejectionReason { get; set; }
    public bool IsLegalCleared { get; set; }
    public DateTime? LegalClearedAt { get; set; }
    public string? LegalClearanceNotes { get; set; }
}

public class GuarantorInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public decimal GuaranteeAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasBureauReport { get; set; }
    public string? RejectionReason { get; set; }
}

public class FinancialAnalysisInfo
{
    public List<FinancialRatio> Ratios { get; set; } = [];
    public List<YearlyFinancials> YearlyData { get; set; } = [];
}

public class FinancialRatio
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? Benchmark { get; set; }
}

public class YearlyFinancials
{
    public int Year { get; set; }
    public decimal Revenue { get; set; }
    public decimal NetIncome { get; set; }
    public decimal TotalAssets { get; set; }
    public decimal TotalLiabilities { get; set; }
}

public class BureauReportInfo
{
    public Guid Id { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string SubjectType { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? CreditScore { get; set; }
    public string Rating { get; set; } = string.Empty;
    public bool IsScoreDerived { get; set; }
    public int TotalLoans { get; set; }
    public int ActiveLoans { get; set; }
    public int PerformingLoans { get; set; }
    public int ClosedLoans { get; set; }
    public decimal TotalExposure { get; set; }
    public decimal HighestFacility { get; set; }
    public decimal TotalOverdue { get; set; }
    public int MaxDelinquencyDays { get; set; }
    public bool HasLegalIssues { get; set; }
    public DateTime ReportDate { get; set; }
    // Fraud check results (SmartComply)
    public int? FraudRiskScore { get; set; }
    public string? FraudRecommendation { get; set; }
    // Party linkage
    public Guid? PartyId { get; set; }
    public string? PartyType { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AdvisoryInfo
{
    public decimal OverallScore { get; set; }
    public string RiskRating { get; set; } = string.Empty;
    public List<ScoreCategory> ScoreBreakdown { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
    public List<string> RedFlags { get; set; } = [];
    public List<string> Strengths { get; set; } = [];
    public DateTime GeneratedAt { get; set; }
}

public class ScoreCategory
{
    public string Category { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public decimal MaxScore { get; set; }
    public decimal Weight { get; set; }
}

public class WorkflowHistoryItem
{
    public string FromStage { get; set; } = string.Empty;
    public string ToStage { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Comments { get; set; }
}

public class CommitteeInfo
{
    public Guid ReviewId { get; set; }
    public string CommitteeType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<CommitteeMemberVote> Members { get; set; } = [];
    // Recommended terms (captured before voting starts)
    public decimal? RecommendedAmount { get; set; }
    public int? RecommendedTenorMonths { get; set; }
    public decimal? RecommendedInterestRate { get; set; }
    public string? RecommendedConditions { get; set; }
    // Vote tally
    public int ApprovalVotes { get; set; }
    public int RejectionVotes { get; set; }
    public int AbstainVotes { get; set; }
    public int PendingVotes { get; set; }
    public bool HasQuorum { get; set; }
    public bool HasMajorityApproval { get; set; }
    public bool IsOverdue { get; set; }
    // Decision
    public string? Decision { get; set; }
    public string? DecisionComments { get; set; }
    public DateTime? DecisionDate { get; set; }
}

public record CommitteeDecisionArgs(
    string Decision,
    string Rationale,
    decimal? Amount,
    int? TenorMonths,
    decimal? InterestRate,
    string? Conditions
);

public class CommitteeMemberVote
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsChairperson { get; set; }
    public string? Vote { get; set; }
    public string? Comments { get; set; }
    public DateTime? VotedAt { get; set; }
}

public class CommentInfo
{
    public Guid Id { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsInternal { get; set; }
}

public class StandingCommitteeInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CommitteeType { get; set; } = string.Empty;
    public int RequiredVotes { get; set; }
    public int MinimumApprovalVotes { get; set; }
    public int DefaultDeadlineHours { get; set; }
    public decimal MinAmountThreshold { get; set; }
    public decimal? MaxAmountThreshold { get; set; }
    public bool IsActive { get; set; }
    public List<StandingMemberInfo> Members { get; set; } = [];
}

public class StandingMemberInfo
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsChairperson { get; set; }
}

public class CreateApplicationRequest
{
    public string AccountNumber { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public decimal RequestedAmount { get; set; }
    public int TenorMonths { get; set; }
    public decimal InterestRate { get; set; }
    public string InterestRateType { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string? RegistrationNumberOverride { get; set; }
    public DateTime? IncorporationDateOverride { get; set; }
    public string? IndustrySector { get; set; }
    // Directors from SmartComply CAC (with user-entered BVNs)
    public List<DirectorInput> Directors { get; set; } = new();
    // Signatories from core banking (with user-entered BVNs where missing)
    public List<SignatoryInput> Signatories { get; set; } = new();
}

// Director data from SmartComply CAC Advanced + user-entered BVN
public class DirectorInput
{
    public string FullName { get; set; } = string.Empty;
    public string? Surname { get; set; }
    public string? FirstName { get; set; }
    public string? OtherName { get; set; }
    public string? BVN { get; set; }             // entered by data-entry user
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public decimal? ShareholdingPercent { get; set; }
    public long? NumSharesAlloted { get; set; }
    public string? TypeOfShares { get; set; }
    public bool? IsChairman { get; set; }
    public string? AffiliateType { get; set; }  // designation from CAC
    public string? DateOfAppointment { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Occupation { get; set; }
    public string? Nationality { get; set; }
    public string? Address { get; set; }
}

// Signatory from core banking + user-entered BVN (when missing)
public class SignatoryInput
{
    public string SignatoryId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? BVN { get; set; }             // from core banking or entered by data-entry user
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Designation { get; set; }
    public string? MandateType { get; set; }
}

// Result of SmartComply CAC Advanced lookup used in New.razor
public class CacLookupResult
{
    public string? CompanyName { get; set; }
    public string? RcNumber { get; set; }
    public string? Status { get; set; }
    public string? RegistrationDate { get; set; }
    public string? EntityType { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public long? CompanyId { get; set; }
    public List<CacDirectorEntry> Directors { get; set; } = new();
}

// One director row in the New Application form
public class CacDirectorEntry
{
    public long? Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Surname { get; set; }
    public string? FirstName { get; set; }
    public string? OtherName { get; set; }
    public string? Gender { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Occupation { get; set; }
    public string? Nationality { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public bool? IsChairman { get; set; }
    public string? AffiliateType { get; set; }
    public string? TypeOfShares { get; set; }
    public long? NumSharesAlloted { get; set; }
    public string? DateOfAppointment { get; set; }
    // Entered by data-entry user:
    public string BvnInput { get; set; } = string.Empty;
}

public class BankStatementInfo
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int MonthsCovered { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public string Source { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public decimal TrustWeight { get; set; }
    public string AnalysisStatus { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public string? OriginalFileName { get; set; }
    public decimal? TotalCredits { get; set; }
    public decimal? TotalDebits { get; set; }
    public decimal? AverageMonthlyBalance { get; set; }
    public decimal? NetMonthlyCashflow { get; set; }
    public int? BouncedTransactions { get; set; }
    public int? GamblingTransactions { get; set; }
    public string? VerificationNotes { get; set; }
}

public class StatementTransactionInfo
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;   // "Credit" | "Debit"
    public decimal RunningBalance { get; set; }
    public string? Reference { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal CategoryConfidence { get; set; }
    public bool IsRecurring { get; set; }
}

public class UploadExternalStatementRequest
{
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public DateTime PeriodFrom { get; set; } = DateTime.Today.AddMonths(-6);
    public DateTime PeriodTo { get; set; } = DateTime.Today;
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public IBrowserFile? File { get; set; }
}

public class StatementTransactionRow
{
    public DateTime Date { get; set; } = DateTime.Today;
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public decimal? DebitAmount { get; set; }
    public decimal? CreditAmount { get; set; }
    public decimal RunningBalance { get; set; }
}

public class StatementUploadResult
{
    public Guid StatementId { get; set; }
    public List<StatementTransactionRow> ParsedTransactions { get; set; } = [];
    public string? ParseMessage { get; set; }
}

public class StatementParseResult
{
    public List<StatementTransactionRow> Transactions { get; set; } = [];
    public int SkippedRows { get; set; }
    public string DetectedFormat { get; set; } = string.Empty;
    public string? Error { get; set; }
    public bool Success => Error == null;
}

public class ApplicationFilter
{
    public string? Status { get; set; }
    public string? SearchTerm { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public Guid? ProductId { get; set; }
    public decimal? AmountMin { get; set; }
    public decimal? AmountMax { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
public class ApiResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    /// <summary>Non-fatal informational message (e.g. partial success warning).</summary>
    public string? Message { get; set; }

    public static ApiResponse Ok() => new ApiResponse { Success = true };
    public static ApiResponse Ok(string message) => new ApiResponse { Success = true, Message = message };

    public static ApiResponse Fail(string error) => new ApiResponse { Success = false, Error = error };
}
public class ApiResponse<T> : ApiResponse
{
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data
        };
    }

    public new static ApiResponse<T> Fail(string error)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Error = error
        };
    }
}
public class LoanProduct
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public decimal MinAmount { get; set; }

    public decimal MaxAmount { get; set; }

    public int MinTenorMonths { get; set; }

    public int MaxTenorMonths { get; set; }

    public decimal BaseInterestRate { get; set; }

    public bool IsActive { get; set; }

    public int? FineractProductId { get; set; }
}

public class FinancialStatementInfo
{
    public Guid Id { get; set; }

    public int Year { get; set; }

    public DateTime YearEndDate { get; set; }

    public string YearType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public decimal TotalAssets { get; set; }

    public decimal TotalLiabilities { get; set; }

    public decimal TotalEquity { get; set; }

    public decimal TotalDebt { get; set; }

    public decimal Revenue { get; set; }

    public decimal NetProfit { get; set; }

    public string? RejectionReason { get; set; }
}

public class LocationInfo
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid? ParentLocationId { get; set; }
    public string? ParentLocationName { get; set; }
    public bool IsActive { get; set; }
    public string? Address { get; set; }
    public string? ManagerName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public int SortOrder { get; set; }

    public string DisplayName => $"{Code} - {Name}";
    public string TypeBadgeClass => Type switch
    {
        "HeadOffice" => "badge-primary",
        "Region" => "badge-info",
        "Zone" => "badge-warning",
        "Branch" => "badge-success",
        _ => "badge-secondary"
    };
}

public class LocationTreeNode
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? ManagerName { get; set; }
    public int SortOrder { get; set; }
    public List<LocationTreeNode> Children { get; set; } = [];

    public bool IsExpanded { get; set; } = true;
    public string TypeIcon => Type switch
    {
        "HeadOffice" => "🏛️",
        "Region" => "🗺️",
        "Zone" => "📍",
        "Branch" => "🏢",
        _ => "📁"
    };
}

// Notification Template Models
public class NotificationTemplateInfo
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public string? Subject { get; set; }
    public string BodyTemplate { get; set; } = string.Empty;
    public string? BodyHtmlTemplate { get; set; }
    public bool IsActive { get; set; }
    public int Version { get; set; }
}

public class CreateTemplateRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string BodyTemplate { get; set; } = string.Empty;
    public string? BodyHtmlTemplate { get; set; }
}

public class UpdateTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Subject { get; set; }
    public string BodyTemplate { get; set; } = string.Empty;
    public string? BodyHtmlTemplate { get; set; }
}

// Offer Letter history
public class OfferLetterInfo
{
    public Guid Id { get; set; }
    public int Version { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Status { get; set; } = string.Empty;
    public string GeneratedByUserName { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public string StoragePath { get; set; } = string.Empty;
}

// ---------------------------------------------------------------------------
// Disbursement Checklist Models (OfferGenerated / OfferAccepted stage)
// ---------------------------------------------------------------------------

public class DisbursementChecklistModel
{
    public Guid LoanApplicationId { get; set; }
    public bool AllPrecedentResolved { get; set; }
    public List<ChecklistItemModel> Items { get; set; } = [];

    public IEnumerable<ChecklistItemModel> PrecedentItems =>
        Items.Where(i => i.ConditionType == "Precedent").OrderBy(i => i.SortOrder);

    public IEnumerable<ChecklistItemModel> SubsequentItems =>
        Items.Where(i => i.ConditionType == "Subsequent").OrderBy(i => i.SortOrder);
}

public class ChecklistItemModel
{
    public Guid Id { get; set; }
    public Guid TemplateItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public string ConditionType { get; set; } = string.Empty;
    public int? SubsequentDueDays { get; set; }
    public bool RequiresDocumentUpload { get; set; }
    public bool RequiresLegalRatification { get; set; }
    public bool CanBeWaived { get; set; }
    public int SortOrder { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsResolved { get; set; }
    public bool BlocksDisbursement { get; set; }

    // Satisfaction
    public Guid? SatisfiedByUserId { get; set; }
    public DateTime? SatisfiedAt { get; set; }
    public Guid? EvidenceDocumentId { get; set; }

    // Legal
    public Guid? LegalRatifiedByUserId { get; set; }
    public DateTime? LegalRatifiedAt { get; set; }
    public string? LegalReturnReason { get; set; }

    // Waiver
    public Guid? WaiverProposedByUserId { get; set; }
    public string? WaiverProposedByUserName { get; set; }
    public DateTime? WaiverProposedAt { get; set; }
    public string? WaiverReason { get; set; }
    public Guid? WaiverRatifiedByUserId { get; set; }
    public DateTime? WaiverRatifiedAt { get; set; }
    public string? WaiverRejectionReason { get; set; }

    // CS due date
    public DateTime? DueDate { get; set; }
    public DateTime? OriginalDueDate { get; set; }
    public string? ExtensionReason { get; set; }

    public string StatusBadgeClass => Status switch
    {
        "Satisfied" => "badge bg-success",
        "Waived" => "badge bg-warning text-dark",
        "PendingLegalReview" => "badge bg-info text-dark",
        "LegalReturned" => "badge bg-danger",
        "WaiverPending" => "badge bg-warning text-dark",
        "Overdue" => "badge bg-danger",
        "ExtensionPending" => "badge bg-secondary",
        _ => "badge bg-light text-dark"
    };

    public string StatusDisplay => Status switch
    {
        "Pending" => "Pending",
        "PendingLegalReview" => "Legal Review",
        "LegalReturned" => "Returned by Legal",
        "Satisfied" => "Satisfied",
        "WaiverPending" => "Waiver Pending",
        "Waived" => "Waived",
        "Overdue" => "Overdue",
        "ExtensionPending" => "Extension Pending",
        _ => Status
    };
}

public class ChecklistTemplateItemModel
{
    public Guid Id { get; set; }
    public Guid LoanProductId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public string ConditionType { get; set; } = "Precedent";
    public int? SubsequentDueDays { get; set; }
    public bool RequiresDocumentUpload { get; set; }
    public bool RequiresLegalRatification { get; set; }
    public bool CanBeWaived { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

// Bureau Account Info for detail modal
public class BureauAccountInfo
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string? CreditorName { get; set; }
    public string? AccountType { get; set; }
    public string Status { get; set; } = string.Empty;
    public string DelinquencyLevel { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
    public decimal Balance { get; set; }
    public DateTime? DateOpened { get; set; }
    public DateTime? LastPaymentDate { get; set; }
}

public class ApprovalGateResultModel
{
    public bool IsStrict { get; set; }
    public bool HasIssues => RejectedItems.Count > 0 || PendingItems.Count > 0;
    public bool IsHardBlock => IsStrict && HasIssues;
    public bool RequiresOverrideNote => !IsStrict && RejectedItems.Count > 0;
    public List<GateItemModel> RejectedItems { get; set; } = [];
    public List<GateItemModel> PendingItems { get; set; } = [];
}

public class GateItemModel
{
    public Guid ItemId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string ItemLabel { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
}

public class ApprovalOverrideInfo
{
    public Guid Id { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string NoteText { get; set; } = string.Empty;
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}
