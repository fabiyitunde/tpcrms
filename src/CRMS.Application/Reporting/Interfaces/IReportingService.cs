using CRMS.Application.Reporting.DTOs;

namespace CRMS.Application.Reporting.Interfaces;

public interface IReportingService
{
    // Dashboard
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default);
    
    // Loan Funnel
    Task<LoanFunnelDto> GetLoanFunnelAsync(DateTime? fromDate, DateTime? toDate, CancellationToken ct = default);
    Task<LoanFunnelReportDto> GetLoanFunnelReportAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    
    // Portfolio
    Task<PortfolioSummaryDto> GetPortfolioSummaryAsync(CancellationToken ct = default);
    Task<PortfolioReportDto> GetPortfolioReportAsync(DateTime? asOfDate, CancellationToken ct = default);
    
    // Performance
    Task<PerformanceMetricsDto> GetPerformanceMetricsAsync(DateTime? fromDate, DateTime? toDate, CancellationToken ct = default);
    Task<PerformanceReportDto> GetPerformanceReportAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    
    // Decisions
    Task<DecisionDistributionDto> GetDecisionDistributionAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    
    // SLA
    Task<SLAReportDto> GetSLAReportAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    
    // Committee
    Task<CommitteeReportDto> GetCommitteeReportAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);
}
