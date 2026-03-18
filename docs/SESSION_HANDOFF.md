# CRMS — Session Handoff Document

**Last Updated:** 2026-03-18 (Session 23)
**Project:** Credit Risk Management System (CRMS)
**Working Directory:** `C:\Users\fabiy\source\repos\crms`

---

## ⚡ AI AGENT PROTOCOL — READ THIS FIRST

This document is designed to be updated after every session so it stays current for the next one.

### At the START of every session
1. Read this entire file
2. Read `docs/UIGaps.md`
3. Ask the user what they want to work on, or proceed with **Section 6 (Suggested Next Task)** if no instruction is given
4. Do NOT read other docs unless you specifically need them (they are listed in Section 9)

### At the END of every session (when a feature is complete OR when the user says "update handoff")

Update **this file** as follows — do not skip any step:

| Step | What to update | How |
|------|---------------|-----|
| 1 | **Section 2 — "What Works" table** | Move completed items from "What Is Pending" into "What Works" with ✅ |
| 2 | **Section 2 — "What Is Pending" table** | Remove completed items; add any newly discovered pending items |
| 3 | **Section 5 — "Last Session Summary"** | Replace the entire section with what was done this session: list each completed feature with the key files changed and any important implementation notes |
| 4 | **Section 6 — "Suggested Next Task"** | Update to the next logical feature. Include: which backend handlers already exist, which files to change, and what pattern to follow |
| 5 | **`Last Updated` date** in the header | Set to today's date |
| 6 | **Section 5 — "Docs Updated This Session"** | Use the mandatory checklist below — all three docs are always required; fill in the version numbers |

**Mandatory checklist — copy this exactly into Section 5 every session:**
```
### Docs Updated This Session
- [ ] `docs/SESSION_HANDOFF.md` → updated (this file)
- [ ] `docs/UIGaps.md` → vX.X
- [ ] `docs/ImplementationTracker.md` → vX.X
```
Replace `[ ]` with `[x]` for each doc you actually updated. If a doc was skipped, leave it unchecked and add a note explaining why.

Then update **`docs/UIGaps.md`**:
- Move completed features to ✅ in the relevant section
- Add newly discovered issues if any
- Add a row to the Changelog table at the bottom

Then update **`docs/ImplementationTracker.md`**:
- Bump the version number and date in the header
- Add a row to the Document History table at the bottom describing what was done

> **Platform note:** If you are an AI without direct file system access (e.g., web chat), output the full updated content of each file so the user can paste it back in. If you have file system access, edit the files directly.

---

## 1. What This System Is

A **Blazor Server** intranet application for bank staff to manage **corporate loan applications**. Uses **Clean Architecture / DDD**:

```
CRMS.Domain          → Aggregates, domain rules (no dependencies)
CRMS.Application     → Command/Query handlers, DTOs
CRMS.Infrastructure  → EF Core (MySQL), repositories, mock external services
CRMS.Web.Intranet    → Blazor Server UI (calls Application layer directly, no HTTP)
```

The Blazor UI calls `ApplicationService.cs` which resolves Application layer handlers via `IServiceProvider`. There are **no HTTP API calls** from the UI — everything is in-process.

---

## 2. Current Project State

**Backend:** 100% complete (16 modules). All Application layer commands/handlers exist and are registered.

**Intranet UI:** Core workflows complete. A few management features remain.

### What Works (as of 2026-03-14)

| Feature Area | Status |
|---|---|
| Create new application (auto-fetches details from core banking + directors from SmartComply CAC) | ✅ |
| Submit for review, workflow transitions (Approve / Return / Reject) | ✅ |
| Add / Edit / Delete / View Collateral | ✅ |
| Set Collateral Valuation (modal: market value, FSV, haircut %, live AcceptableValue) | ✅ |
| Approve Collateral (confirmation modal) | ✅ |
| Upload / View / Download / Delete Collateral Documents | ✅ |
| Add / Edit / Delete / View Guarantor | ✅ |
| Approve / Reject Guarantor (confirmation modal + reject with reason) | ✅ |
| Upload / View / Download Documents | ✅ |
| Verify Document (inline — no modal) | ✅ |
| Reject Document (modal with mandatory reason) | ✅ |
| Financial Statements (4-step manual entry, Excel upload, view / edit / delete) | ✅ |
| AI Advisory generation | ✅ |
| Committee voting | ✅ |
| Loan Pack PDF generation | ✅ |
| Workflow queue pages (My Queue, All Queues) | ✅ |
| Dashboard and Reports | ✅ |
| Credit Bureau UI (SmartComply) | ✅ |
| Bank Statement tab (view transactions drill-down) | ✅ |
| User management CRUD (Create / Edit / Activate / Deactivate) | ✅ |
| Product management (Create / Edit / Enable / Disable) | ✅ |
| **Scoring Config editor (`/admin/scoring`) — maker-checker, seed, all 9 categories** | ✅ |
| **Real Core Banking API integration (OAuth2, account details + transactions)** | ✅ |
| **Director discrepancy indicator (CBS vs SmartComply CAC comparison in New Application)** | ✅ |
| **AI Advisory data quality fixes (GAPs 1-3, 5, 7-8)** | ✅ |
| **Industry/Sector classification on loan applications** | ✅ |
| **Role-based workflow authorization aligned (UI ↔ Backend)** | ✅ |
| **Location hierarchy (HO/Region/Zone/Branch) + role-based visibility filtering** | ✅ |
| **Location/visibility bug fixes (8 bugs + 2 gaps fixed)** | ✅ |
| **Test users seeded with LocationId assignments** | ✅ |
| **Location CRUD Admin UI (`/admin/locations`) — tree view, create/edit/activate/deactivate** | ✅ |
| **User Admin location picker dropdown (replaces hardcoded branch list)** | ✅ |
| **Performance & Committee report pages wired to ReportingService** | ✅ |
| **M-3: RequestBureauReportCommand migrated to ISmartComplyProvider** | ✅ |
| **M-4: In-process concurrency lock in ProcessLoanCreditChecksCommand** | ✅ |
| **M-5: NonPerformingAccounts → DelinquentFacilities rename (10 files + migration)** | ✅ |
| **Product mock fallback removed from New.razor** | ✅ |
| **Application Detail tabs wired to real backend (Workflow, Advisory, Committee, Comments)** | ✅ |
| **DownloadDocumentAsync fully implemented (IFileStorageService)** | ✅ |
| **GetMyPendingTasksAsync fixed (Amount, ProductName populated)** | ✅ |
| **Collateral mapping fixed (ForcedSaleValue, LastValuationDate)** | ✅ |
| **Committee voting authorization guard (role-based)** | ✅ |
| **Committee setup UI (SetupCommitteeModal — create review + add members)** | ✅ |
| **Standing Committee admin (`/admin/committees`) — permanent roster, amount-based routing** | ✅ |
| **Automatic committee routing (amount → standing committee → auto-populate members)** | ✅ |
| **5 standing committees seeded (Branch/Regional/HO/Management/Board with NGN thresholds)** | ✅ |
| **Dashboard growth badges wired to real backend data** | ✅ |
| **Reports Index page wired to ReportingService (growth %, funnel, portfolio, SLA)** | ✅ |
| **Committee Reviews page votes progress wired to real data** | ✅ |
| **My Pending Votes page wired to real backend** | ✅ |
| **Overdue Queue page wired to real backend** | ✅ |
| **Export buttons disabled with "coming soon" tooltip across all report pages** | ✅ |
| **NavMenu badge counts wired to real backend (MyQueue, Overdue, PendingVotes)** | ✅ |
| **Overdue functionality bug fixes (5 bugs: hardcoded counts, inconsistent queries)** | ✅ |

### What Is Pending

| Feature | Priority | Notes |
|---------|----------|-------|
| Template management CRUD (`/admin/templates`) | P3 | Display only — no edit functionality |
| Guarantor credit check trigger UI | P3 | On hold pending provider decision |
| Bureau report detail modal (click to expand) | P3 | View detail for individual bureau report |

---

## 3. Critical Patterns — Follow These Exactly

### ApplicationService.cs — How to Call the Application Layer
```csharp
// Always resolve the handler from IServiceProvider, never inject directly
var handler = _sp.GetRequiredService<SomeCommandHandler>();
var result = await handler.Handle(new SomeCommand(...), CancellationToken.None);
return result.IsSuccess
    ? ApiResponse.Ok()
    : ApiResponse.Fail(result.Error ?? "Failed to do X");
```

### Adding a New Feature — Checklist
1. Check if the Application layer command/handler already exists (they almost always do)
2. Confirm handler is registered in `src/CRMS.Infrastructure/DependencyInjection.cs`
3. Add method to `ApplicationService.cs`
4. Create or update the Razor component (modal or tab)
5. Wire up in `Detail.razor`: add state variables, modal HTML block, and C# handler methods in `@code`

### Domain Status Values (UI receives these as strings)
- **Collateral:** `"Proposed"` → `"UnderValuation"` → `"Valued"` → `"Approved"` → `"Perfected"` → `"Released"` / `"Rejected"`
- **Guarantor:** `"Proposed"` → `"PendingVerification"` → `"CreditCheckPending"` → `"CreditCheckCompleted"` → `"Approved"` / `"Rejected"`
- **Document:** domain stores `"Uploaded"` → displayed as `"Pending"` in UI via `FormatStatus()` in `DocumentsTab.razor`
- **Application:** `"Draft"` → `"BranchReview"` → `"HOReview"` → `"CreditAnalysis"` → `"FinalApproval"` → `"Approved"` / `"Rejected"`

