# ReportingService Module

## Overview

The ReportingService module provides dashboards and analytics for business insights. It aggregates data from all modules to present loan funnel metrics, portfolio analysis, performance tracking, and compliance reporting.

## Key Features

- **Dashboard Summary**: At-a-glance view of key metrics
- **Loan Funnel**: Track applications through stages (Submitted → In Review → Approved → Disbursed)
- **Portfolio Analytics**: Active loans breakdown by product, type, and risk
- **Performance Metrics**: Processing times, SLA compliance, user productivity
- **Decision Distribution**: Approval rates, rejection reasons
- **Committee Analytics**: Committee activity, voting patterns, member participation
- **SLA Compliance**: Track breaches and compliance rates by stage

## API Endpoints

| Method | Endpoint | Description | Access |
|--------|----------|-------------|--------|
| GET | /api/reporting/dashboard | Dashboard summary with all key metrics | Authenticated |
| GET | /api/reporting/funnel | Loan funnel metrics | Authenticated |
| GET | /api/reporting/funnel/detailed | Detailed funnel with stages and trends | Authenticated |
| GET | /api/reporting/portfolio | Portfolio summary | Authenticated |
| GET | /api/reporting/portfolio/detailed | Detailed portfolio breakdown | Authenticated |
| GET | /api/reporting/performance | Performance metrics | Authenticated |
| GET | /api/reporting/performance/detailed | User and stage performance | Manager+ |
| GET | /api/reporting/decisions | Decision distribution | Authenticated |
| GET | /api/reporting/sla | SLA compliance report | Manager+ |
| GET | /api/reporting/committee | Committee activity report | Manager+ |

## Dashboard Summary

The dashboard endpoint returns a consolidated view of:

### Loan Funnel
- Submitted, In Review, Approved, Rejected, Disbursed counts
- Total amounts at each stage
- Approval and conversion rates

### Portfolio Summary
- Total active loans and outstanding balance
- Corporate vs Retail breakdown
- Distribution by product

### Performance Metrics
- Average processing time (days)
- SLA compliance rate
- Month-over-month application growth
- Processing time by stage

### Pending Actions
- Applications pending review
- Overdue SLAs
- Pending committee votes
- Awaiting disbursement

## Report DTOs

### LoanFunnelDto
```csharp
record LoanFunnelDto(
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
```

### PortfolioSummaryDto
```csharp
record PortfolioSummaryDto(
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
```

### PerformanceMetricsDto
```csharp
record PerformanceMetricsDto(
    decimal AverageProcessingTimeDays,
    decimal AverageApprovalTimeDays,
    decimal SLAComplianceRate,
    int TotalApplicationsThisMonth,
    int TotalApplicationsLastMonth,
    decimal MonthOverMonthGrowth,
    Dictionary<string, decimal> ProcessingTimeByStage
);
```

## Detailed Reports

### Loan Funnel Report
- Stage-by-stage breakdown with conversion percentages
- Daily trend data for submitted, approved, rejected, disbursed

### Portfolio Report
- Breakdown by product, branch, risk rating
- Aging buckets (0-30, 31-60, 61-90, 90+ days)

### Performance Report
- Individual user performance (applications processed, SLA compliance)
- Stage-level performance metrics
- Daily trend data

### Decision Distribution
- Approval/rejection breakdown by product
- Risk score distribution
- Top rejection reasons

### SLA Report
- Overall compliance rate
- Stage-by-stage compliance with SLA targets
- Daily compliance trend

### Committee Report
- Reviews by committee type
- Member participation and voting patterns
- Average review duration

## Data Sources

The ReportingService aggregates data from:

| Source | Data |
|--------|------|
| LoanApplications | Application counts, amounts, statuses |
| WorkflowInstances | SLA tracking, processing times |
| WorkflowTransitionLogs | Stage transitions, user actions |
| WorkflowStages | SLA targets by stage |
| CommitteeReviews | Committee decisions, review durations |
| CommitteeMembers | Voting patterns, participation |

## Date Range Filtering

Most report endpoints accept optional date range parameters:

```
GET /api/reporting/funnel?fromDate=2026-01-01&toDate=2026-01-31
```

- `fromDate`: Start of reporting period (inclusive)
- `toDate`: End of reporting period (inclusive)
- If not provided, defaults to current month

## Access Control

| Report | Required Role |
|--------|---------------|
| Dashboard, Funnel, Portfolio | Authenticated user |
| Performance (detailed) | Manager, RiskManager, SystemAdministrator |
| SLA Report | Manager, RiskManager, ComplianceOfficer, SystemAdministrator |
| Committee Report | Manager, RiskManager, ComplianceOfficer, SystemAdministrator |

## Files

### Application Layer
- `Application/Reporting/Interfaces/IReportingService.cs` - Service interface
- `Application/Reporting/DTOs/ReportingDtos.cs` - All report DTOs

### Infrastructure Layer
- `Infrastructure/Services/ReportingService.cs` - Implementation

### API Layer
- `API/Controllers/ReportingController.cs` - REST endpoints

### Domain Layer
- `Domain/Aggregates/Reporting/ReportDefinition.cs` - (Optional) Saved report configurations

## Future Enhancements

1. **Export to Excel/PDF**: Generate downloadable reports
2. **Scheduled Reports**: Auto-generate and email reports on schedule
3. **Custom Report Builder**: User-defined report configurations
4. **Real-time Dashboard**: WebSocket-based live updates
5. **Comparative Analytics**: Period-over-period comparisons
6. **Predictive Analytics**: ML-based forecasting
7. **Drill-down Capability**: Click through to individual records
