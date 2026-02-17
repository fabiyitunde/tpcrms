namespace CRMS.Application.Reporting.DTOs;

// Dashboard Summary DTOs
public record DashboardSummaryDto(
    LoanFunnelDto LoanFunnel,
    PortfolioSummaryDto Portfolio,
    PerformanceMetricsDto Performance,
    PendingActionsDto PendingActions
);

public record LoanFunnelDto(
    int Submitted,
    int InReview,
    int Approved,
    int Rejected,
    int Disbursed,
    decimal SubmittedAmount,
    decimal ApprovedAmount,
    decimal DisbursedAmount,
    decimal ApprovalRate,
    decimal ConversionRate
);

public record PortfolioSummaryDto(
    int TotalActiveLoans,
    decimal TotalOutstanding,
    decimal AverageTicketSize,
    int CorporateLoans,
    int RetailLoans,
    decimal CorporateOutstanding,
    decimal RetailOutstanding,
    Dictionary<string, int> LoansByProduct,
    Dictionary<string, decimal> OutstandingByProduct
);

public record PerformanceMetricsDto(
    decimal AverageProcessingTimeDays,
    decimal AverageApprovalTimeDays,
    decimal SLAComplianceRate,
    int TotalApplicationsThisMonth,
    int TotalApplicationsLastMonth,
    decimal MonthOverMonthGrowth,
    Dictionary<string, decimal> ProcessingTimeByStage
);

public record PendingActionsDto(
    int PendingApplications,
    int OverdueSLAs,
    int PendingCommitteeVotes,
    int PendingDocuments,
    int AwaitingDisbursement
);

// Loan Funnel Report
public record LoanFunnelReportDto(
    DateTime FromDate,
    DateTime ToDate,
    List<FunnelStageDto> Stages,
    List<FunnelTrendDto> DailyTrend
);

public record FunnelStageDto(
    string Stage,
    int Count,
    decimal Amount,
    decimal PercentageOfTotal,
    decimal ConversionFromPrevious
);

public record FunnelTrendDto(
    DateTime Date,
    int Submitted,
    int Approved,
    int Rejected,
    int Disbursed
);

// Portfolio Report
public record PortfolioReportDto(
    DateTime AsOfDate,
    PortfolioSummaryDto Summary,
    List<PortfolioByProductDto> ByProduct,
    List<PortfolioByBranchDto> ByBranch,
    List<PortfolioByRiskRatingDto> ByRiskRating,
    List<PortfolioAgingDto> Aging
);

public record PortfolioByProductDto(
    string ProductName,
    int LoanCount,
    decimal Outstanding,
    decimal Percentage
);

public record PortfolioByBranchDto(
    string BranchName,
    int LoanCount,
    decimal Outstanding,
    decimal Percentage
);

public record PortfolioByRiskRatingDto(
    string RiskRating,
    int LoanCount,
    decimal Outstanding,
    decimal Percentage
);

public record PortfolioAgingDto(
    string Bucket,
    int LoanCount,
    decimal Outstanding,
    decimal Percentage
);

// Performance Report
public record PerformanceReportDto(
    DateTime FromDate,
    DateTime ToDate,
    PerformanceMetricsDto Overall,
    List<PerformanceByUserDto> ByUser,
    List<PerformanceByStageDto> ByStage,
    List<PerformanceTrendDto> Trend
);

public record PerformanceByUserDto(
    Guid UserId,
    string UserName,
    string Role,
    int ApplicationsProcessed,
    decimal AverageProcessingTime,
    decimal SLACompliance
);

public record PerformanceByStageDto(
    string Stage,
    int Count,
    decimal AverageTime,
    decimal SLACompliance
);

public record PerformanceTrendDto(
    DateTime Date,
    int Processed,
    decimal AverageTime,
    decimal SLACompliance
);

// Decision Distribution Report
public record DecisionDistributionDto(
    DateTime FromDate,
    DateTime ToDate,
    int TotalDecisions,
    int Approved,
    int Rejected,
    int Referred,
    decimal ApprovalRate,
    List<DecisionByProductDto> ByProduct,
    List<DecisionByRiskScoreDto> ByRiskScore,
    List<TopRejectionReasonDto> TopRejectionReasons
);

public record DecisionByProductDto(
    string ProductName,
    int Approved,
    int Rejected,
    decimal ApprovalRate
);

public record DecisionByRiskScoreDto(
    string ScoreRange,
    int Count,
    int Approved,
    int Rejected,
    decimal ApprovalRate
);

public record TopRejectionReasonDto(
    string Reason,
    int Count,
    decimal Percentage
);

// SLA Report
public record SLAReportDto(
    DateTime FromDate,
    DateTime ToDate,
    decimal OverallCompliance,
    int TotalCases,
    int OnTime,
    int Breached,
    List<SLAByStageDto> ByStage,
    List<SLAByUserDto> ByUser,
    List<SLATrendDto> Trend
);

public record SLAByStageDto(
    string Stage,
    int TotalCases,
    int OnTime,
    int Breached,
    decimal ComplianceRate,
    decimal AverageTimeHours,
    decimal SLATargetHours
);

public record SLAByUserDto(
    Guid UserId,
    string UserName,
    int TotalCases,
    int OnTime,
    int Breached,
    decimal ComplianceRate
);

public record SLATrendDto(
    DateTime Date,
    decimal ComplianceRate,
    int TotalCases,
    int Breached
);

// Committee Report
public record CommitteeReportDto(
    DateTime FromDate,
    DateTime ToDate,
    int TotalReviews,
    int Approved,
    int Rejected,
    decimal ApprovalRate,
    decimal AverageReviewDays,
    List<CommitteeByTypeDto> ByCommitteeType,
    List<CommitteeMemberStatsDto> MemberStats
);

public record CommitteeByTypeDto(
    string CommitteeType,
    int Reviews,
    int Approved,
    int Rejected,
    decimal ApprovalRate,
    decimal AverageReviewDays
);

public record CommitteeMemberStatsDto(
    Guid UserId,
    string UserName,
    int VotesCast,
    int ApproveVotes,
    int RejectVotes,
    decimal ParticipationRate
);

// Report Request/Response
public record ReportRequestDto(
    DateTime? FromDate,
    DateTime? ToDate,
    string? ProductId,
    string? BranchId,
    string? Status,
    string? GroupBy
);

public record ExportReportRequestDto(
    string ReportCode,
    string Format, // PDF, Excel, CSV
    ReportRequestDto Parameters
);