### Access Control Rules
- `IsApplicationEditable` = `application.Status == "Draft"` — data entry (add/edit/delete) only allowed in Draft
- `CanManageValuation` = status is NOT `Draft`, `Approved`, `CommitteeApproved`, `Rejected`, or `Disbursed` — valuation/approval happens during review stages
- **Directors** come from **SmartComply CAC** (primary source) — core banking also returns directors for discrepancy comparison only
- **Signatories** come from **core banking** (CBS `fulldetailsbynuban`)
- PartiesTab is intentionally read-only; null fields (BVN, shareholding %) can be filled via FillPartyInfoModal (Draft only)

### Blazor Modal Pattern (used consistently throughout Detail.razor)
```csharp
// State variables (in @code)
private bool showXyzModal;
private Guid? xyzTargetId;
private string? xyzError;
private bool isProcessingXyz;

// Show
private void ShowXyzModal(Guid id) { xyzTargetId = id; xyzError = null; showXyzModal = true; }

// Close
private void CloseXyzModal() { showXyzModal = false; xyzTargetId = null; xyzError = null; }

// Confirm
private async Task ConfirmXyz()
{
    if (xyzTargetId == null) return;
    isProcessingXyz = true; xyzError = null;
    try
    {
        var userId = AuthService.CurrentUser?.Id ?? Guid.Empty;
        var result = await AppService.XyzAsync(xyzTargetId.Value, userId);
        if (result.Success) { showXyzModal = false; await LoadApplication(); }
        else xyzError = result.Error ?? "Failed.";
    }
    finally { isProcessingXyz = false; }
}
```

---

## 4. Key File Locations

### Most Frequently Edited Files
| File | Purpose |
|---|---|
| `src/CRMS.Web.Intranet/Services/ApplicationService.cs` | All UI→backend calls (~1550 lines) |
| `src/CRMS.Web.Intranet/Services/ApplicationServiceDtos.cs` | DTOs used by service and modals |
| `src/CRMS.Web.Intranet/Components/Pages/Applications/Detail.razor` | Application detail page — all modal state, wiring, handlers (~1450 lines) |
| `src/CRMS.Infrastructure/DependencyInjection.cs` | Register new handlers here |

### Modals Directory
```
src/CRMS.Web.Intranet/Components/Pages/Applications/Modals/
├── AddCollateralModal.razor
├── EditCollateralModal.razor
├── ViewCollateralModal.razor              ← includes document list with view/download/delete
├── SetCollateralValuationModal.razor
├── UploadCollateralDocumentModal.razor
├── AddGuarantorModal.razor
├── EditGuarantorModal.razor
├── ViewGuarantorModal.razor
├── UploadDocumentModal.razor
├── FinancialStatementModal.razor
├── UploadFinancialStatementModal.razor
├── UploadExternalStatementModal.razor     ← upload other-bank statement
├── FillPartyInfoModal.razor               ← fill null BVN/shareholding for a party
└── SetupCommitteeModal.razor              ← auto-routes from standing committee or falls back to ad-hoc
```

### Tabs Directory
```
src/CRMS.Web.Intranet/Components/Pages/Applications/Tabs/
├── CollateralTab.razor       ← params: CanManageValuation, OnSetValuation, OnApproveCollateral, OnUploadDocument
├── DocumentsTab.razor        ← params: OnVerifyDocument, OnRejectDocument
├── GuarantorsTab.razor       ← params: CanManageGuarantors, OnApproveGuarantor, OnRejectGuarantor
├── FinancialsTab.razor
├── StatementsTab.razor       ← NEW: Own Bank + Other Banks; trust badges; verify/reject/analyze
├── PartiesTab.razor          ← params: IsEditable, OnRequestBureauCheck, OnFillPartyInfo
└── BureauTab.razor
```

### Application Layer — Check These Before Writing Any New Code
```
src/CRMS.Application/
├── LoanApplication/Commands/UploadDocumentCommand.cs    ← Verify + RejectDocumentHandler
├── Collateral/Commands/CollateralCommands.cs            ← SetValuation + ApproveCollateralHandler
├── Guarantor/Commands/GuarantorCommands.cs              ← ApproveGuarantorHandler, RejectGuarantorHandler
├── Workflow/Commands/TransitionWorkflowCommand.cs
└── ...
```

---

## 5. Last Session Summary (2026-03-18 Session 23)

### Completed — Overdue Functionality Bug Fix (Comprehensive Review)

User reported: NavMenu shows "2" badge for Overdue, but clicking shows empty list. Performed comprehensive code review and found 5 bugs.

#### Bugs Identified

| Bug | Location | Issue |
|-----|----------|-------|
| **BUG-1** | `NavMenu.razor` line 145 | `OverdueCount`, `MyQueueCount`, `PendingVotesCount` were **hardcoded** (2, 5, 1) — never fetched from backend |
| **BUG-2** | `ReportingService.cs` vs `WorkflowRepositories.cs` | Different query conditions: ReportingService used `IsSLABreached` flag, repository used `SLADueAt < now` |
| **BUG-3** | `WorkflowInstance` | `IsSLABreached` flag only set when `CheckAndMarkSLABreachesAsync()` runs |
| **BUG-4** | Entire codebase | `CheckAndMarkSLABreachesAsync()` is **never called** — no background job exists |
| **BUG-5** | `NavMenu.razor` | No `OnInitializedAsync` — counts never loaded |

#### Fixes Applied

**1. NavMenu.razor — Wire to Real Backend**
- Added `@inject ApplicationService AppService`
- Added `OnInitializedAsync()` that calls `LoadCounts()`
- Loads `MyQueueCount`, `OverdueCount`, `PendingVotesCount` in parallel
- Removed hardcoded values

**2. ApplicationService.cs — Added 3 Count Methods**
```csharp
public async Task<int> GetOverdueCountAsync()
public async Task<int> GetMyQueueCountAsync(Guid userId)
public async Task<int> GetMyPendingVotesCountAsync(Guid userId)
```

**3. ReportingService.cs — Aligned Overdue Query**
- Changed from: `IsSLABreached && CompletedAt == null`
- Changed to: `!IsCompleted && SLADueAt.HasValue && SLADueAt < now`
- Now consistent with `GetOverdueSLAAsync()` in repository

**Build:** 0 errors, 20 warnings (pre-existing). **Tests:** All pass.

### Files Modified This Session
- `src/CRMS.Web.Intranet/Components/Layout/NavMenu.razor` — Real counts from backend
- `src/CRMS.Web.Intranet/Services/ApplicationService.cs` — 3 new count methods
- `src/CRMS.Infrastructure/Services/ReportingService.cs` — Aligned overdue query

### Note
The `IsSLABreached` flag is still never set (no background job). However, this is now irrelevant because both NavMenu and Overdue page use `SLADueAt < now` consistently. If you want the flag set for audit purposes, add a background job calling `CheckAndMarkSLABreachesAsync()`.

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v4.2
- [ ] `docs/ImplementationTracker.md` → not updated (bug fix only, no new features)

---

## 5. Previous Session Summary (2026-03-18 Session 22)

### Completed — UI Wiring Audit: Mock Data Removal + Real Backend Integration

Continued from Session 21's comprehensive UI wiring audit. Fixed all remaining pages that were displaying hardcoded mock data instead of real backend data. All report/queue pages now call the backend handlers.

#### 1. Critical Fixes (C-1, C-2, C-3)

**C-1: Dashboard RecentActivities & ApplicationsByStatus**
- `GetDashboardSummaryAsync()` in `ApplicationService.cs` now populates `ApplicationsByStatus` from real loan funnel data
- `RecentActivities` fetched from audit logs via `GetAuditLogsAsync()`

**C-2: My Pending Votes Page**
- Updated `MyVotes.razor` to call `AppService.GetMyPendingVotesAsync(userId)` instead of mock data
- Updated `GetMyPendingVotesAsync()` to populate `VotesCast`, `TotalMembers`, `Amount`, and `CustomerName`
- Added `VotesCast`, `TotalMembers`, `Amount` fields to `CommitteeReviewSummary` DTO

**C-3: Overdue Queue Page**
- Added `GetOverdueWorkflowsAsync()` method to `ApplicationService.cs` calling `GetOverdueWorkflowsHandler`
- Added `OverdueWorkflowItem` DTO
- Updated `Overdue.razor` to use real backend data

#### 2. High Priority Fixes (H-1 to H-6)

**Reports Index Page Wiring**
- Added `GetEnhancedReportingDataAsync(int periodDays)` fetching:
  - Growth percentages (current vs previous period calculation)
  - Application funnel from `IReportingService.GetLoanFunnelAsync()`
  - Portfolio by product from `IReportingService.GetPortfolioSummaryAsync()`
  - SLA compliance from `IReportingService.GetSLAReportAsync()`
- Added `EnhancedReportingData`, `FunnelStageData`, `ProductPortfolioData` DTOs
- Updated `Reports/Index.razor` with period selector that triggers data reload

