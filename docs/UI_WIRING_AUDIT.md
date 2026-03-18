# CRMS Blazor Intranet UI — Wiring Completeness Audit

**Date:** 2026-03-18  
**Scope:** `src/CRMS.Web.Intranet/` — All pages under Components/Pages  
**Service layer:** `ApplicationService.cs` (2553 lines, calls real CQRS handlers)

---

## Executive Summary

The UI has **strong real-backend wiring for core CRUD and workflow operations** but relies on **mock/fallback data in several important places**, particularly on pages that show aggregate/list data when the database is empty or services fail. The pattern used everywhere is: "call real service → if result is empty, show hardcoded mock data". This means the UI **will appear functional with fake data** even when the backend has no data, which makes it hard to distinguish "works" from "looks like it works".

### Severity Legend
- ✅ **Fully Wired** — Calls real handler, no mock fallback for primary data
- ⚠️ **Wired + Mock Fallback** — Calls real handler first, falls back to hardcoded mock if empty/error
- ❌ **Fully Mock / Stub** — No real backend call at all, or the call is a no-op
- 🟡 **Partially Wired** — Some data from backend, some hardcoded

---

## Page-by-Page Analysis

### 1. Dashboard (`/`) — `Dashboard/Index.razor`

| Aspect | Status | Detail |
|--------|--------|--------|
| Dashboard summary metrics | ⚠️ Wired + Mock | Calls `AppService.GetDashboardSummaryAsync()` → real `IReportingService.GetDashboardSummaryAsync()`. **But**: if `TotalApplications == 0`, replaces entire summary with hardcoded mock (156 apps, ₦2.45B disbursed, etc.) |
| Pending tasks list | ⚠️ Wired + Mock | Calls `AppService.GetMyPendingTasksAsync(userId)` → real `GetMyWorkflowQueueHandler`. Falls back to 3 hardcoded mock tasks if empty. |
| Recent Activity | ❌ Mock-only | `RecentActivities` are populated **only** by the mock fallback. The real `GetDashboardSummaryAsync` mapping sets `RecentActivities = new List<RecentActivity>()` (always empty). The mock fallback fills it with 3 fake entries. |
| Applications by Status | ❌ Mock-only | Same issue — real mapping sets `ApplicationsByStatus = new List<ApplicationByStatus>()` (always empty). Only populated by mock fallback. |
| "+12% from last month" text | ❌ Hardcoded | The "+12% from last month" and "+8% approval rate" badges are static HTML strings, not data-driven. |

**Verdict:** Dashboard metrics CAN come from real reporting service, but "Recent Activity" and "Applications by Status" sections are **structurally broken** — always empty from real data, always mock. The growth percentages are hardcoded strings.

---

### 2. Application Detail (`/applications/{Id:guid}`) — `Applications/Detail.razor`

