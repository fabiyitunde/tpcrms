using CRMS.Application.Reporting.DTOs;
using CRMS.Application.Reporting.Interfaces;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Services;

public class ReportingService : IReportingService
{
    private readonly CRMSDbContext _context;

    public ReportingService(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var funnel = await GetLoanFunnelAsync(startOfMonth, now, ct);
        var portfolio = await GetPortfolioSummaryAsync(ct);
        var performance = await GetPerformanceMetricsAsync(startOfMonth, now, ct);
        var pending = await GetPendingActionsAsync(ct);

        return new DashboardSummaryDto(funnel, portfolio, performance, pending);
    }

    public async Task<LoanFunnelDto> GetLoanFunnelAsync(DateTime? fromDate, DateTime? toDate, CancellationToken ct = default)
    {
        var query = _context.LoanApplications.AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(x => x.CreatedAt >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(x => x.CreatedAt <= toDate.Value);

        var applications = await query.ToListAsync(ct);

        var submitted = applications.Count;
        var inReview = applications.Count(x => x.Status == LoanApplicationStatus.BranchReview || 
                                                x.Status == LoanApplicationStatus.CreditAnalysis ||
                                                x.Status == LoanApplicationStatus.HOReview);
        var approved = applications.Count(x => x.Status == LoanApplicationStatus.Approved);
        var rejected = applications.Count(x => x.Status == LoanApplicationStatus.Rejected ||
                                               x.Status == LoanApplicationStatus.BranchRejected ||
                                               x.Status == LoanApplicationStatus.CommitteeRejected);
        var disbursed = applications.Count(x => x.Status == LoanApplicationStatus.Disbursed);

        var submittedAmount = applications.Sum(x => x.RequestedAmount.Amount);
        var approvedAmount = applications.Where(x => x.Status == LoanApplicationStatus.Approved || 
                                                      x.Status == LoanApplicationStatus.Disbursed)
                                          .Sum(x => x.ApprovedAmount?.Amount ?? x.RequestedAmount.Amount);
        var disbursedAmount = applications.Where(x => x.Status == LoanApplicationStatus.Disbursed)
                                           .Sum(x => x.ApprovedAmount?.Amount ?? x.RequestedAmount.Amount);

        var approvalRate = submitted > 0 ? (decimal)(approved + disbursed) / submitted * 100 : 0;
        var conversionRate = submitted > 0 ? (decimal)disbursed / submitted * 100 : 0;

        return new LoanFunnelDto(
            submitted, inReview, approved, rejected, disbursed,
            submittedAmount, approvedAmount, disbursedAmount,
            Math.Round(approvalRate, 2), Math.Round(conversionRate, 2));
    }

    public async Task<LoanFunnelReportDto> GetLoanFunnelReportAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var applications = await _context.LoanApplications
            .Where(x => x.CreatedAt >= fromDate && x.CreatedAt <= toDate)
            .ToListAsync(ct);

        var total = applications.Count;
        var stages = new List<FunnelStageDto>();

        // Define funnel stages
        var stageData = new[]
        {
            ("Submitted", applications.Where(x => x.Status == LoanApplicationStatus.Draft || x.Status == LoanApplicationStatus.Submitted).ToList()),
            ("In Review", applications.Where(x => x.Status == LoanApplicationStatus.BranchReview || x.Status == LoanApplicationStatus.CreditAnalysis || x.Status == LoanApplicationStatus.HOReview).ToList()),
            ("Approved", applications.Where(x => x.Status == LoanApplicationStatus.Approved || x.Status == LoanApplicationStatus.CommitteeApproved).ToList()),
            ("Disbursed", applications.Where(x => x.Status == LoanApplicationStatus.Disbursed).ToList()),
            ("Rejected", applications.Where(x => x.Status == LoanApplicationStatus.Rejected || x.Status == LoanApplicationStatus.BranchRejected || x.Status == LoanApplicationStatus.CommitteeRejected).ToList())
        };

        var previousCount = total;
        foreach (var (stage, stageApps) in stageData)
        {
            var count = stageApps.Count;
            var amount = stageApps.Sum(x => x.RequestedAmount.Amount);
            var percentOfTotal = total > 0 ? (decimal)count / total * 100 : 0;
            var conversionFromPrev = previousCount > 0 ? (decimal)count / previousCount * 100 : 0;

            stages.Add(new FunnelStageDto(stage, count, amount, Math.Round(percentOfTotal, 2), Math.Round(conversionFromPrev, 2)));
            previousCount = count > 0 ? count : previousCount;
        }

        // Daily trend
        var dailyTrend = applications
            .GroupBy(x => x.CreatedAt.Date)
            .OrderBy(x => x.Key)
            .Select(g => new FunnelTrendDto(
                g.Key,
                g.Count(),
                g.Count(x => x.Status == LoanApplicationStatus.Approved || x.Status == LoanApplicationStatus.Disbursed),
                g.Count(x => x.Status == LoanApplicationStatus.Rejected || x.Status == LoanApplicationStatus.BranchRejected || x.Status == LoanApplicationStatus.CommitteeRejected),
                g.Count(x => x.Status == LoanApplicationStatus.Disbursed)))
            .ToList();

        return new LoanFunnelReportDto(fromDate, toDate, stages, dailyTrend);
    }