**Export Buttons**
- Disabled all export buttons across report pages with "coming soon" tooltip

#### 3. Medium Priority Fixes (M-3, M-4)

**M-3: Dashboard Growth Badges**
- Added `ApplicationsGrowthPercent` and `ApprovalRateChange` to `DashboardSummary` model
- `Dashboard/Index.razor` now shows real growth percentage with dynamic positive/negative styling
- Approval rate calculated from real approved/rejected counts

**M-4: Committee Reviews Votes Progress**
- Updated `GetCommitteeReviewsByStatusAsync()` to populate `VotesCast`, `TotalMembers`, `Amount`, `CustomerName`
- `Reviews.razor` now shows real vote progress bars

#### 4. Low Priority Fixes (L-1)

**Export Buttons Disabled**
- `Reports/Performance.razor`, `Reports/Committee.razor`, `Reports/Audit.razor` - all export buttons disabled with tooltip

**Build:** 0 errors, 17 warnings (pre-existing). **Tests:** All pass.

---

## 5. Previous Session Summary (2026-03-18 Session 21)

### Completed — Standing Committee Infrastructure + Amount-Based Automatic Routing

Replaced the ad-hoc per-application committee assignment with a proper standing committee system. Committees are now pre-configured at the institutional level with permanent member rosters and amount-based routing thresholds, matching standard Nigerian banking practice.

#### 1. Domain Layer — StandingCommittee Aggregate

- **`StandingCommittee.cs`** (new) — Aggregate with: `Name`, `CommitteeType`, `RequiredVotes`, `MinimumApprovalVotes`, `DefaultDeadlineHours`, `MinAmountThreshold`, `MaxAmountThreshold`, `IsActive`, and child `StandingCommitteeMember` entities (UserId, UserName, Role, IsChairperson)
- Domain methods: `Create`, `Update`, `AddMember`, `RemoveMember`, `UpdateMember`, `Activate`, `Deactivate`
- Chairperson is exclusive — setting a new one automatically clears the previous

- **`IStandingCommitteeRepository.cs`** (new) — 6 methods including `GetForAmountAsync(amount)` for automatic routing

#### 2. Infrastructure Layer

- **`StandingCommitteeConfiguration.cs`** (new) — EF config for `StandingCommittees` + `StandingCommitteeMembers` tables; unique index on `CommitteeType`; composite unique on `(StandingCommitteeId, UserId)`
- **`StandingCommitteeRepository.cs`** (new) — Implements `GetForAmountAsync` with `WHERE IsActive AND MinAmount <= amount AND (MaxAmount IS NULL OR MaxAmount >= amount) ORDER BY MinAmount DESC`
- **`CRMSDbContext.cs`** — Added `StandingCommittees` and `StandingCommitteeMembers` DbSets
- **`DependencyInjection.cs`** — Registered `IStandingCommitteeRepository`, 7 standing committee handlers
- **Migration `20260318120000_AddStandingCommittees`** — Creates both tables with indexes

#### 3. Application Layer

- **`StandingCommitteeDtos.cs`** (new) — `StandingCommitteeDto`, `StandingCommitteeMemberDto`
- **`StandingCommitteeCommands.cs`** (new) — 5 commands+handlers: `CreateStandingCommittee`, `UpdateStandingCommittee`, `ToggleStandingCommittee`, `AddStandingCommitteeMember`, `RemoveStandingCommitteeMember`
- **`StandingCommitteeQueries.cs`** (new) — 2 queries: `GetAllStandingCommittees`, `GetStandingCommitteeForAmount`

#### 4. Web.Intranet — Admin UI + Automatic Routing

- **`Committees.razor`** (new, `/admin/committees`) — Full admin page:
  - Card-per-committee layout with type badge, amount range, quorum rules, deadline, member table
  - Create/edit committee modal (name, type, amount range, votes, deadline)
  - Add/remove members with role and chairperson designation
  - Activate/deactivate toggle
- **`NavMenu.razor`** — Added "Committees" link under Administration
- **`ApplicationService.cs`** — 8 new methods: `GetStandingCommitteesAsync`, `CreateStandingCommitteeAsync`, `UpdateStandingCommitteeAsync`, `ToggleStandingCommitteeAsync`, `AddStandingCommitteeMemberAsync`, `RemoveStandingCommitteeMemberAsync`, `GetStandingCommitteeForAmountAsync`
- **`ApplicationModels.cs`** — Added `StandingCommitteeInfo`, `StandingMemberInfo` DTOs

#### 5. Automatic Routing — Refactored SetupCommitteeModal

- `SetupCommitteeModal.razor` completely rewritten to support two modes:
  - **Auto-routed** — On open, calls `GetStandingCommitteeForAmountAsync(loanAmount)`. If a standing committee matches, shows green banner with pre-populated committee config and member roster. One-click to create the review.
  - **Ad-hoc fallback** — If no standing committee matches, shows warning and manual setup (same as before)
- `Detail.razor` now passes `LoanAmount="application.Loan.RequestedAmount"` to the modal

#### 6. Seed Data

5 standing committees with Nigerian banking standard thresholds:

| Committee | Amount Range | Required/Min Approval | Deadline |
|-----------|-------------|----------------------|----------|
| Branch Credit | N0 — N50M | 3/2 | 48h |
| Regional Credit | N50M — N200M | 3/2 | 72h |
| Head Office Credit | N200M — N500M | 5/3 | 72h |
| Management Credit | N500M — N2B | 5/4 | 120h |
| Board Credit | N2B+ | 7/5 | 168h |

**Build:** 0 errors, 19 warnings (all pre-existing). **Tests:** All pass (2/2).

### Files Created This Session
- `src/CRMS.Domain/Aggregates/Committee/StandingCommittee.cs`
- `src/CRMS.Domain/Interfaces/IStandingCommitteeRepository.cs`
- `src/CRMS.Application/Committee/DTOs/StandingCommitteeDtos.cs`
- `src/CRMS.Application/Committee/Commands/StandingCommitteeCommands.cs`
- `src/CRMS.Application/Committee/Queries/StandingCommitteeQueries.cs`
- `src/CRMS.Infrastructure/Persistence/Configurations/Committee/StandingCommitteeConfiguration.cs`
- `src/CRMS.Infrastructure/Persistence/Repositories/Committee/StandingCommitteeRepository.cs`
- `src/CRMS.Infrastructure/Persistence/Migrations/20260318120000_AddStandingCommittees.cs`
- `src/CRMS.Web.Intranet/Components/Pages/Admin/Committees.razor`

### Files Modified This Session
- `src/CRMS.Web.Intranet/Services/ApplicationService.cs` — 8 new standing committee methods
- `src/CRMS.Web.Intranet/Models/ApplicationModels.cs` — `StandingCommitteeInfo`, `StandingMemberInfo`
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Modals/SetupCommitteeModal.razor` — Rewritten with auto-route + ad-hoc fallback
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Detail.razor` — Passes `LoanAmount` to modal
- `src/CRMS.Web.Intranet/Components/Layout/NavMenu.razor` — Added Committees nav link
- `src/CRMS.Infrastructure/Persistence/CRMSDbContext.cs` — Added `StandingCommittees`, `StandingCommitteeMembers` DbSets
- `src/CRMS.Infrastructure/DependencyInjection.cs` — 8 new registrations (repository + 7 handlers)
- `src/CRMS.Infrastructure/Persistence/SeedData.cs` — Added `SeedStandingCommitteesAsync()`

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v4.1
- [x] `docs/ImplementationTracker.md` → v4.6

---

## 5. Previous Session Summary (2026-03-18 Session 20)

### Completed — Comprehensive UI Wiring Audit + Critical Fixes + Committee Setup UI

This session performed a full code audit of the intranet UI and ApplicationService, discovered significant gaps hidden by mock data fallbacks, and fixed all critical issues. Also built the missing Committee Review setup workflow. See Session 20 details for full implementation notes.

**Build:** 0 errors, 19 warnings (all pre-existing). **Tests:** All pass (2/2).

---

## 5. Previous Session Summary (2026-03-16 Session 19)

### Completed — Location CRUD Admin UI + User Location Picker

Implemented the Location Management admin page (`/admin/locations`) with full CRUD functionality and updated the User Admin page with a dynamic location picker dropdown. See Session 19 details for full implementation notes.

**Build:** 0 errors, 16 warnings (pre-existing). **Tests:** All 4 pass.

---

## 5. Previous Session Summary (2026-03-16 Session 18)

### Completed — Location/Visibility Bug Fixes (8 Bugs + 2 Gaps)

Fixed all identified bugs and gaps in the location hierarchy and visibility filtering system implemented in Session 17.

#### BUG-1: AuthService.cs — UserInfo.LocationId never populated after login
- **File:** `src/CRMS.Web.Intranet/Services/AuthService.cs`
- **Fix:** Added `LocationId = appUser.LocationId` and `LocationName = appUser.LocationName` mapping in `LoginAsync()`

#### BUG-2: ApplicationService.cs — New applications created with BranchId = null
- **Files:** `src/CRMS.Web.Intranet/Services/ApplicationService.cs`, `src/CRMS.Web.Intranet/Components/Pages/Applications/New.razor`
- **Fix:** Added optional `userLocationId` parameter to `CreateApplicationAsync()`; `New.razor` now passes `AuthService.CurrentUser?.LocationId`