| Aspect | Status | Detail |
|--------|--------|--------|
| Core application data | ✅ Fully Wired | `GetApplicationDetailAsync()` → `GetLoanApplicationByIdHandler` — real handler, maps all fields from domain |
| Directors & Signatories | ✅ Fully Wired | Loaded from `app.Parties` in `GetApplicationDetailAsync` |
| Documents | ✅ Fully Wired | Loaded from `app.Documents` with upload/verify/reject all wired to real handlers |
| Financial Statements | ✅ Fully Wired | `GetFinancialStatementsForApplicationAsync` → real handler. CRUD (create, edit, delete, delete-all) all wired. |
| Bank Statements | ✅ Fully Wired | `GetBankStatementsAsync` → real handler. Upload, verify, reject, analyze all wired. |
| Credit Bureau Reports | ✅ Fully Wired | `GetBureauReportsAsync` → real `GetBureauReportsByLoanApplicationHandler` |
| Collateral | ✅ Fully Wired | Full CRUD + valuation + approve + document upload — all via real handlers |
| Guarantors | ✅ Fully Wired | Full CRUD + approve/reject — all via real handlers |
| Advisory (AI) tab | 🟡 Partially Wired | `GenerateAdvisory()` calls real `GenerateCreditAdvisoryHandler`. **However**, the advisory data displayed in the tab comes from `application.Advisory`, which is **NOT loaded from the backend** in `GetApplicationDetailAsync()` — it's only populated by the mock `GenerateMockApplication()`. Real applications will show `Advisory == null` → empty state. |
| Workflow History tab | 🟡 Partially Wired | `application.WorkflowHistory` is **NOT loaded from the backend** in `GetApplicationDetailAsync()` — only populated by mock. Real apps will show empty workflow history. |
| Committee tab | 🟡 Partially Wired | `application.Committee` is **NOT loaded from the backend** in `GetApplicationDetailAsync()` — only populated by mock. `CastVote()` IS wired to real handler though. |
| Comments tab | ❌ Stub | `AddComment()` is explicitly stubbed: `// Comment functionality would need its own handler` — just calls `LoadApplication()`. Comments list is only populated by mock. |
| Approve/Return/Reject buttons | ✅ Fully Wired | All three call real `TransitionWorkflowHandler` via `ApproveApplicationAsync`, `ReturnApplicationAsync`, `RejectApplicationAsync` |
| Submit for Review | ✅ Fully Wired | Calls `SubmitApplicationAsync` → real `SubmitLoanApplicationHandler` |
| Generate Loan Pack | ✅ Fully Wired | Calls `GenerateLoanPackAsync` → real `GenerateLoanPackHandler` |
| Fallback mock application | ⚠️ | If `GetApplicationDetailAsync` returns null, generates a **full hardcoded mock application** with fake directors, bureau reports, advisory, workflow, committee, comments. This means navigating to a non-existent application ID shows convincing fake data instead of an error. |

**Verdict:** The Detail page has excellent wiring for core data (documents, financials, bureau, collateral, guarantors, workflow actions). But Advisory, WorkflowHistory, Committee, and Comments data are **not loaded from the backend for real applications** — these sections only show data for the mock fallback application.

---

### 3. My Queue (`/queues/my`) — `Queues/MyQueue.razor`

| Aspect | Status | Detail |
|--------|--------|--------|
| Pending tasks | ⚠️ Wired + Mock | Calls `AppService.GetMyPendingTasksAsync(userId)` → real handler. Falls back to `GenerateMockTasks()` (4 hardcoded items) if empty. |
| Click → detail navigation | ✅ Fully Wired | |

---

### 4. All Queues (`/queues/all`) — `Queues/AllQueues.razor`

| Aspect | Status | Detail |
|--------|--------|--------|
| Queue summary cards | ⚠️ Wired + Mock | Calls `AppService.GetQueueSummaryAsync()` → real `GetQueueSummaryHandler`. Falls back to 6 hardcoded mock queues if empty or error. |
| Queue detail items | ⚠️ Wired + Mock | Calls `AppService.GetQueueByRoleAsync(stage)` → real `GetWorkflowQueueByRoleHandler`. Falls back to 3 mock items if empty or error. |

---

### 5. Overdue Queue (`/queues/overdue`) — `Queues/Overdue.razor`

| Aspect | Status | Detail |
|--------|--------|--------|
| Overdue items | ❌ Fully Mock | Does `await Task.Delay(300)` then hardcodes 3 mock overdue items. **No backend call at all.** |

---

### 6. Committee Reviews (`/committee/reviews`) — `Committee/Reviews.razor`

| Aspect | Status | Detail |
|--------|--------|--------|
| Reviews list | ⚠️ Wired + Mock | Calls `AppService.GetCommitteeReviewsByStatusAsync()` → real `GetCommitteeReviewsByStatusHandler`. Falls back to 5 hardcoded mock reviews if empty. |
| Search/filter | ⚠️ Limited | Status filter is passed to backend. Search term and committee type filter are **not sent to backend** — only used for UI filtering (and actually not even filtered client-side). |
| Votes progress (X/Y) | ❌ Mock-only | `VotesCast` and `TotalMembers` are **not returned by the real backend mapping** (only set to 0). Only populated in mock fallback. |

