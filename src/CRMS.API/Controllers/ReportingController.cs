using CRMS.Application.Reporting.DTOs;
using CRMS.Application.Reporting.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportingController : ControllerBase
{
    private readonly IReportingService _reportingService;

    public ReportingController(IReportingService reportingService)
    {
        _reportingService = reportingService;
    }

    /// <summary>
    /// Get dashboard summary with key metrics.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardSummaryDto>> GetDashboardSummary(CancellationToken ct)
    {
        var result = await _reportingService.GetDashboardSummaryAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Get loan funnel metrics.
    /// </summary>
    [HttpGet("funnel")]
    public async Task<ActionResult<LoanFunnelDto>> GetLoanFunnel(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var result = await _reportingService.GetLoanFunnelAsync(fromDate, toDate, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get detailed loan funnel report with stages and trends.
    /// </summary>
    [HttpGet("funnel/detailed")]
    public async Task<ActionResult<LoanFunnelReportDto>> GetLoanFunnelReport(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        CancellationToken ct)
    {
        var result = await _reportingService.GetLoanFunnelReportAsync(fromDate, toDate, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get portfolio summary.
    /// </summary>
    [HttpGet("portfolio")]
    public async Task<ActionResult<PortfolioSummaryDto>> GetPortfolioSummary(CancellationToken ct)
    {
        var result = await _reportingService.GetPortfolioSummaryAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Get detailed portfolio report with breakdowns.
    /// </summary>
    [HttpGet("portfolio/detailed")]
    public async Task<ActionResult<PortfolioReportDto>> GetPortfolioReport(
        [FromQuery] DateTime? asOfDate,
        CancellationToken ct)
    {
        var result = await _reportingService.GetPortfolioReportAsync(asOfDate, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get performance metrics.
    /// </summary>
    [HttpGet("performance")]
    public async Task<ActionResult<PerformanceMetricsDto>> GetPerformanceMetrics(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var result = await _reportingService.GetPerformanceMetricsAsync(fromDate, toDate, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get detailed performance report with user and stage breakdowns.
    /// </summary>
    [HttpGet("performance/detailed")]
    [Authorize(Roles = "Manager,RiskManager,SystemAdministrator")]
    public async Task<ActionResult<PerformanceReportDto>> GetPerformanceReport(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        CancellationToken ct)
    {
        var result = await _reportingService.GetPerformanceReportAsync(fromDate, toDate, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get decision distribution report.
    /// </summary>
    [HttpGet("decisions")]
    public async Task<ActionResult<DecisionDistributionDto>> GetDecisionDistribution(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        CancellationToken ct)
    {
        var result = await _reportingService.GetDecisionDistributionAsync(fromDate, toDate, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get SLA compliance report.
    /// </summary>
    [HttpGet("sla")]
    [Authorize(Roles = "Manager,RiskManager,ComplianceOfficer,SystemAdministrator")]
    public async Task<ActionResult<SLAReportDto>> GetSLAReport(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        CancellationToken ct)
    {
        var result = await _reportingService.GetSLAReportAsync(fromDate, toDate, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get committee activity report.
    /// </summary>
    [HttpGet("committee")]
    [Authorize(Roles = "Manager,RiskManager,ComplianceOfficer,SystemAdministrator")]
    public async Task<ActionResult<CommitteeReportDto>> GetCommitteeReport(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        CancellationToken ct)
    {
        var result = await _reportingService.GetCommitteeReportAsync(fromDate, toDate, ct);
        return Ok(result);
    }
}