#### BUG-3: UserDto.BranchId → LocationId rename across auth chain
- **Files:** `AuthDtos.cs`, `AuthService.cs` (Infrastructure), `RegisterUserCommand.cs`, `GetUserQuery.cs`, `UpdateUserCommand.cs`
- **Fix:** Renamed `UserDto.BranchId` → `LocationId`, added `LocationName` field; updated all 5 files that construct `UserDto`

#### BUG-4: LocationRepository.GetHierarchyTreeAsync returns empty children
- **Files:** `src/CRMS.Domain/Aggregates/Location/Location.cs`, `src/CRMS.Infrastructure/Persistence/Repositories/Location/LocationRepository.cs`
- **Fix:** Added `Location.AddChild()` public method; `GetHierarchyTreeAsync()` now builds parent-child relationships in-memory using dictionary lookup

#### BUG-5: UpdateUserCommand missing LocationId field
- **Files:** `src/CRMS.Application/Identity/Commands/UpdateUserCommand.cs`, `src/CRMS.Web.Intranet/Services/ApplicationService.cs`, `src/CRMS.Web.Intranet/Components/Pages/Admin/Users.razor`
- **Fix:** Added `LocationId` parameter to command; handler calls `user.SetLocation()`; updated service and UI

#### BUG-6: LoanApplicationsController.GetPendingBranchReview not using visibility
- **File:** `src/CRMS.API/Controllers/LoanApplicationsController.cs`
- **Fix:** Extracts `LocationId` and `Role` from JWT claims; passes to `GetPendingBranchReviewQuery`

#### BUG-7: GetPendingBranchReviewHandler missing from Infrastructure DI
- **File:** `src/CRMS.Infrastructure/DependencyInjection.cs`
- **Fix:** Added `services.AddScoped<GetPendingBranchReviewHandler>()`

#### BUG-8: VisibilityService return value ambiguity (empty list = two meanings)
- **File:** `src/CRMS.Domain/Services/VisibilityService.cs`
- **Fix:** Added extensive XML documentation explaining the semantic difference between empty list for "Own" scope vs "no access"

#### GAP-2: No seeded users assigned to locations
- **Files:** `src/CRMS.Infrastructure/Persistence/SeedData.cs`, `Program.cs` (Web.Intranet + API), `SeedController.cs`
- **Fix:** Added `SeedTestUsersAsync()` creating 6 test users with locations:
  - `loanofficer@crms.test` (Lagos Main Branch)
  - `branchapprover@crms.test` (Lagos Main Branch)
  - `loanofficer.abuja@crms.test` (Abuja Main Branch)
  - `creditofficer@crms.test` (Head Office)
  - `horeviewer@crms.test` (Head Office)
  - `admin@crms.test` (Head Office)
- Default password: `Test@123`

#### GAP-5: North-East zone (ZN-NE) has no branches
- **File:** `src/CRMS.Infrastructure/Persistence/SeedData.cs`
- **Fix:** Added 2 branches: Maiduguri Branch (BR-MAI-001) and Bauchi Branch (BR-BAU-001)

**Build:** 0 errors, 19 warnings (pre-existing). **Tests:** All 5 pass.

### Files Modified This Session
- `src/CRMS.Web.Intranet/Services/AuthService.cs` — LocationId/LocationName mapping
- `src/CRMS.Web.Intranet/Services/ApplicationService.cs` — userLocationId param on CreateApplicationAsync, UpdateUserAsync
- `src/CRMS.Web.Intranet/Components/Pages/Applications/New.razor` — passes userLocationId
- `src/CRMS.Web.Intranet/Components/Pages/Admin/Users.razor` — passes locationId to UpdateUserAsync
- `src/CRMS.Application/Identity/DTOs/AuthDtos.cs` — BranchId→LocationId, added LocationName
- `src/CRMS.Application/Identity/Commands/RegisterUserCommand.cs` — BranchId→LocationId
- `src/CRMS.Application/Identity/Commands/UpdateUserCommand.cs` — added LocationId param + SetLocation()
- `src/CRMS.Application/Identity/Queries/GetUserQuery.cs` — LocationId/LocationName in UserDto
- `src/CRMS.Infrastructure/Identity/AuthService.cs` — LocationId/LocationName in UserDto
- `src/CRMS.Infrastructure/DependencyInjection.cs` — registered GetPendingBranchReviewHandler
- `src/CRMS.Infrastructure/Persistence/SeedData.cs` — SeedTestUsersAsync(), NE zone branches
- `src/CRMS.Infrastructure/Persistence/Repositories/Location/LocationRepository.cs` — tree-building in GetHierarchyTreeAsync
- `src/CRMS.Domain/Aggregates/Location/Location.cs` — AddChild() method
- `src/CRMS.Domain/Services/VisibilityService.cs` — documentation for return value ambiguity
- `src/CRMS.API/Controllers/LoanApplicationsController.cs` — visibility params from JWT
- `src/CRMS.API/Controllers/SeedController.cs` — passes passwordHasher to SeedAsync
- `src/CRMS.API/Program.cs` — passes passwordHasher to SeedAsync
- `src/CRMS.Web.Intranet/Program.cs` — passes passwordHasher to SeedAsync

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v3.8
- [x] `docs/ImplementationTracker.md` → v4.3

---

## 5. Previous Session Summary (2026-03-16 Session 17)

### Completed — Location Hierarchy + Role-Based Visibility Filtering

Implemented a complete 4-level location hierarchy (HeadOffice → Region → Zone → Branch) with role-based visibility filtering so users only see loan applications within their organizational scope.

#### Domain Layer

- **`Location.cs`** (new aggregate) — Self-referencing hierarchy with `LocationType` enum (HeadOffice/Region/Zone/Branch). Factory methods: `CreateHeadOffice`, `CreateRegion`, `CreateZone`, `CreateBranch`. Domain methods: `Update`, `Activate`, `Deactivate`, `ValidateParentType`.
- **`VisibilityScope.cs`** (new enum) — Own, Branch, Zone, Region, Global
- **`Roles.cs`** — Added `RoleVisibilityScopes` dictionary (11 roles mapped), `GetVisibilityScope()`, `HasGlobalVisibility()` helpers. Branch roles: LoanOfficer, BranchApprover. Global roles: CreditOfficer, HOReviewer, CommitteeMember, FinalApprover, Operations, RiskManager, Auditor, SystemAdmin. Own: Customer.
- **`ApplicationUser.cs`** — Replaced `BranchId` with `LocationId` + `Location` navigation property. Deprecated `BranchId` property for backward compatibility. Added `SetLocation()` method.
- **`ILocationRepository.cs`** (new interface) — 13 methods including `GetDescendantBranchIdsAsync()`, `GetAncestorIdsAsync()`, `GetHierarchyTreeAsync()`.
- **`VisibilityService.cs`** (new domain service) — `GetVisibleBranchIdsAsync()` returns `null` for global (no filter), `[]` for own (filter by user), or branch GUID list for scoped visibility. `CanAccessApplicationAsync()` for single-application access checks.

#### Infrastructure Layer

- **`LocationConfiguration.cs`** (new EF config) — Self-referencing FK with Restrict delete, 5 indexes (Code unique, Type, ParentLocationId, IsActive, composite Type+IsActive).
- **`LocationRepository.cs`** (new) — Full hierarchy traversal: zone→branches, region→zones→branches, HO→all branches. Recursive ancestor lookup.
- **`ApplicationUserConfiguration.cs`** — Added Location FK (SetNull on delete) + LocationId index.
- **`CRMSDbContext.cs`** — Added `Locations` DbSet.
- **`SeedData.cs`** — `SeedLocationsAsync()` creates Nigeria banking geography: 1 HO, 2 Regions (Southern/Northern), 6 Zones (SW/SE/SS/NC/NW/NE), 12 Branches (Lagos×4, Ibadan, PH, Enugu, Benin, Abuja×2, Kano, Kaduna).
- **`DependencyInjection.cs`** — Registered `ILocationRepository` → `LocationRepository`, `VisibilityService`.
- **Migration `20260316164251_AddLocationHierarchy`** — Creates `Locations` table, renames `Users.BranchId` → `LocationId`, adds FK with SetNull delete.

#### Application Layer — Visibility Filtering

- **`ILoanApplicationRepository.cs`** — Added `GetByStatusFilteredAsync(status, visibleBranchIds)` and `GetPendingBranchReviewFilteredAsync(visibleBranchIds)`.
- **`LoanApplicationRepository.cs`** — Implemented both filtered methods (null = no filter, list = filter by BranchId IN list).
- **`GetLoanApplicationQuery.cs`** — `GetLoanApplicationsByStatusQuery` now accepts `UserLocationId`, `UserRole`, `UserId`. Handler uses `VisibilityService` to filter: Global roles see all, Own scope filters by initiator, Branch/Zone/Region scopes filter by descendant branch IDs. Backward-compatible when no role info provided.
- **`GetPendingBranchReviewQuery`** — Same pattern: accepts `UserLocationId`, `UserRole`; uses `VisibilityService` for filtering.

#### Web.Intranet Layer