---

### 7. My Pending Votes (`/committee/my-votes`) — `Committee/MyVotes.razor`

| Aspect | Status | Detail |
|--------|--------|--------|
| Pending votes list | ❌ Fully Mock | Does `await Task.Delay(300)` then hardcodes 3 mock pending votes. **Does NOT call `AppService.GetMyPendingVotesAsync()`** even though that method exists and is fully wired to a real handler. |
| Cast Vote action | ✅ Fully Wired | `SubmitVote()` calls `AppService.CastVoteAsync()` → real `CastVoteHandler` |
| Vote modal | ✅ Fully Wired | UI properly sends vote + comments |

**Verdict:** The vote submission works, but the **list of what to vote on is entirely fake**. The real `GetMyPendingVotesAsync` exists but is not called.

---

### 8. Reports Index (`/reports`) — `Reports/Index.razor`

| Aspect | Status | Detail |
|--------|--------|--------|
| Top-level metrics (Applications, Approved, Avg Time, Disbursed) | ⚠️ Wired + Mock | Calls `AppService.GetReportingMetricsAsync()` → real `IReportingService`. Falls back to mock if `ApplicationsReceived == 0`. |
| Growth percentages (+12%, +18%) | ❌ Hardcoded | `ApplicationsGrowth = 12`, `DisbursementGrowth = 18` are hardcoded even when real data is used. |
| Application Funnel | ❌ Mock / Calculated | Funnel stages are **entirely derived from the top-level metrics** with hardcoded percentages (91%, 80%, 63%, etc.). Not from real funnel data. |
| Portfolio by Product | ❌ Fully Mock | 4 hardcoded products with fake amounts. No backend call. |
| Decision Distribution | 🟡 Partially | Uses `metrics.Approved` from real data but calculates rejected/pending with hardcoded percentages. |
| SLA Compliance gauge | ❌ Hardcoded | `slaCompliance = 87`, `withinSla = 145`, `breachedSla = 22` are hardcoded fields. |
| Export button | ❌ No-op | Button exists but has no `@onclick` handler. |
| Period selector | ❌ No-op | `selectedPeriod` is bound but **never passed to any service call**. |

---

### 9. Audit Trail (`/reports/audit`) — `Reports/Audit.razor`

| Aspect | Status | Detail |
|--------|--------|--------|
| Audit logs | ⚠️ Wired + Mock | Calls `AppService.GetAuditLogsAsync()` → real `GetRecentAuditLogsHandler`. Falls back to 8 hardcoded mock entries if empty. |
| Search filter | ⚠️ Partial | Action type and date filters are passed to service method signature but `GetRecentAuditLogsQuery` **takes no parameters** — it always returns recent logs without filtering. Search term is not used at all. |
| Pagination | ❌ Fake | `totalCount = 150` and `totalPages = 8` are hardcoded. Previous/Next buttons are disabled based on `currentPage` but there's no pagination logic. |
| Export button | ❌ No-op | No handler attached. |

---

### 10. Performance Report (`/reports/performance`) — `Reports/Performance.razor`

| Aspect | Status | Detail |
|--------|--------|--------|
| All metrics | ✅ Fully Wired | Calls `AppService.GetPerformanceReportDataAsync(periodDays)` → real `IReportingService.GetPerformanceReportAsync`, `GetSLAReportAsync`, `GetPerformanceMetricsAsync`. Returns `PerformanceReportData` with empty lists on error. |
| Stage performance | ✅ Fully Wired | From `slaReport.ByStage` |
| Top performers | ✅ Fully Wired | From `report.ByUser` |
| Team performance | ✅ Fully Wired | From `report.ByStage` |
| Export button | ❌ No-op | No handler attached. |

---

### 11. Committee Report (`/reports/committee`) — `Reports/Committee.razor`