    public async Task<PortfolioSummaryDto> GetPortfolioSummaryAsync(CancellationToken ct = default)
    {
        var activeLoans = await _context.LoanApplications
            .Where(x => x.Status == LoanApplicationStatus.Disbursed)
            .ToListAsync(ct);

        var totalActive = activeLoans.Count;
        var totalOutstanding = activeLoans.Sum(x => x.ApprovedAmount?.Amount ?? x.RequestedAmount.Amount);
        var avgTicketSize = totalActive > 0 ? totalOutstanding / totalActive : 0;

        var corporateLoans = activeLoans.Count(x => x.Type == LoanApplicationType.Corporate);
        var retailLoans = activeLoans.Count(x => x.Type == LoanApplicationType.Retail);
        var corporateOutstanding = activeLoans.Where(x => x.Type == LoanApplicationType.Corporate)
                                               .Sum(x => x.ApprovedAmount?.Amount ?? x.RequestedAmount.Amount);
        var retailOutstanding = activeLoans.Where(x => x.Type == LoanApplicationType.Retail)
                                            .Sum(x => x.ApprovedAmount?.Amount ?? x.RequestedAmount.Amount);

        var loansByProduct = activeLoans
            .GroupBy(x => x.ProductCode ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Count());

        var outstandingByProduct = activeLoans
            .GroupBy(x => x.ProductCode ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Sum(x => x.ApprovedAmount?.Amount ?? x.RequestedAmount.Amount));

        return new PortfolioSummaryDto(
            totalActive, totalOutstanding, avgTicketSize,
            corporateLoans, retailLoans, corporateOutstanding, retailOutstanding,
            loansByProduct, outstandingByProduct);
    }

    public async Task<PortfolioReportDto> GetPortfolioReportAsync(DateTime? asOfDate, CancellationToken ct = default)
    {
        var effectiveDate = asOfDate ?? DateTime.UtcNow;
        var summary = await GetPortfolioSummaryAsync(ct);

        var activeLoans = await _context.LoanApplications
            .Where(x => x.Status == LoanApplicationStatus.Disbursed)
            .ToListAsync(ct);

        var byProduct = activeLoans
            .GroupBy(x => x.ProductCode ?? "Unknown")
            .Select(g => new PortfolioByProductDto(
                g.Key,
                g.Count(),
                g.Sum(x => x.ApprovedAmount?.Amount ?? x.RequestedAmount.Amount),
                summary.TotalOutstanding > 0 
                    ? Math.Round(g.Sum(x => x.ApprovedAmount?.Amount ?? x.RequestedAmount.Amount) / summary.TotalOutstanding * 100, 2) 
                    : 0))
            .ToList();

        var byBranch = new List<PortfolioByBranchDto>
        {
            new("Head Office", activeLoans.Count, summary.TotalOutstanding, 100)
        };

        var byRiskRating = new List<PortfolioByRiskRatingDto>
        {
            new("Low", 0, 0, 0),
            new("Moderate", 0, 0, 0),
            new("High", 0, 0, 0)
        };

        var aging = new List<PortfolioAgingDto>
        {
            new("0-30 days", activeLoans.Count, summary.TotalOutstanding, 100),
            new("31-60 days", 0, 0, 0),
            new("61-90 days", 0, 0, 0),
            new("90+ days", 0, 0, 0)
        };

        return new PortfolioReportDto(effectiveDate, summary, byProduct, byBranch, byRiskRating, aging);
    }