- **`AuthModels.cs`** — `UserInfo` now has `LocationId` (Guid?), `LocationName`, `PrimaryRole`. `BranchId`/`BranchName` properties retained as computed backward-compatibility shims.
- **`ApplicationService.cs`** — `GetApplicationsByStatusAsync` now has a visibility-aware overload accepting `userLocationId`, `userRole`, `userId`.
- **`Applications/Index.razor`** — Passes `user.LocationId`, `user.PrimaryRole`, `user.Id` to status query.

**Build:** 0 errors. **Tests:** All pass (4/4).

### Files Created This Session
- `src/CRMS.Domain/Aggregates/Location/Location.cs`
- `src/CRMS.Domain/Enums/VisibilityScope.cs`
- `src/CRMS.Domain/Interfaces/ILocationRepository.cs`
- `src/CRMS.Domain/Services/VisibilityService.cs`
- `src/CRMS.Infrastructure/Persistence/Configurations/Location/LocationConfiguration.cs`
- `src/CRMS.Infrastructure/Persistence/Repositories/Location/LocationRepository.cs`
- `src/CRMS.Infrastructure/Persistence/Migrations/20260316164251_AddLocationHierarchy.cs`
- `src/CRMS.Infrastructure/Persistence/Migrations/20260316164251_AddLocationHierarchy.Designer.cs`