| Aspect | Status | Detail |
|--------|--------|--------|
| All metrics | ✅ Fully Wired | Calls `AppService.GetCommitteeReportDataAsync(periodDays)` → real `IReportingService.GetCommitteeReportAsync`. Returns empty data on error. |
| Member participation | ✅ Fully Wired | From `report.MemberStats` |
| Export button | ❌ No-op | No handler attached. |

---

## Summary: Critical Gaps

### 1. Structurally Broken (Data Never Loaded for Real Applications)

| Feature | Location | Issue |
|---------|----------|-------|
| Advisory data display | Detail.razor → AdvisoryTab | `application.Advisory` never populated from backend for real apps |
| Workflow History | Detail.razor → WorkflowTab | `application.WorkflowHistory` never populated from backend for real apps |
| Committee data display | Detail.razor → CommitteeTab | `application.Committee` never populated from backend for real apps |
| Comments display | Detail.razor → CommentsTab | `application.Comments` never populated from backend for real apps |
| Recent Activity (Dashboard) | Dashboard/Index.razor | Mapping always returns empty list; only mock has data |
| Applications by Status (Dashboard) | Dashboard/Index.razor | Mapping always returns empty list; only mock has data |

### 2. Entirely Mock Pages (No Backend Calls)

| Page | Issue |
|------|-------|
| `/queues/overdue` | 100% hardcoded mock, `Task.Delay(300)` simulates loading |
| `/committee/my-votes` | 100% hardcoded mock, ignores existing `GetMyPendingVotesAsync` method |

### 3. Stubbed Actions

| Action | Location | Issue |
|--------|----------|-------|
| AddComment | Detail.razor | Explicitly stubbed: `// Comment functionality would need its own handler` |
| Export buttons | Reports pages | All export buttons are decorative — no onclick handler |
| Download document | ApplicationService | `DownloadDocumentAsync` returns null with "not yet fully implemented" log |

### 4. Misleading Hardcoded Values

| Value | Location | Issue |
|-------|----------|-------|
| "+12% from last month" | Dashboard | Static HTML string |
| "+8% approval rate" | Dashboard | Static HTML string |
| Growth percentages in Reports | Reports/Index | `ApplicationsGrowth=12`, `DisbursementGrowth=18` hardcoded |
| SLA compliance = 87% | Reports/Index | Hardcoded field |
| Funnel percentages | Reports/Index | 91%, 80%, 63% are hardcoded multipliers |
| Portfolio by Product | Reports/Index | 4 products with fake amounts, no backend |
| Audit pagination | Reports/Audit | totalCount=150, totalPages=8 hardcoded |

### 5. Silent Mock Fallback Pattern (Works But Hides Issues)

Every list page follows: `call real service → if empty, show mock data`. This means:
- If the database is empty → UI shows fake data (looks like it works)
- If the service throws → UI shows fake data (silently swallows errors)
- If auth is wrong → might still show fake data

Pages using this pattern: Dashboard, MyQueue, AllQueues, Committee Reviews, Reports Index, Audit Trail.

---

## What IS Truly Wired (Strengths)

The following features have **end-to-end real implementation** with no mock fallback for the primary operation:

1. **Application creation** (New.razor → InitiateCorporateLoanCommand)
2. **Application detail loading** (core fields, parties, documents, financials, collateral, guarantors, bureau reports)
3. **Document upload/verify/reject**
4. **Financial statement CRUD** (create, edit, delete, delete-all, view)
5. **Bank statement upload/verify/reject/analyze**
6. **Collateral full CRUD + valuation + approval + document upload**
7. **Guarantor full CRUD + approve/reject**
8. **Workflow transitions** (approve, return, reject, submit for review)
9. **Bureau report retrieval**
10. **Advisory generation** (trigger — but display is broken)
11. **Committee vote casting** (action — but vote list is mock)
12. **Loan pack generation**
13. **Performance & Committee reports** (fully wired to IReportingService)
14. **Admin pages** (Users, Products, Locations, Scoring Config — all wired)