    public async Task<PerformanceMetricsDto> GetPerformanceMetricsAsync(DateTime? fromDate, DateTime? toDate, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var effectiveFrom = fromDate ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var effectiveTo = toDate ?? now;
        
        var lastMonthStart = effectiveFrom.AddMonths(-1);
        var lastMonthEnd = effectiveFrom.AddDays(-1);

        var thisMonthApps = await _context.LoanApplications
            .Where(x => x.CreatedAt >= effectiveFrom && x.CreatedAt <= effectiveTo)
            .CountAsync(ct);

        var lastMonthApps = await _context.LoanApplications
            .Where(x => x.CreatedAt >= lastMonthStart && x.CreatedAt <= lastMonthEnd)
            .CountAsync(ct);

        var growth = lastMonthApps > 0 ? (decimal)(thisMonthApps - lastMonthApps) / lastMonthApps * 100 : 0;

        var workflows = await _context.WorkflowInstances
            .Where(x => x.CreatedAt >= effectiveFrom && x.CreatedAt <= effectiveTo)
            .ToListAsync(ct);

        var totalWorkflows = workflows.Count;
        var slaCompliant = workflows.Count(x => !x.IsSLABreached);
        var slaComplianceRate = totalWorkflows > 0 ? (decimal)slaCompliant / totalWorkflows * 100 : 100;

        var completedApps = await _context.LoanApplications
            .Where(x => x.CreatedAt >= effectiveFrom && x.CreatedAt <= effectiveTo)
            .Where(x => x.Status == LoanApplicationStatus.Approved || 
                        x.Status == LoanApplicationStatus.Rejected ||
                        x.Status == LoanApplicationStatus.Disbursed)
            .ToListAsync(ct);

        var avgProcessingDays = completedApps.Count > 0
            ? completedApps.Average(x => (x.ModifiedAt ?? x.CreatedAt).Subtract(x.CreatedAt).TotalDays)
            : 0;

        var processingTimeByStage = new Dictionary<string, decimal>
        {
            { "Submission", 0.5m },
            { "Credit Check", 1.0m },
            { "Analysis", 2.0m },
            { "Committee", 3.0m },
            { "Approval", 1.0m }
        };

        return new PerformanceMetricsDto(
            Math.Round((decimal)avgProcessingDays, 2),
            Math.Round((decimal)avgProcessingDays * 0.7m, 2),
            Math.Round(slaComplianceRate, 2),
            thisMonthApps,
            lastMonthApps,
            Math.Round(growth, 2),
            processingTimeByStage);
    }

    public async Task<PerformanceReportDto> GetPerformanceReportAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var overall = await GetPerformanceMetricsAsync(fromDate, toDate, ct);

        var transitionLogs = await _context.WorkflowTransitionLogs
            .Where(x => x.CreatedAt >= fromDate && x.CreatedAt <= toDate)
            .ToListAsync(ct);

        var byUser = transitionLogs
            .GroupBy(x => x.PerformedByUserId)
            .Select(g => new PerformanceByUserDto(
                g.Key,
                "User",
                "Staff",
                g.Count(),
                0,
                100))
            .Take(20)
            .ToList();