### Files Modified This Session
- `src/CRMS.Domain/Constants/Roles.cs` — Added `RoleVisibilityScopes`, `GetVisibilityScope()`, `HasGlobalVisibility()`
- `src/CRMS.Domain/Entities/Identity/ApplicationUser.cs` — `LocationId` replaces `BranchId`, `Location` nav prop, `SetLocation()`
- `src/CRMS.Domain/Interfaces/ILoanApplicationRepository.cs` — Added 2 filtered query methods
- `src/CRMS.Application/LoanApplication/Queries/GetLoanApplicationQuery.cs` — Visibility-aware handlers
- `src/CRMS.Infrastructure/Persistence/CRMSDbContext.cs` — `Locations` DbSet
- `src/CRMS.Infrastructure/Persistence/Configurations/Identity/ApplicationUserConfiguration.cs` — Location FK
- `src/CRMS.Infrastructure/Persistence/Repositories/LoanApplicationRepository.cs` — 2 filtered methods
- `src/CRMS.Infrastructure/Persistence/SeedData.cs` — `SeedLocationsAsync()`
- `src/CRMS.Infrastructure/DependencyInjection.cs` — Registered LocationRepository + VisibilityService
- `src/CRMS.Infrastructure/Persistence/Migrations/CRMSDbContextModelSnapshot.cs` — Updated
- `src/CRMS.Web.Intranet/Models/AuthModels.cs` — `UserInfo.LocationId`, `PrimaryRole`
- `src/CRMS.Web.Intranet/Services/ApplicationService.cs` — Visibility-aware `GetApplicationsByStatusAsync` overload
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Index.razor` — Passes visibility context

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v3.7
- [x] `docs/ImplementationTracker.md` → v4.2

---

## 5. Previous Session Summary (2026-03-16 Session 16)

### Completed — Role-Based Workflow Authorization Alignment

Fixed 4 UI authorization issues in `Detail.razor` where button visibility did not match backend workflow definitions: HOReview now checks CreditOfficer, Return/Reject buttons have per-status role checks, CommitteeCirculation added for CommitteeMember, FinalApproval corrected to CommitteeApproved for FinalApprover.

---

## 5. Previous Session Summary (2026-03-16 Session 15)

### Completed — Real Core Banking API Integration + Director Discrepancy Indicator

Replaced the mock-only core banking layer with a real CBS API client matching the bank's actual API, and aligned the mock to reflect real API constraints. Added a director discrepancy comparison UI. See Session 15 details for full implementation notes.

**Build:** 0 errors.

---

## 5. Previous Session Summary (2026-03-14 Session 14)

### Completed — Scoring Config Editor UI (`/admin/scoring`)

The scoring configuration page was display-only with hardcoded data. Replaced with a fully functional maker-checker editor wired to the real backend. See previous handoff for full details.

**Build:** 0 errors.

---

## 5. Previous Session Summary (2026-03-13 Session 13)

### Completed — AI Advisory Bureau Data Fix + Scoring Config Alignment

Two related gaps fixed in this session.

#### 1. AI Advisory Now Uses Real Bureau Data

Previously, `GenerateCreditAdvisoryHandler.BuildAIRequest()` created placeholder `BureauDataInput` objects (all-zeros, random GUIDs) for every director and signatory. The actual `BureauReport` table — populated by `ProcessLoanCreditChecksCommand` after branch approval — was never queried.

- **`GenerateCreditAdvisoryCommand.cs`**:
  - Injected `IBureauReportRepository`
  - `BuildAIRequest()` now calls `GetByLoanApplicationIdAsync(loanApp.Id)` and indexes completed reports by `PartyId`
  - For each party in `loanApp.Parties`, finds matching `BureauReport` by `PartyId` → builds real `BureauDataInput`
  - Falls back to a flagged placeholder (`IsPlaceholder = true`) when no bureau report exists for a party, so the AI model knows the gap
  - Also picks up the corporate/business bureau report (`SubjectType.Business`) and adds it as a `"Corporate"` entry
  - Added `MapBureauReport()` private helper — maps: `CreditScore`, `ActiveLoans`, `TotalOutstandingBalance`, `PerformingAccounts`, `NonPerformingAccounts`, `MaxDelinquencyDays`, `HasLegalActions`, `TotalOverdue`, `FraudRiskScore`, `FraudRecommendation`, `ReportDate`; derives `WorstStatus` from `MaxDelinquencyDays`

- **`IAIAdvisoryService.cs`** — `BureauDataInput` extended with 6 new fields:
  - `MaxDelinquencyDays`, `HasLegalActions`, `TotalOverdue`, `FraudRiskScore`, `FraudRecommendation`
  - `IsPlaceholder` — flags entries with no actual bureau data

#### 2. New Bureau Scoring Thresholds Added to Scoring Config (Admin-Editable)

The new `MockAIAdvisoryService` scoring logic initially had hardcoded penalty values. These were moved to the scoring configuration so admins can tune them.

- **`ScoringConfiguration.cs`** — Added 10 new fields to `CreditHistoryConfig`:
  - `LegalActionsPenalty` (default 20)
  - `SevereDelinquencyDaysThreshold` / `SevereDelinquencyPenalty` (90 days / 15pts)
  - `WatchListDaysThreshold` / `WatchListPenalty` (30 days / 8pts)
  - `HighFraudRiskScoreThreshold` / `HighFraudRiskPenalty` (score ≥70 / 25pts)
  - `ElevatedFraudRiskScoreThreshold` / `ElevatedFraudRiskPenalty` (score ≥50 / 10pts)
  - `MissingBureauDataPenaltyPerParty` (5pts per missing party)

- **`ScoringConfigurationService.cs`** — Added 10 corresponding `GetValue()` calls to load each new field from DB (under `CreditHistory` category key), with defaults matching the config class.

- **`MockAIAdvisoryService.cs`** — `CalculateCreditHistoryScore()` updated:
  - All new penalties now use `cfg.FieldName` instead of hardcoded constants
  - Scoring rationale string now includes delinquency days, legal action status, fraud score, and real vs placeholder report count

**Build:** 0 errors.

### Files Updated This Session
- `src/CRMS.Application/Advisory/Interfaces/IAIAdvisoryService.cs`
- `src/CRMS.Application/Advisory/Commands/GenerateCreditAdvisoryCommand.cs`
- `src/CRMS.Domain/Configuration/ScoringConfiguration.cs`
- `src/CRMS.Domain/Services/ScoringConfigurationService.cs`
- `src/CRMS.Infrastructure/ExternalServices/AIServices/MockAIAdvisoryService.cs`

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [ ] `docs/UIGaps.md` → no UI changes this session
- [x] `docs/ImplementationTracker.md` → v3.7

---

## 5. Previous Session Summary (2026-03-13 Session 12)

### Completed — SmartComply CAC Advanced Data Structure Fix + New Application Flow Redesign

#### 1. SmartComply CAC Advanced DTOs (User's Primary Fix)

- **`SmartComplyDtos.cs`**: Added complete CAC Advanced response DTOs matching the actual API:
  - `CacAdvancedData` — company-level fields: `CompanyName`, `RcNumber`, `CompanyId`, `EntityType`, `CompanyStatus`, `CompanyAddress`, `EmailAddress`, `RegistrationDate`, `City`, `State`, `Lga`, `BranchAddress`, `SearchScore`, `Directors[]`
  - `CacAdvancedDirectorData` — full director fields: `Id`, `Surname`, `Firstname`, `OtherName`, `Gender`, `Status`, `Address`, `City`, `State`, `Lga`, `Email`, `PhoneNumber`, `Occupation`, `Nationality`, `IdentityNumber`, `DateOfBirth`, `IsChairman`, `IsCorporate`, `IsDesignated`, `TypeOfShares`, `NumSharesAlloted`, `DateOfAppointment`, and all former-name fields
  - Nested classes: `CacCountryReference`, `CacAffiliateTypeReference`, `CacPscInformation`, `CacResidentialAddress`

- **`ISmartComplyProvider.cs`** — Enriched domain records:
  - `SmartComplyCacResult`: added `CompanyId` field
  - `SmartComplyCacDirector`: replaced 3-field record with 24-field record (`Id`, `Surname`, `FirstName`, `OtherName`, `FullName`, `Gender`, `DateOfBirth`, `Nationality`, `Occupation`, `Email`, `PhoneNumber`, `Address`, `City`, `State`, `Lga`, `Status`, `IsChairman`, `IsCorporate`, `DateOfAppointment`, `AffiliateType`, `TypeOfShares`, `NumSharesAlloted`, `IdentityNumber`, `Country`)

- **`SmartComplyProvider.cs`** — Split `GetCacVerificationAsync` into two separate methods:
  - `VerifyCacAsync` → uses `CacVerificationData` (basic endpoint, unchanged structure)
  - `VerifyCacAdvancedAsync` → uses `CacAdvancedData` (advanced endpoint, full structure)
  - Added `MapCacAdvancedToResult()` and `MapCacAdvancedDirector()` helpers

- **`MockSmartComplyProvider.cs`** — Updated mock to return fully populated `SmartComplyCacDirector` objects with shares, IsChairman, AffiliateType, DateOfAppointment, etc.

#### 2. New Application Flow — Directors from SmartComply CAC

**New flow:** Core banking → account name + signatories only. RC number always editable. SmartComply CAC Advanced → directors list. Data entry fills BVN for each director and any signatory without BVN.

- **`ApplicationModels.cs`**:
  - Added `DirectorInput` — UI model for a director with user-entered BVN
  - Added `SignatoryInput` — UI model for a signatory with user-entered BVN
  - Added `CacLookupResult` — SmartComply CAC Advanced result for New.razor
  - Added `CacDirectorEntry` — one director row with `BvnInput` binding
  - Added `Signatories` list to `CustomerInfo` model
  - Updated `CreateApplicationRequest` to carry `Directors` and `Signatories` lists

- **`ApplicationService.cs`**:
  - `FetchCorporateDataAsync`: now fetches signatories from core banking and includes them in the response; RC number left blank (user always enters it)
  - `FetchCacDirectorsAsync(rcNumber)` (NEW): calls `ISmartComplyProvider.VerifyCacAdvancedAsync` and returns a `CacLookupResult` with all directors
  - `CreateApplicationAsync`: maps `request.Directors` → `CmdNs.DirectorInput` records and `request.Signatories` → `CmdNs.SignatoryInput` records, passes them to the command

- **`InitiateCorporateLoanCommand.cs`**:
  - Added `DirectorInput` and `SignatoryInput` command-layer records
  - Added `Directors` and `Signatories` optional params to the command
  - Handler uses passed-in directors/signatories when provided; falls back to core banking calls when not (legacy compatibility)

- **`New.razor`** — Restructured Step 1:
  - RC number field is now **always shown and always editable** (not conditional on empty)
  - "Fetch Directors" button calls `FetchCacDirectorsAsync` and shows CAC company confirmation banner
  - Directors from SmartComply displayed in cards with BVN input per director
  - Signatories from core banking displayed with BVN input (disabled if already on file, editable if missing)
  - `CanProceed` step 1 = customer loaded AND RC number entered
  - `CreateApplication` packs directors (with BVNs) and signatories into the request

### Files Updated This Session
- `src/CRMS.Infrastructure/ExternalServices/SmartComply/SmartComplyDtos.cs`
- `src/CRMS.Domain/Interfaces/ISmartComplyProvider.cs`
- `src/CRMS.Infrastructure/ExternalServices/SmartComply/SmartComplyProvider.cs`
- `src/CRMS.Infrastructure/ExternalServices/SmartComply/MockSmartComplyProvider.cs`
- `src/CRMS.Web.Intranet/Models/ApplicationModels.cs`
- `src/CRMS.Web.Intranet/Services/ApplicationService.cs`
- `src/CRMS.Application/LoanApplication/Commands/InitiateCorporateLoanCommand.cs`
- `src/CRMS.Web.Intranet/Components/Pages/Applications/New.razor`

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [ ] `docs/UIGaps.md` → not updated this session
- [x] `docs/ImplementationTracker.md` → v3.6

---

## 5. Previous Session Summary (2026-03-09 Session 11)

### Completed — Code Quality Fixes (M-1, M-2) + User Management CRUD + Product Management + Product Dropdown Bug Fix

---

#### 1. Code Quality Fixes (Option B — M-1, M-2)

- **`ConsentRecordConfiguration.cs`**: Added `HasIndex(x => x.NIN)` — NIN index was missing (BVN index already existed).
- **`BureauReportConfiguration.cs`**: Added `HasIndex(x => x.ConsentRecordId)` — FK field had no index.
- M-3/M-4/M-5 deferred: M-5 touches 20+ files + migration column rename; M-3 requires full API shape change; M-4 needs distributed lock infrastructure.

#### 2. Product Management — Edit / Enable / Disable (Option D)

- **`LoanProduct.cs`** (Domain): Added `LoanProductSuspendedEvent`; existing `Suspend()` now raises it.
- **`SuspendLoanProductCommand.cs`** (NEW): Command + handler calling `product.Suspend()`.
- **`DependencyInjection.cs`**: Registered `ActivateLoanProductHandler` and `SuspendLoanProductHandler`.
- **`ApplicationService.cs`**: Added `CreateLoanProductAsync()`, `UpdateLoanProductAsync()`, `ToggleLoanProductAsync()` (calls Suspend or Activate based on current state).
- **`Products.razor`**: `SaveProduct()` now calls real backend (Create or Update per `isEditing`); `ToggleProduct()` calls `ToggleLoanProductAsync()`; error displayed in modal footer.

#### 3. User Management CRUD (Option C)

- **`ApplicationUser.cs`** (Domain): Added `ClearRoles()` method (domain already had `UpdateProfile`).
- **`UpdateUserCommand.cs`** (NEW): Command + handler — updates FirstName, LastName, PhoneNumber, clears and reassigns roles.
- **`ToggleUserStatusCommand.cs`** (NEW): Command + handler — calls `Activate()` or `Deactivate()` based on `request.Deactivate`.
- **`DependencyInjection.cs`**: Registered `RegisterUserHandler`, `UpdateUserHandler`, `ToggleUserStatusHandler`.
- **`ApplicationService.cs`**: Added `CreateUserAsync()`, `UpdateUserAsync()`, `ToggleUserStatusAsync()`. Default password for new users: `Welcome@1234`.
- **`Users.razor`**: `SaveUser()` calls Create or Update (real backend); `ToggleUserStatus()` calls `ToggleUserStatusAsync()`; `saveError` shown in modal footer.

#### 4. Product Dropdown Bug Fix (New Application page)

**Root cause:** `LoanProductSummaryDto` was missing `MinTenorMonths`, `MaxTenorMonths`, `BaseInterestRate`. So `GetLoanProductsAsync()` and `GetAllLoanProductsAsync()` hardcoded these values (`6`, `60`, `15m`) regardless of what the admin configured.

- **`LoanProductDto.cs`**: Added `MinTenorMonths`, `MaxTenorMonths`, `BaseInterestRate` to `LoanProductSummaryDto`.
- **`LoanProductMappings.cs`**: `ToSummaryDto()` now maps real domain values; `BaseInterestRate` = first pricing tier rate (or 0).
- **`ApplicationService.cs`**: Both `GetLoanProductsAsync()` and `GetAllLoanProductsAsync()` now use `p.MinTenorMonths`, `p.MaxTenorMonths`, `p.BaseInterestRate` — no more hardcoded values.

> **Remaining note:** `New.razor` mock fallback uses `Guid.NewGuid()` product IDs — valid only for UI demo when DB is empty. Real fix = seed default products via the `SeedData` class in Infrastructure.

### Files Updated This Session
- `src/CRMS.Infrastructure/Persistence/Configurations/Consent/ConsentRecordConfiguration.cs`
- `src/CRMS.Infrastructure/Persistence/Configurations/CreditBureau/BureauReportConfiguration.cs`
- `src/CRMS.Domain/Aggregates/ProductCatalog/LoanProduct.cs`
- `src/CRMS.Application/ProductCatalog/Commands/SuspendLoanProductCommand.cs` ← **NEW**
- `src/CRMS.Application/ProductCatalog/DTOs/LoanProductDto.cs`
- `src/CRMS.Application/ProductCatalog/Mappings/LoanProductMappings.cs`
- `src/CRMS.Domain/Entities/Identity/ApplicationUser.cs`
- `src/CRMS.Application/Identity/Commands/UpdateUserCommand.cs` ← **NEW**
- `src/CRMS.Application/Identity/Commands/ToggleUserStatusCommand.cs` ← **NEW**
- `src/CRMS.Infrastructure/DependencyInjection.cs`
- `src/CRMS.Web.Intranet/Services/ApplicationService.cs`
- `src/CRMS.Web.Intranet/Components/Pages/Admin/Products.razor`
- `src/CRMS.Web.Intranet/Components/Pages/Admin/Users.razor`

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v3.6
- [x] `docs/ImplementationTracker.md` → v3.5

---

## 5. Previous Session Summary (2026-03-09 Session 10)

### Completed — Bank Statement Transaction Detail Viewer

**Goal:** Add a drill-down view so users can see individual transactions inside any bank statement (own-bank CoreBanking or external).

#### What was built

- **`StatementTransactionInfo`** added to `ApplicationModels.cs` — UI model for a single transaction (Date, Description, Amount, Type, RunningBalance, Reference, Category, CategoryConfidence, IsRecurring).
- **`GetStatementTransactionsAsync(Guid statementId)`** added to `ApplicationService.cs` — calls the already-existing `GetStatementTransactionsHandler` (DI-registered since Session 7) and maps results to `StatementTransactionInfo`.
- **`ViewStatementModal.razor`** (new) — full-featured transaction viewer:
  - Header with bank name, account, period
  - Summary row: Opening/Closing balance, transaction count, total credits/debits
  - Filter buttons: All / Credits / Debits (with live counts)
  - Live search by description or reference
  - Scrollable table: Date | Description | Ref | Category | Debit | Credit | Running Balance
  - Recurring badge (↻) on recurring transactions
  - Category badges color-coded: red = Gambling/Bounced, green = Salary/Income/Transfer In, yellow = Loan/Rent/Utility
  - Negative running balance highlighted in red
- **`StatementsTab.razor`** — added "View" button to the own-bank card and to every row in the external statements table; added `OnViewTransactions` `EventCallback<Guid>` parameter.
- **`Detail.razor`** — added `OnViewTransactions="ShowViewStatementTransactionsModal"` param to `StatementsTab`; added state vars (`showViewStatementTransactionsModal`, `viewingStatementTransactionsId`); added show/close handlers; added `ViewStatementModal` rendering block.

**Build:** 0 errors.

### Files Updated This Session
- `src/CRMS.Web.Intranet/Models/ApplicationModels.cs`
- `src/CRMS.Web.Intranet/Services/ApplicationService.cs`
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Modals/ViewStatementModal.razor` ← **NEW**
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Tabs/StatementsTab.razor`
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Detail.razor`

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v3.5
- [x] `docs/ImplementationTracker.md` → v3.4

