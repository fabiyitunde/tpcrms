# Audit Report: ReportingService Module

**Module ID:** 16
**Audit Date:** 2026-02-17
**Auditor:** Domain Expert Review
**Module Status (Documented):** 🟢 Completed
**Audit Verdict:** ⚠️ Performance and Accuracy Gaps — Acceptable for MVP

---

## 1. Executive Summary

The ReportingService provides a solid set of dashboards and analytics covering the loan funnel, portfolio, performance, committee activity, and SLA compliance. The design is fit for an MVP. However, the current implementation queries live tables directly without any optimization layer (no materialized views, no caching), which will cause performance degradation at scale. Several metric definitions are ambiguous, there is a privacy concern with individual performance reports, and date range filtering requires careful validation.

---

## 2. Performance Issues

### 2.1 No Caching or Materialized Views for Report Queries (HIGH)

All reporting endpoints query the operational database in real-time. As loan volume grows:
- The dashboard endpoint may execute 8–10 queries on large tables simultaneously
- `JOIN` across `LoanApplications`, `WorkflowInstances`, `WorkflowTransitionLogs`, `CommitteeReviews`, and `CommitteeMembers` tables will be slow without proper indexing
- Multiple managers loading the dashboard simultaneously will create significant read load on the production database

**Recommendation:**
- Add Redis caching for dashboard summary metrics (TTL: 5 minutes)
- Create database indexes on frequently queried columns: `LoanApplications.Status`, `LoanApplications.CreatedAt`, `WorkflowInstances.IsSLABreached`, `CommitteeReviews.Status`
- For heavy reports (portfolio aging, user performance), consider pre-computing via a nightly background job into a reporting table

### 2.2 `Dictionary<string, decimal> ProcessingTimeByStage` Type (MEDIUM)

Using `Dictionary<string, decimal>` means stage names are plain strings. A typo in the key (e.g., `"BranchReview"` vs `"Branch Review"`) produces phantom entries. This dictionary will have inconsistent keys if the `WorkflowStage.DisplayName` values change.

**Recommendation:**
- Use `Dictionary<LoanApplicationStatus, decimal>` instead
- Or use a typed DTO: `List<StagePerformanceDto>` with strongly-typed stage identifier

---

## 3. Metric Definition Issues

### 3.1 `ConversionRate` Definition Is Ambiguous (MEDIUM)

`LoanFunnelDto.ConversionRate` is not defined clearly. Possible interpretations:
- Submitted → Disbursed
- Approved → Disbursed (offer acceptance rate)
- Submitted → Approved

Each interpretation gives a different number and serves a different business purpose. Without a clear definition, stakeholders may misinterpret the metric.

**Recommendation:** Replace `ConversionRate` with two explicitly named metrics:
- `ApprovalRate`: Approved / Submitted
- `DisbursementRate`: Disbursed / Approved

### 3.2 Approval Rate Includes Withdrawn and Expired Applications (MEDIUM)

`ApprovalRate = Approved / Submitted` includes rejected and withdrawn applications in the denominator. Applications that were `BranchRejected` before credit assessment are very different from those that reached `CommitteeRejected`. Aggregating all rejections into a single rate masks the quality of applications reaching each stage.

**Recommendation:**
- Report stage-specific conversion rates (funnel analysis)
- Separate early rejections (branch) from late rejections (committee) in the decision distribution report

### 3.3 Date Range Defaults to Current Month Without Explicit Validation (LOW)

If `fromDate > toDate`, the query will return zero results with no error. If an excessively wide date range is requested (e.g., 10 years of data), the query could time out.

**Recommendation:**
- Validate `fromDate <= toDate`
- Enforce a maximum date range (e.g., 12 months for detailed reports, 5 years for trend reports)

---

## 4. Privacy and Access Control Issues

### 4.1 Individual Performance Reports May Have HR Implications (MEDIUM)

```
GET /api/reporting/performance/detailed
```

This report includes per-user metrics: applications processed, SLA compliance rate. This is employee productivity data that:
- May be used in performance reviews without user consent
- Could expose slow performers to management scrutiny without context
- Should be governed by the bank's HR data protection policy

**Recommendation:**
- Clearly document that this report contains personal performance data
- Restrict access to Senior Management and HR roles
- Consider anonymizing individual data in the report (show "User A, User B" rather than names)

### 4.2 No Currency Normalization in Amount Reporting

All amount metrics (`SubmittedAmount`, `ApprovedAmount`, `DisbursedAmount`) aggregate raw `decimal` amounts. If the system ever handles multi-currency facilities (USD, EUR alongside NGN), these totals will be meaningless.

**Recommendation:**
- For now, clearly document that all amounts are in NGN
- Add a `Currency` field to report DTOs
- Plan for currency normalization (using a reference rate) when multi-currency support is added

---

## 5. Data Completeness Issues

### 5.1 Reports Show Only Corporate Loan Data (Phase 1)

Since retail loans (Phase 2) are not yet implemented, all reports currently reflect only corporate loan data. This should be clearly communicated to business stakeholders who might misinterpret low numbers as poor performance.

**Recommendation:** Add a banner/note in the dashboard API response when retail loans are not yet active, indicating that metrics reflect corporate loans only.

### 5.2 Committee Report Does Not Distinguish Overrides (LOW)

When a chairperson records a decision that contradicts the vote majority (e.g., majority voted Reject but chairperson records Approve), this is not flagged separately in the committee analytics report. Override decisions are an important risk signal.

**Recommendation:** Add an `OverrideCount` metric to `CommitteeAnalyticsDto` — cases where the chairperson's decision contradicted the vote majority.

---

## 6. Missing Features

### 6.1 No Export Functionality

Reports cannot be exported to Excel or PDF. For regulatory reporting, compliance teams need printable/exportable reports.

**Recommendation:** Implement `?format=excel` query parameter support using a library like ClosedXML for Excel export.

### 6.2 No Scheduled Reporting

Managers cannot configure automated daily/weekly reports sent via email. This is a P3 enhancement but should be planned.

---

## 7. Recommendations Summary

| Priority | Item |
|----------|------|
| HIGH | Add Redis caching for dashboard metrics |
| HIGH | Add database indexes for common report filters |
| MEDIUM | Replace `ConversionRate` with explicit `ApprovalRate` and `DisbursementRate` |
| MEDIUM | Use strongly-typed stage keys in performance metrics dictionary |
| MEDIUM | Add date range validation (min/max bounds) |
| MEDIUM | Review individual performance report privacy implications |
| LOW | Add `OverrideCount` to committee analytics |
| LOW | Document NGN-only currency assumption in all amount metrics |
| LOW | Plan Excel/PDF export for compliance reporting |
| LOW | Plan scheduled report distribution |