        var byStage = transitionLogs
            .GroupBy(x => x.ToStatus.ToString())
            .Select(g => new PerformanceByStageDto(
                g.Key,
                g.Count(),
                0,
                100))
            .ToList();

        var trend = transitionLogs
            .GroupBy(x => x.CreatedAt.Date)
            .OrderBy(x => x.Key)
            .Select(g => new PerformanceTrendDto(
                g.Key,
                g.Count(),
                0,
                100))
            .ToList();

        return new PerformanceReportDto(fromDate, toDate, overall, byUser, byStage, trend);
    }

    public async Task<DecisionDistributionDto> GetDecisionDistributionAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var decisions = await _context.LoanApplications
            .Where(x => x.CreatedAt >= fromDate && x.CreatedAt <= toDate)
            .Where(x => x.Status == LoanApplicationStatus.Approved ||
                        x.Status == LoanApplicationStatus.Rejected ||
                        x.Status == LoanApplicationStatus.Disbursed ||
                        x.Status == LoanApplicationStatus.BranchRejected ||
                        x.Status == LoanApplicationStatus.CommitteeRejected)
            .ToListAsync(ct);

        var total = decisions.Count;
        var approved = decisions.Count(x => x.Status == LoanApplicationStatus.Approved || x.Status == LoanApplicationStatus.Disbursed);
        var rejected = decisions.Count(x => x.Status == LoanApplicationStatus.Rejected || 
                                            x.Status == LoanApplicationStatus.BranchRejected || 
                                            x.Status == LoanApplicationStatus.CommitteeRejected);
        var referred = 0;

        var approvalRate = total > 0 ? (decimal)approved / total * 100 : 0;

        var byProduct = decisions
            .GroupBy(x => x.ProductCode ?? "Unknown")
            .Select(g => new DecisionByProductDto(
                g.Key,
                g.Count(x => x.Status == LoanApplicationStatus.Approved || x.Status == LoanApplicationStatus.Disbursed),
                g.Count(x => x.Status == LoanApplicationStatus.Rejected || x.Status == LoanApplicationStatus.BranchRejected || x.Status == LoanApplicationStatus.CommitteeRejected),
                g.Count() > 0 
                    ? Math.Round((decimal)g.Count(x => x.Status == LoanApplicationStatus.Approved || x.Status == LoanApplicationStatus.Disbursed) / g.Count() * 100, 2)
                    : 0))
            .ToList();

        var byRiskScore = new List<DecisionByRiskScoreDto>
        {
            new("0-30 (High Risk)", 0, 0, 0, 0),
            new("31-60 (Medium Risk)", 0, 0, 0, 0),
            new("61-100 (Low Risk)", 0, 0, 0, 0)
        };

        var topRejectionReasons = new List<TopRejectionReasonDto>
        {
            new("Insufficient Income", 0, 0),
            new("Poor Credit History", 0, 0),
            new("High Debt Ratio", 0, 0)
        };

        return new DecisionDistributionDto(
            fromDate, toDate, total, approved, rejected, referred,
            Math.Round(approvalRate, 2), byProduct, byRiskScore, topRejectionReasons);
    }

    public async Task<SLAReportDto> GetSLAReportAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var workflows = await _context.WorkflowInstances
            .Where(x => x.CreatedAt >= fromDate && x.CreatedAt <= toDate)
            .ToListAsync(ct);

        var total = workflows.Count;
        var onTime = workflows.Count(x => !x.IsSLABreached);
        var breached = workflows.Count(x => x.IsSLABreached);
        var overallCompliance = total > 0 ? (decimal)onTime / total * 100 : 100;

        var stages = await _context.WorkflowStages.ToListAsync(ct);

        var byStage = stages.Select(s => new SLAByStageDto(
            s.DisplayName,
            0, 0, 0, 100,
            0,
            s.SLAHours
        )).ToList();

        var byUser = new List<SLAByUserDto>();

        var trend = workflows
            .GroupBy(x => x.CreatedAt.Date)
            .OrderBy(x => x.Key)
            .Select(g => new SLATrendDto(
                g.Key,
                g.Count() > 0 ? Math.Round((decimal)g.Count(x => !x.IsSLABreached) / g.Count() * 100, 2) : 100,
                g.Count(),
                g.Count(x => x.IsSLABreached)))
            .ToList();

        return new SLAReportDto(fromDate, toDate, Math.Round(overallCompliance, 2), total, onTime, breached, byStage, byUser, trend);
    }

    public async Task<CommitteeReportDto> GetCommitteeReportAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var reviews = await _context.CommitteeReviews
            .Include(x => x.Members)
            .Where(x => x.CreatedAt >= fromDate && x.CreatedAt <= toDate)
            .ToListAsync(ct);

        var total = reviews.Count;
        var approved = reviews.Count(x => x.FinalDecision == CommitteeDecision.Approved || x.FinalDecision == CommitteeDecision.ApprovedWithConditions);
        var rejected = reviews.Count(x => x.FinalDecision == CommitteeDecision.Rejected);
        var approvalRate = total > 0 ? (decimal)approved / total * 100 : 0;

        var avgReviewDays = reviews.Where(x => x.DecisionAt.HasValue)
            .Select(x => (x.DecisionAt!.Value - x.CreatedAt).TotalDays)
            .DefaultIfEmpty(0)
            .Average();

        var byCommitteeType = reviews
            .GroupBy(x => x.CommitteeType)
            .Select(g => new CommitteeByTypeDto(
                g.Key.ToString(),
                g.Count(),
                g.Count(x => x.FinalDecision == CommitteeDecision.Approved || x.FinalDecision == CommitteeDecision.ApprovedWithConditions),
                g.Count(x => x.FinalDecision == CommitteeDecision.Rejected),
                g.Count() > 0 ? Math.Round((decimal)g.Count(x => x.FinalDecision == CommitteeDecision.Approved || x.FinalDecision == CommitteeDecision.ApprovedWithConditions) / g.Count() * 100, 2) : 0,
                Math.Round((decimal)g.Where(x => x.DecisionAt.HasValue)
                    .Select(x => (x.DecisionAt!.Value - x.CreatedAt).TotalDays)
                    .DefaultIfEmpty(0).Average(), 2)))
            .ToList();

        var memberStats = reviews
            .SelectMany(r => r.Members)
            .GroupBy(m => m.UserId)
            .Select(g => new CommitteeMemberStatsDto(
                g.Key,
                g.First().UserName,
                g.Count(x => x.Vote.HasValue),
                g.Count(x => x.Vote == CommitteeVote.Approve),
                g.Count(x => x.Vote == CommitteeVote.Reject),
                g.Count() > 0 ? Math.Round((decimal)g.Count(x => x.Vote.HasValue) / g.Count() * 100, 2) : 0))
            .ToList();

        return new CommitteeReportDto(
            fromDate, toDate, total, approved, rejected,
            Math.Round(approvalRate, 2), Math.Round((decimal)avgReviewDays, 2),
            byCommitteeType, memberStats);
    }

    private async Task<PendingActionsDto> GetPendingActionsAsync(CancellationToken ct = default)
    {
        var pendingApps = await _context.LoanApplications
            .CountAsync(x => x.Status == LoanApplicationStatus.BranchReview || 
                             x.Status == LoanApplicationStatus.CreditAnalysis ||
                             x.Status == LoanApplicationStatus.HOReview, ct);

        var overdueSlAs = await _context.WorkflowInstances
            .CountAsync(x => x.IsSLABreached && x.CompletedAt == null, ct);

        var pendingVotes = await _context.CommitteeMembers
            .CountAsync(x => x.Vote == null, ct);

        var awaitingDisbursement = await _context.LoanApplications
            .CountAsync(x => x.Status == LoanApplicationStatus.Approved, ct);

        return new PendingActionsDto(pendingApps, overdueSlAs, pendingVotes, 0, awaitingDisbursement);
    }
}