---

## 5. Previous Session Summary (2026-03-01 Session 9)

### Completed — UI Theme Migration to Forest Green + Bug Fixes

**Goal:** Migrate the entire CRMS intranet UI to match the RH-SHF-EOI reference app's forest green color scheme, and fix broken/misaligned UI elements.

#### 1. Color Scheme Migration (CSS Variables)

- **`wwwroot/css/app.css`**: Replaced all 10 `--primary-*` CSS variables from blue (#3b82f6 scale) to forest green (#1a5f2a / #2e7d32 scale). All components using `var(--primary-*)` (buttons, badges, form focus rings, tabs, spinners, nav items, user avatar, logo icon) now render in green.
- **`wwwroot/app.css`**: Updated legacy Bootstrap-style `.btn-primary`, link color, and focus ring from blue to green.
- Both sidebar gradients updated to dark forest green (`#0d2813 → #1a3d20`).

#### 2. Sidebar Background Not Updating (Critical Fix)

- **Root cause:** `MainLayout.razor.css` (Blazor scoped CSS) had an old blue/purple gradient `rgb(5,39,103) → #3a0647` and `position: sticky` on `.sidebar`. Scoped CSS has higher specificity than global CSS — it was winning and overriding the global green gradient and `position: fixed`.
- **Fix:** Rewrote `MainLayout.razor.css` to contain only the dark green sidebar gradient and the `#blazor-error-ui` styles. Removed all legacy template styles (`.page`, `.top-row`, sidebar width/position overrides).

#### 3. NavMenu Legacy CSS Conflicts (Fixed)

- **Root cause:** `NavMenu.razor.css` had `padding-bottom: 0.5rem` on `.nav-item` (conflicting with global padding), `::deep a.active` background override (conflicting with themed active state), and other legacy template styles.
- **Fix:** Cleared `NavMenu.razor.css` to a comment-only file. All nav styling now comes exclusively from the global `app.css`.

#### 4. Login Page Heading Text Invisible (Fixed)

- **Root cause:** Global CSS rule `h1, h2, h3 { color: var(--gray-900); }` explicitly sets dark text, overriding the inherited `color: white` from `.login-left`. On the dark green background this made headings nearly invisible.
- **Fix:** Added `.login-left h1, .login-left h2, .login-left h3, .login-left h4, .login-left p, .login-left span { color: white; }` to `app.css`.

#### 5. Applications List Empty (Fixed)

- **Root cause:** `Applications/Index.razor` called `AppService.GetMyApplicationsAsync()` which returns an empty list when the DB has no data for the current user. Unlike the Dashboard page, it had no mock data fallback. The `GenerateMockApplications()` method was defined but never called.
- **Fix:** Added mock data fallback (same pattern as Dashboard): if `GetMyApplicationsAsync` returns empty, call `GenerateMockApplications()` as a fallback.

### Files Updated This Session
- `wwwroot/css/app.css` — primary color vars, sidebar gradient, login gradient, login-left text fix
- `wwwroot/app.css` — legacy link/button blue → green
- `Components/Layout/MainLayout.razor.css` — complete rewrite (remove conflicting legacy styles, fix sidebar)
- `Components/Layout/NavMenu.razor.css` — cleared conflicting legacy styles
- `Components/Pages/Applications/Index.razor` — mock data fallback added

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [ ] `docs/UIGaps.md` → no feature change, visual-only
- [ ] `docs/ImplementationTracker.md` → no feature change, visual-only

---

## 5.1 Previous Session Summary (2026-03-01 Session 8)

### Completed — SDK Version Pin (Runtime Crash Fix)

**Bug:** App crashed on every page load with `InvalidOperationException: Router does not have property 'NotFoundPage'`.

**Root cause:** Two SDKs are installed (9.0.310 and 10.0.102). With no `global.json`, the machine defaulted to SDK 10. The SDK 10 Razor compiler generates .NET 10-style `Router` code using a `NotFoundPage` (Type) parameter; the project's net9.0 runtime `Router` only knows `NotFound` (RenderFragment) — mismatch at runtime.

**Fix:** Created `global.json` at repo root pinning SDK to `9.0.310` with `rollForward: latestPatch`. One file, zero code changes. Build and runtime now match.

### Files Updated This Session
- `global.json` ← **NEW** (repo root)

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [ ] `docs/UIGaps.md` → no change (not a UI feature)
- [ ] `docs/ImplementationTracker.md` → no change (infrastructure-only fix)

---

## 5.2 Previous Session Summary (2026-03-01 Session 7)

### Completed — Bank Statement Auto-Fetch + External Statements UI + Editable Fallback Fields

Three related gaps implemented in a single session:

#### 1. Bank Statement Auto-Fetch at Application Creation

- **`InitiateCorporateLoanCommand.cs`**: Injects `IBankStatementRepository`; after saving the application, calls `ICoreBankingService.GetStatementAsync()` (6-month window) and persists a `BankStatement` aggregate with `StatementSource.CoreBanking` and all transactions.
- **`LoanApplication.cs`**: Added `IncorporationDate` property; updated `CreateCorporate(...)` factory; added `UpdatePartyFields(...)` domain method.
- **`LoanApplicationParty.cs`**: Added `UpdateBVN()` and `UpdateShareholdingPercent()` domain methods.

#### 2. Bank Statement UI (StatementsTab)

- **`StatementsTab.razor`** (new): Two sections — Own Bank (internal CoreBanking) and Other Banks (external). Trust badges (100% Internal / 85% Verified / 70% Unverified). Cashflow metrics when analysis complete. Verify/Reject/Analyze action buttons.
- **`UploadExternalStatementModal.razor`** (new): Fields: bank name, account number/name, period, opening/closing balance. Period ≥ 3 month validation.
- **`Detail.razor`**: Added "Bank Statements" tab; wired modal state for upload, reject statement (with reason), analyze; `LoadApplication()` fetches real statements.
- **`ApplicationService.cs`**: Added `GetBankStatementsAsync`, `UploadExternalStatementAsync`, `VerifyStatementAsync`, `RejectStatementAsync`, `AnalyzeStatementAsync`.

#### 3. Editable Fallback for Null Core Banking Fields

- **`FillPartyInfoModal.razor`** (new): Targeted modal; shows only null fields (BVN if empty, shareholding % if null and Director).
- **`PartiesTab.razor`**: Added "Complete info" warning button per row when `IsEditable && null fields exist`. Added `OnFillPartyInfo` param.
- **`New.razor`**: Replaced mock `FetchCustomer` with real `AppService.FetchCorporateDataAsync()`; shows editable override fields for null RC number and IncorporationDate from core banking.
- **`ApplicationService.cs`**: Added `FetchCorporateDataAsync()` (returns `ApiResponse<CustomerInfo>`) and `UpdatePartyInfoAsync()`.
- **`UpdatePartyInfoCommand.cs`** (new): Command + handler for party BVN/shareholding updates.

#### Application Layer Updates

- **`UploadStatementCommand.cs`**: Added `VerifyStatementCommand`/`RejectStatementCommand` + handlers.
- **`StatementAnalysisDtos.cs`**: Extended `BankStatementSummaryDto` from 8 to 18 fields.
- **`GetStatementQuery.cs`**: Updated `GetStatementsByLoanApplicationHandler` mapper for new fields.
- **`LoanApplicationDtos.cs`**: Added `IncorporationDate` to `LoanApplicationDto`.
- **`GetLoanApplicationQuery.cs`**: Both `MapToDto` overloads updated to map `IncorporationDate`.
- **`ApplicationModels.cs`**: Added `BankStatementInfo`, `UploadExternalStatementRequest`, `RawBVN`/`PartyType` to `PartyInfo`, `IncorporationDate` to `LoanApplicationDetail`.

#### Infrastructure

- **`DependencyInjection.cs`**: Registered `TransactionCategorizationService`, `CashflowAnalysisService`, 8 statement handlers, `UpdatePartyInfoHandler`.
- **`LoanApplicationConfiguration.cs`**: Added `IncorporationDate` column config.
- **Migration `20260301170000_AddIncorporationDateToLoanApplication`**: Manual migration (+ Designer.cs) adding nullable `datetime(6)` `IncorporationDate` column to `LoanApplications`.
- **`CRMSDbContextModelSnapshot.cs`**: Updated with `IncorporationDate`.

### Files Updated This Session
- `src/CRMS.Domain/Aggregates/LoanApplication/LoanApplication.cs`
- `src/CRMS.Domain/Aggregates/LoanApplication/LoanApplicationParty.cs`
- `src/CRMS.Application/LoanApplication/DTOs/LoanApplicationDtos.cs`
- `src/CRMS.Application/LoanApplication/Queries/GetLoanApplicationQuery.cs`
- `src/CRMS.Application/LoanApplication/Commands/InitiateCorporateLoanCommand.cs`
- `src/CRMS.Application/LoanApplication/Commands/UpdatePartyInfoCommand.cs` ← **NEW**
- `src/CRMS.Application/StatementAnalysis/Commands/UploadStatementCommand.cs`
- `src/CRMS.Application/StatementAnalysis/DTOs/StatementAnalysisDtos.cs`
- `src/CRMS.Application/StatementAnalysis/Queries/GetStatementQuery.cs`
- `src/CRMS.Infrastructure/DependencyInjection.cs`
- `src/CRMS.Infrastructure/Persistence/Configurations/LoanApplication/LoanApplicationConfiguration.cs`
- `src/CRMS.Infrastructure/Persistence/Migrations/20260301170000_AddIncorporationDateToLoanApplication.cs` ← **NEW**
- `src/CRMS.Infrastructure/Persistence/Migrations/20260301170000_AddIncorporationDateToLoanApplication.Designer.cs` ← **NEW**
- `src/CRMS.Infrastructure/Persistence/Migrations/CRMSDbContextModelSnapshot.cs`
- `src/CRMS.Web.Intranet/Models/ApplicationModels.cs`
- `src/CRMS.Web.Intranet/Services/ApplicationService.cs`
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Tabs/StatementsTab.razor` ← **NEW**
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Tabs/PartiesTab.razor`
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Modals/UploadExternalStatementModal.razor` ← **NEW**
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Modals/FillPartyInfoModal.razor` ← **NEW**
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Detail.razor`
- `src/CRMS.Web.Intranet/Components/Pages/Applications/New.razor`

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v3.4
- [x] `docs/ImplementationTracker.md` → v3.3

---

## 5.1 Previous Session Summary (2026-03-01 Session 5)

### Completed — Comprehensive Code Review + Critical/High Bug Fixes

This session performed a full code review of the SmartComply integration (Sessions 1–4). 14 issues were identified (1 critical, 3 high, 5 medium, 5 low). The 4 critical/high bugs were fixed immediately.

#### BUG Fixes Applied

- **C-1 (CRITICAL): Workflow no longer advances when all credit checks blocked by missing consent**
- **H-1 (HIGH): `RecordBulkConsentHandler.CreateOrGetConsent` no longer throws `InvalidOperationException`**
- **H-2 (HIGH): InternalError path in credit check now creates NDPA audit record**
- **H-3 (HIGH): Duplicate consent records for same-BVN parties in bulk consent batch**

---

## 5.3 Previous Session Summary (2026-03-01 Session 4)

### Completed — Extended Bug Fixes, NDPA Compliance & Production Hardening

BUG-A through BUG-I, GAP-F through GAP-H, DESIGN-J fixes applied. See Session 4 details in previous handoff versions.

---

## 5.5 Previous Sessions (2026-03-01 Sessions 1-3)

Sessions 1-3 focused on SmartComply infrastructure and backend wiring. See previous handoff versions for full details.

---

## 6. Suggested Next Task

### Option A — Template Management CRUD (`/admin/templates`)

Notification templates page is display-only. Need:
1. Check if `CreateNotificationTemplateCommand` / `UpdateNotificationTemplateCommand` handlers exist in Application layer
2. Add `CreateTemplateAsync`, `UpdateTemplateAsync`, `DeleteTemplateAsync` to `ApplicationService`
3. Wire the Templates page with create/edit modals

---

### Option B — Configure Real CBS Credentials & Test Live

Set real `BaseUrl`, `ClientId`, `ClientSecret` in appsettings (or user-secrets), flip `UseMock: false`, and test the full flow against the CBS sandbox. Verify: account lookup, director fetch, 6-month statement pull, discrepancy indicator.

---

### Option C — Bureau Report Detail Modal

Add a click-to-expand detail view for individual bureau reports in the BureauTab. Currently shows summary data in the table but no way to see full report details.

---

### Option D — Guarantor Credit Check Trigger UI

Add a "Run Credit Check" button on the Guarantor tab that triggers bureau checks for individual guarantors. Backend handlers exist but no UI trigger.

---

### Option E — Populate Standing Committee Members from Seed

The 5 standing committees are seeded but have no members assigned. Add test committee member assignments in SeedData using the seeded test users (e.g. `creditofficer@crms.test`, `horeviewer@crms.test`).

**Note:** `dotnet ef database update` requires `Microsoft.EntityFrameworkCore.Design` — use `dotnet run` instead (app runs `MigrateAsync()` on startup automatically).

---

## 7. Build & Run Reference

```bash
# Build (stop the app first, or expect file-lock warnings)
dotnet build src/CRMS.Web.Intranet/CRMS.Web.Intranet.csproj --no-restore -v quiet

# Run
dotnet run --project src/CRMS.Web.Intranet/CRMS.Web.Intranet.csproj

# Check for real errors only (ignore MSB3026/MSB3021 file-lock noise from running app)
dotnet build src/CRMS.Web.Intranet/CRMS.Web.Intranet.csproj 2>&1 | grep "error CS"

# Verify correct SDK is active (must be 9.x, not 10.x)
dotnet --version   # should print 9.0.x
```

`MSB3026` / `MSB3021` errors = app is running and holding DLL locks. **Not real errors.** Only `error CS` lines are compiler errors.

### SDK Version — CRITICAL

A `global.json` at the repo root pins the SDK to **9.0.310** (`rollForward: latestPatch`). **Do not remove it.**

**Why:** Both SDK 9.0.310 and SDK 10.0.102 are installed on this machine. SDK 10's Razor compiler generates .NET 10-style Router code (`NotFoundPage` as a `Type` parameter) which is incompatible with the net9.0 runtime's `Router` class (which uses `NotFound` as a `RenderFragment`). Without the pin, the app crashes on startup with `InvalidOperationException: Router does not have property 'NotFoundPage'`.

If you see that error again, run `dotnet --version` first — it must say `9.0.x`.

---

## 8. Mock Data Reference

### Core Banking Mock (CBS)
Account `1234567890` ("Acme Industries Ltd", clientType=BUSINESS, RC=RC123456):
- 3 directors (CBS shape — name/BVN/email/phone only, no shareholding): John Adebayo, Amina Ibrahim, Chukwuma Okonkwo
- 2 signatories (same CBS shape): John Adebayo, Fatima Bello
- CBS does **not** return: `ShareholdingPercent`, `Nationality`, `MandateType`, `Designation`, `Industry`, `IncorporationDate`

Account `0987654321` ("Oluwaseun Bakare", clientType=PERSON): individual account, no directors/signatories.

Any other NUBAN returns "not found". Use `1234567890` when testing the New Application flow.

### Core Banking Configuration
```json
"CoreBanking": {
    "BaseUrl": "",           // e.g. "https://sandbox.cbs.com/api"
    "ClientId": "",          // OAuth2 client_id
    "ClientSecret": "",      // OAuth2 client_secret
    "TokenEndpoint": "/oauth/token",
    "TimeoutSeconds": 30,
    "UseMock": true          // flip to false for real CBS
}
```

### SmartComply CAC Mock
RC `RC123456` returns 3 directors with full CAC data (shares, appointment date, chairman flag, etc.).

---

## 9. Reference Docs (only read when specifically needed)

| Doc | Read When |
|---|---|
| `docs/UIGaps.md` | Need full UI feature status, modal list, or session file change history |
| `docs/ImplementationTracker.md` | Need full architecture details, DDD patterns, or module list |
| `docs/modules/CollateralManagement.md` | Need collateral domain model, haircut table, LTV formula |
| `docs/modules/GuarantorManagement.md` | Need guarantor domain model or credit check flow |
| `docs/modules/CorporateLoanInitiation.md` | Need document workflow, application states, or credit check trigger logic |
| `docs/audit/CollateralManagement.md` | Historical bug report — useful if a collateral domain bug is suspected |
