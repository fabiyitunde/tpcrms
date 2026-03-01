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
            "CommitteeCirculation" => "Committee",
            "CommitteeApproved" => "Committee Approved",
            "CommitteeRejected" => "Committee Rejected",
            "FinalApproval" => "Final Approval",
            "Approved" => "Approved",
            "Rejected" => "Rejected",
            "OfferGenerated" => "Offer Generated",
            "OfferAccepted" => "Offer Accepted",
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
}

public class CollateralInfo
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal MarketValue { get; set; }
    public decimal ForcedSaleValue { get; set; }
    public decimal LoanToValue { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? LastValuationDate { get; set; }
}

public class GuarantorInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public decimal GuaranteeAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasBureauReport { get; set; }
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
    public int ActiveLoans { get; set; }
    public decimal TotalExposure { get; set; }
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
    public string? Decision { get; set; }
    public string? DecisionComments { get; set; }
    public DateTime? DecisionDate { get; set; }
}

public class CommitteeMemberVote
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
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

    public static ApiResponse Ok()
    {
        return new ApiResponse
        {
            Success = true
        };
    }

    public static ApiResponse Fail(string error)
    {
        return new ApiResponse
        {
            Success = false,
            Error = error
        };
    }
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
}
