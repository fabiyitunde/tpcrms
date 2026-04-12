# CRMS Intranet UI - Gap Analysis

**Document Version:** 5.7
**Date:** 2026-04-09
**Status:** Priority 1 RESOLVED | Priority 2 RESOLVED | Priority 3 RESOLVED

---

## Executive Summary

Following the 2026-02-18 gap analysis, significant progress has been made. The core data entry workflow (Collateral, Guarantors, Documents, Financial Statements) is now functional. Workflow transitions work correctly. Financial statement validation supports businesses of all ages. Document verification/rejection and collateral valuation/approval are now also complete.

---

## 1. Resolved Issues (Priority 1)

### 1.1 UI Buttons - NOW FUNCTIONAL ✅

| Location | Button | Backend Command | Status |
|----------|--------|-----------------|--------|
| `CollateralTab.razor` | "Add Collateral" | `AddCollateralCommand` | ✅ RESOLVED |
| `GuarantorsTab.razor` | "Add Guarantor" | `AddIndividualGuarantorCommand` | ✅ RESOLVED |
| `DocumentsTab.razor` | "Upload Document" | `UploadDocumentCommand` | ✅ RESOLVED |
| `DocumentsTab.razor` | View | `IFileStorageService` | ✅ RESOLVED |
| `DocumentsTab.razor` | Download | `IFileStorageService` | ✅ RESOLVED |
| `FinancialsTab.razor` | "Add Financial Year" | `CreateFinancialStatementCommand` | ✅ RESOLVED |
| `Detail.razor` | Approve/Return/Reject | `TransitionWorkflowCommand` | ✅ RESOLVED |

### 1.2 ApplicationService Methods - ADDED ✅

The following methods were added to `ApplicationService.cs`:

```csharp
// Collateral
- AddCollateralAsync(Guid applicationId, AddCollateralRequest request)
- SetCollateralValuationAsync(Guid collateralId, decimal marketValue, decimal? forcedSaleValue, decimal? haircutPercentage)
- ApproveCollateralAsync(Guid collateralId, Guid approvedByUserId)             // Added 2026-02-20

// Guarantors
- AddGuarantorAsync(Guid applicationId, AddGuarantorRequest request)

// Documents
- UploadDocumentAsync(Guid applicationId, UploadDocumentRequest request)
- VerifyDocumentAsync(Guid applicationId, Guid documentId, Guid userId)
- RejectDocumentAsync(Guid applicationId, Guid documentId, Guid userId, string reason)  // Added 2026-02-20

// Financial Statements
- CreateFinancialStatementAsync(Guid applicationId, CreateFinancialStatementRequest request)
- GetFinancialStatementsForApplicationAsync(Guid applicationId)

// Workflow
- TransitionWorkflowAsync(Guid workflowInstanceId, LoanApplicationStatus toStatus, WorkflowAction action, string? comments, Guid userId, string userRole)
- ApproveApplicationAsync(Guid applicationId, string comments, Guid userId, string userRole)
- ReturnApplicationAsync(Guid applicationId, string comments, Guid userId, string userRole)
- RejectApplicationAsync(Guid applicationId, string comments, Guid userId, string userRole)
```

### 1.3 Modal Components - CREATED ✅

| Component | Purpose | Features |
|-----------|---------|----------|
| `AddCollateralModal.razor` | Add collateral to application | Type, description, location, owner info |
| `EditCollateralModal.razor` | Edit existing collateral | Same fields as add, pre-populated |
| `ViewCollateralModal.razor` | View collateral details | Full detail including valuation, lien, insurance |
| `SetCollateralValuationModal.razor` | Set valuation for collateral | Market value, FSV, haircut %, live acceptable value | ← *Added 2026-02-20* |
| `UploadCollateralDocumentModal.razor` | Upload document to collateral | Document type, file upload, description | ← *Added 2026-02-21* |
| `AddGuarantorModal.razor` | Add guarantor to application | Personal info, BVN, guarantee type, net worth |
| `EditGuarantorModal.razor` | Edit existing guarantor | Same fields as add, pre-populated |
| `ViewGuarantorModal.razor` | View guarantor details | Full detail including credit check data |
| `UploadDocumentModal.razor` | Upload documents | File picker, category selection |
| `FinancialStatementModal.razor` | 4-step financial data entry | Header → Balance Sheet → P&L → Cash Flow |

### 1.4 Financial Statement Validation - ENHANCED ✅

Business age-based validation now implemented:

| Business Age | Required Statements |
|-------------|---------------------|
| Startup (< 1 year) | 3 years Projected |
| 1 year | 1 Actual + 2 Projected |
| 2 years | 2 Actuals (1 Audited) + 1 Projected |
| 3+ years | 3 years Audited |

### 1.5 Document Viewer - IMPLEMENTED ✅

- PDF preview via iframe with `Content-Disposition: inline`
- Image preview (PNG, JPG, GIF)
- Download functionality with `Content-Disposition: attachment`
- API endpoints: `/api/documents/{id}/view` and `/api/documents/{id}/download`

---

## 2. Remaining Issues (Priority 2)

### 2.1 Collateral Management - ✅ Complete

| Feature | Status | Notes |
|---------|--------|-------|
| Add collateral | ✅ Done | Via modal |
| Edit collateral | ✅ Done | Via modal (Proposed/UnderValuation status only) |
| Delete collateral | ✅ Done | Confirmation modal (Proposed status only) |
| View collateral detail | ✅ Done | Full detail modal including valuation, lien, insurance, documents |
| Set valuation | ✅ Done | `SetCollateralValuationModal` — market value, FSV, haircut %, live acceptable value calc |
| Approve collateral | ✅ Done | Confirmation modal, calls `ApproveCollateralAsync` |
| Upload collateral documents | ✅ Done | `UploadCollateralDocumentModal`; upload button in CollateralTab (Draft or review stages) |
| View collateral documents | ✅ Done | View/download buttons in `ViewCollateralModal` DOCUMENTS section |
| Delete collateral documents | ✅ Done | Confirmation dialog; deletes both DB record and file from storage |

`CanManageValuation` is `true` when the application is actively in review (not Draft, Approved, CommitteeApproved, Rejected, or Disbursed).

**Collateral Document Access Control:**
- Upload/Delete available: Draft, BranchReview, HOReview, CreditAnalysis, FinalApproval
- Upload/Delete NOT available: Approved, CommitteeApproved, Rejected, Disbursed

### 2.2 Guarantor Management - ✅ Core Complete

| Feature | Status | Notes |
|---------|--------|-------|
| Add guarantor | ✅ Done | Via modal |
| Edit guarantor | ✅ Done | Via modal |
| Delete guarantor | ✅ Done | Confirmation modal |
| View guarantor detail | ✅ Done | Full detail modal |
| Approve guarantor | ✅ Done | Confirmation modal; `CanManageGuarantors` gate; status `CreditCheckCompleted` only |
| Reject guarantor | ✅ Done | Modal with mandatory reason; statuses `Proposed`, `PendingVerification`, `CreditCheckCompleted` |
| Run credit check | N/A | Auto-triggered after branch approval via `ProcessLoanCreditChecksCommand` |
| View credit report | ✅ Done | `ViewBureauReportModal` — full detail with accounts, fraud risk, alerts (Session 24) |

`CanManageGuarantors` is `true` when the application is actively in review (not Draft, Approved, CommitteeApproved, Rejected, or Disbursed).

### 2.3 Document Management - ✅ Complete

| Feature | Status | Notes |
|---------|--------|-------|
| Upload | ✅ Done | Via modal |
| View | ✅ Done | PDF/image preview |
| Download | ✅ Done | File download |
| Verify | ✅ Done | Inline button; calls `VerifyDocumentAsync` |
| Reject | ✅ Done | Modal with reason textarea; calls `RejectDocumentAsync` |

**Note on status display:** The domain creates documents with `DocumentStatus.Uploaded` but the UI displays this as "Pending" (via `FormatStatus()` in `DocumentsTab.razor`). Both "Uploaded" and "Pending" strings trigger the verify/reject buttons.

### 2.4 Credit Bureau Checks - ✅ Complete

| Feature | Status | Notes |
|---------|--------|-------|
| List bureau reports | ✅ Done | Fetches real data from DB; grouped by party type |
| Business credit reports | ✅ Done | Separate section with distinct styling |
| Fraud risk display | ✅ Done | Color-coded badges (Low/Medium/High) |
| Party grouping | ✅ Done | Directors, Signatories, Guarantors in separate sections |
| New metrics | ✅ Done | Total Overdue, Max Delinquency Days, Provider name |
| Failed check indicator | ✅ Done | Shows "Check Failed" badge for failed reports |
| Request check | ⏸️ On Hold | Manual re-check button not needed — auto-triggered after branch approval |
| View report detail | ✅ Done | `ViewBureauReportModal` — click any card to see full detail (Session 24) |

### 2.5 Directors/Signatories Management - ✅ Core Complete

| Feature | Status | Notes |
|---------|--------|-------|
| List parties | ✅ Done | PartiesTab displays directors and signatories |
| Add director/signatory | N/A — Not needed | Auto-fetched from core banking at application creation |
| Fill null BVN / shareholding % | ✅ Done | `FillPartyInfoModal` — shows only missing fields; Draft status only |
| Run bureau check | ⏸️ On Hold | Pending credit bureau provider decision |

**Confirmed (2026-02-20):** `InitiateCorporateLoanCommand` automatically calls `GetDirectorsAsync(customer.CustomerId)` and `GetSignatoriesAsync(accountNumber)` at application creation, populating all parties as `LoanApplicationParty` records. No "Add Director" or "Add Signatory" modals are needed — the PartiesTab is intentionally read-only for structure.

**Added (2026-03-01):** When core banking returns null BVN or shareholding %, a "Complete info" warning button appears in the row (Draft only). `FillPartyInfoModal` collects only the null fields and calls `UpdatePartyInfoAsync`.

### 2.6 Bank Statement Management - ✅ Complete

| Feature | Status | Notes |
|---------|--------|-------|
| Auto-fetch on application create | ✅ Done | `InitiateCorporateLoanCommand` persists CoreBanking statement (6-month) on create |
| View own-bank statement | ✅ Done | `StatementsTab` — Own Bank section with trust badge, cashflow metrics |
| Upload external statement | ✅ Done | `UploadExternalStatementModal` — bank name, account, period, balances, optional file upload |
| File attachment on upload | ✅ Done | `InputFile` accepts `.pdf,.csv,.xlsx,.xls`; stored via `IFileStorageService` |
| CSV/Excel transaction parsing | ✅ Done | `StatementFileParserService` — auto column detection, 18 date formats, 40+ column name aliases |
| Transaction entry grid | ✅ Done | `ManageStatementTransactionsModal` — date, description, debit/credit (mutually exclusive), running balance; Add/Delete rows |
| Live reconciliation display | ✅ Done | Computed vs Expected closing balance; ±₦1 tolerance; green/orange color coding |
| Pre-populate grid from file | ✅ Done | After CSV/Excel upload, grid auto-populated with parsed transactions + parse message banner |
| Verify external statement | ✅ Done | Inline button; calls `VerifyStatementAsync`; trust weight → 85%; disabled until transactions entered |
| Reject external statement | ✅ Done | Reason modal; calls `RejectStatementAsync` |
| Trigger cashflow analysis | ✅ Done | "Analyze" button per statement; calls `AnalyzeStatementAsync`; disabled until transactions entered |
| Cashflow metrics display | ✅ Done | Credits, Debits, Avg Balance, Net Cashflow, Bounced/Gambling tx counts |
| View individual transactions | ✅ Done | `ViewStatementModal` — filter (All/Credits/Debits), live search, color-coded category badges, recurring badge, negative balance highlight |
| Upload format guidance | ✅ Done | Collapsible panel in upload modal with column name table, sample CSV header row, link to Help |
| Help page format guide | ✅ Done | Full CSV/Excel format guide, 6 bank export instructions, troubleshooting section in `/help` |

---

## 3. Remaining Issues (Priority 3)

### 3.1 Admin Pages

| Page | Route | Status | Issues |
|------|-------|--------|--------|
| Scoring Config | `/admin/scoring` | ✅ Complete | Maker-checker editor, all 9 categories |
| Templates | `/admin/templates` | ✅ CRUD Complete | Create / Edit / Toggle / Preview wired to real backend |
| Users | `/admin/users` | ✅ CRUD Complete | Create / Edit / Activate / Deactivate wired to real backend |
| Products | `/admin/products` | ✅ CRUD Complete | Create / Edit / Enable / Disable wired to real backend; **Checklist Template sub-section pending** |
| Locations | `/admin/locations` | ✅ CRUD Complete | Tree/list view, create/edit/activate/deactivate |
| Committees | `/admin/committees` | ✅ CRUD Complete | Standing committees with amount-based routing, member management |

### 3.2 Report Pages

| Page | Route | Status | Issues |
|------|-------|--------|--------|
| Performance | `/reports/performance` | ✅ Complete | Wired to ReportingService (Session 20) |
| Committee | `/reports/committee` | ✅ Complete | Wired to ReportingService (Session 20) |
| Audit | `/reports/audit` | Partial | Connected but limited filtering |

### 3.3 Application Detail Tabs — All Wired (Session 20)

| Tab | Status | Notes |
|-----|--------|-------|
| Overview | ✅ | Real data from GetApplicationDetailAsync |
| Parties | ✅ | FillPartyInfoModal for null BVN/shareholding |
| Documents | ✅ | Upload/view/download/verify/reject |
| Financials | ✅ | 4-step manual entry + Excel upload |
| Bureau | ✅ | SmartComply integration, fraud risk badges, click-to-expand detail modal |
| Statements | ✅ | Auto-fetch + external upload + transaction drill-down |
| Collateral | ✅ | Full CRUD + valuation + approval + documents |
| Guarantors | ✅ | Full CRUD + approve/reject |
| Workflow | ✅ | Wired to GetWorkflowByLoanApplicationHandler (Session 20) |
| Advisory | ✅ | Wired to GetLatestAdvisoryByLoanApplicationHandler (Session 20) |
| Committee | ✅ | Wired to backend + voting auth guard + setup modal (Session 20) |
| Comments | ✅ | Wired to AddCommitteeCommentHandler (Session 20) |
| Disbursement Checklist | ✅ | `OfferAcceptanceTab.razor` — CP/CS item tables, role-filtered actions, single-modal pattern, CP gate enforced at domain level (Session 46) |

---

## 4. Files Modified

### 2026-02-19 Session

#### Components Created
- `Components/Pages/Applications/Modals/AddCollateralModal.razor`
- `Components/Pages/Applications/Modals/EditCollateralModal.razor`
- `Components/Pages/Applications/Modals/ViewCollateralModal.razor`
- `Components/Pages/Applications/Modals/AddGuarantorModal.razor`
- `Components/Pages/Applications/Modals/EditGuarantorModal.razor`
- `Components/Pages/Applications/Modals/ViewGuarantorModal.razor`
- `Components/Pages/Applications/Modals/UploadDocumentModal.razor`
- `Components/Pages/Applications/Modals/FinancialStatementModal.razor`
- `Components/Pages/Applications/Modals/UploadFinancialStatementModal.razor`
- `Components/Pages/Applications/Modals/_Imports.razor`

#### Components Modified
- `Components/Pages/Applications/Detail.razor` - Added modal integration, workflow actions, financial validation
- `Components/Pages/Applications/Index.razor` - Fixed to show user's Draft applications
- `Components/Pages/Applications/New.razor` - Changed "Submit" to "Create Application" (Draft only)
- `Components/Pages/Applications/Tabs/CollateralTab.razor` - Added OnAddCollateral event
- `Components/Pages/Applications/Tabs/GuarantorsTab.razor` - Added OnAddGuarantor event
- `Components/Pages/Applications/Tabs/DocumentsTab.razor` - Added upload, view, download handlers
- `Components/Pages/Applications/Tabs/FinancialsTab.razor` - Complete redesign with business age validation

#### Services Modified
- `Services/ApplicationService.cs` - Added 15+ methods for CRUD operations
- `Services/AuthService.cs` - Minor fixes

#### Infrastructure Modified
- `Infrastructure/DependencyInjection.cs` - Registered 30+ Application handlers
- `Program.cs` - Added file serving endpoints

#### Models Modified
- `Models/ApplicationModels.cs` - Added FinancialStatementInfo, request/response classes

---

### 2026-02-20 Session

#### Application Layer Added
- `Application/LoanApplication/Commands/UploadDocumentCommand.cs` - Added `RejectDocumentCommand` + `RejectDocumentHandler`

#### Components Created
- `Components/Pages/Applications/Modals/SetCollateralValuationModal.razor` - Set valuation: market value, FSV, haircut %, live acceptable value

#### Components Modified
- `Components/Pages/Applications/Tabs/CollateralTab.razor` - Added `CanManageValuation`, `OnSetValuation`, `OnApproveCollateral` params; "Set Valuation" button (Proposed/UnderValuation status), "Approve" button (Valued status)
- `Components/Pages/Applications/Tabs/DocumentsTab.razor` - Fixed status handling ("Uploaded" → "Pending" display); wired verify/reject buttons with correct styling
- `Components/Pages/Applications/Detail.razor` - Wired document verify/reject; wired collateral set-valuation/approve; added reject document modal; added approve collateral confirmation modal

#### Services Modified
- `Services/ApplicationService.cs` - Added `RejectDocumentAsync`, `ApproveCollateralAsync`

#### Infrastructure Modified
- `Infrastructure/DependencyInjection.cs` - Registered `RejectDocumentHandler`

---

### 2026-02-21 Session

#### Domain Layer Modified
- `Domain/Interfaces/ICollateralRepository.cs` - Added `ICollateralDocumentRepository` interface
- `Domain/Aggregates/Collateral/Collateral.cs` - Added `RemoveDocument()` method

#### Application Layer Added
- `Application/Collateral/Commands/CollateralCommands.cs` - Added `UploadCollateralDocumentCommand`, `UploadCollateralDocumentHandler`, `DeleteCollateralDocumentCommand`, `DeleteCollateralDocumentHandler`

#### Components Created
- `Components/Pages/Applications/Modals/UploadCollateralDocumentModal.razor` - Upload document to collateral with type selection

#### Components Modified
- `Components/Pages/Applications/Modals/ViewCollateralModal.razor` - Added DOCUMENTS section with view/download/delete buttons + delete confirmation dialog
- `Components/Pages/Applications/Tabs/CollateralTab.razor` - Added `OnUploadDocument` param and upload button
- `Components/Pages/Applications/Detail.razor` - Wired upload collateral document modal, added `OnCollateralDocumentDeleted` callback

#### Services Modified
- `Services/ApplicationService.cs` - Added `UploadCollateralDocumentAsync`, `DeleteCollateralDocumentAsync`
- `Services/ApplicationServiceDtos.cs` - Added `UploadCollateralDocumentRequest`, `CollateralDocumentResult`, `CollateralDocumentInfo`

#### Infrastructure Modified
- `Infrastructure/Persistence/Repositories/CollateralRepository.cs` - Added `CollateralDocumentRepository` class
- `Infrastructure/DependencyInjection.cs` - Registered `ICollateralDocumentRepository`, `UploadCollateralDocumentHandler`, `DeleteCollateralDocumentHandler`
- `Program.cs` - Added `/api/collateral-documents/{id}/view` and `/api/collateral-documents/{id}/download` endpoints

---

## 5. Next Steps

### Priority 2 (Remaining)
1. ~~Collateral valuation UI~~ ✅ Done
2. ~~Document verify/reject~~ ✅ Done
3. ~~Guarantor approve/reject workflow UI~~ ✅ Done
4. ~~Directors/Signatories CRUD~~ N/A — auto-fetched from core banking
5. ~~Collateral document upload/view/delete~~ ✅ Done
6. ~~Credit bureau check UI~~ ✅ Done (SmartComply integration)

### Priority 3 (Admin & Reports) — ALL RESOLVED
1. ~~User management CRUD (create, edit, deactivate)~~ ✅ Done
2. ~~Product edit/delete~~ ✅ Done
3. ~~Scoring configuration editor~~ ✅ Done
4. ~~Template management CRUD~~ ✅ Done (Session 24)
5. ~~Connect performance/committee report pages to `ReportingService`~~ ✅ Done (Session 20)
6. ~~Location management admin page (`/admin/locations`)~~ ✅ Done
7. ~~User admin page — location picker dropdown~~ ✅ Done
8. ~~Application Detail tabs wired to real backend~~ ✅ Done (Session 20 — Workflow, Advisory, Committee, Comments)
9. ~~Committee voting authorization guard~~ ✅ Done (Session 20)
10. ~~Committee setup UI (create review + add members)~~ ✅ Done (Session 20)
11. ~~Standing committee admin + automatic routing~~ ✅ Done (Session 21)
12. ~~Bureau report detail modal~~ ✅ Done (Session 24)

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-18 | Initial gap analysis (78 issues identified) |
| 2.0 | 2026-02-19 | Priority 1 resolved: Data entry modals (Collateral, Guarantor, Document, Financial Statement), workflow transitions, document viewer, financial statement validation, delete for collateral/guarantor, edit for collateral/guarantor |
| 3.0 | 2026-02-20 | Priority 2 partial: Document verify/reject fully wired; collateral set-valuation modal and approve confirmation added; directors/signatories confirmed N/A (auto-fetched from core banking); credit bureau on hold |
| 3.1 | 2026-02-21 | Guarantor approve/reject fully wired (`ApproveGuarantorAsync`, `RejectGuarantorAsync` in ApplicationService; GuarantorsTab updated; Detail.razor approve confirmation + reject reason modals; DI registrations confirmed; build clean) |
| 3.2 | 2026-02-21 | Collateral document management complete: `ICollateralDocumentRepository`, upload/delete handlers, `UploadCollateralDocumentModal.razor`, `ViewCollateralModal` DOCUMENTS section with view/download/delete + confirmation dialog, API endpoints for view/download, delete removes both DB record and file |
| 3.3 | 2026-03-01 | Credit Bureau UI complete: SmartComply integration wired to UI; `BureauTab.razor` redesigned with business reports section, party grouping (Directors/Signatories/Guarantors), fraud risk badges (Low/Medium/High), new metrics (TotalOverdue, MaxDelinquencyDays, Provider); `ApplicationService.GetBureauReportsAsync()` fetches real data; `BureauReportInfo` model updated with new fields |
| 3.4 | 2026-03-01 | Bank Statement auto-fetch + UI complete: `InitiateCorporateLoanCommand` now persists CoreBanking statement on create. New `StatementsTab.razor` (Own Bank + Other Banks), `UploadExternalStatementModal.razor`, `FillPartyInfoModal.razor`. `ApplicationService` + 7 new methods. `LoanApplication.IncorporationDate` + `UpdatePartyFields()`. `UpdatePartyInfoCommand`. Party null-field "Complete info" button in PartiesTab. New.razor uses real `FetchCorporateDataAsync()` with editable override fields. Migration `20260301170000_AddIncorporationDateToLoanApplication`. Build: 0 errors. |
| 3.6 | 2026-03-09 | Code quality (M-1 NIN index, M-2 ConsentRecordId index). User CRUD: `UpdateUserCommand`, `ToggleUserStatusCommand`, `ClearRoles()` on `ApplicationUser`; `CreateUserAsync`, `UpdateUserAsync`, `ToggleUserStatusAsync` in `ApplicationService`; `Users.razor` fully wired. Product CRUD: `SuspendLoanProductCommand`, `LoanProductSuspendedEvent`; `CreateLoanProductAsync`, `UpdateLoanProductAsync`, `ToggleLoanProductAsync` in `ApplicationService`; `Products.razor` fully wired. Bug fix: `LoanProductSummaryDto` missing `MinTenorMonths`/`MaxTenorMonths`/`BaseInterestRate` caused hardcoded values in product dropdown — fixed in DTO, mapper, and both service methods. Build: 0 errors. |
| 3.5 | 2026-03-09 | Bank statement transaction drill-down: `ViewStatementModal.razor` (new) — per-statement transaction list with filter (All/Credits/Debits), live search by description/reference, color-coded category badges, recurring badge, negative balance highlight. `StatementTransactionInfo` model added. `GetStatementTransactionsAsync` added to `ApplicationService`. "View" button added to own-bank card and every external statement row in `StatementsTab`. `Detail.razor` wired. No backend changes needed — `GetStatementTransactionsHandler` was already registered in DI. Build: 0 errors. |
| 3.7 | 2026-03-16 | Location hierarchy + visibility filtering: `Location` aggregate (4-level: HO→Region→Zone→Branch), `VisibilityScope` enum, `VisibilityService` domain service, `LocationRepository`, EF migration `AddLocationHierarchy`, seed data (21 Nigeria locations). `ApplicationUser.LocationId` replaces `BranchId`. Query handlers filter by visibility scope. `UserInfo.LocationId`/`PrimaryRole` added. `ApplicationService.GetApplicationsByStatusAsync` visibility-aware overload. Applications Index passes user context. Pending: Location admin page, user location picker. Build: 0 errors. |
| 3.8 | 2026-03-16 | Location/visibility bug fixes (8 bugs + 2 gaps): BUG-1 UserInfo.LocationId populated; BUG-2 new apps get user's branch; BUG-3 UserDto.BranchId→LocationId renamed; BUG-4 GetHierarchyTreeAsync builds tree; BUG-5 UpdateUserCommand has LocationId; BUG-6 API endpoint uses visibility; BUG-7 GetPendingBranchReviewHandler registered in Infrastructure DI; BUG-8 VisibilityService documented; GAP-2 test users seeded with locations (6 users, default pwd Test@123); GAP-5 NE zone now has 2 branches (Maiduguri, Bauchi). Build: 0 errors, tests pass. |
| 3.9 | 2026-03-16 | Location CRUD Admin UI + User location picker: New `Locations.razor` page at `/admin/locations` with tree view (collapsible hierarchy with icons), list view, search/filter, create/edit/activate/deactivate modals. Application layer: `LocationDtos.cs`, `LocationCommands.cs` (4 commands), `LocationQueries.cs` (5 queries). 9 handlers registered in DI. `ApplicationService` + 8 new methods. `Users.razor` updated with dynamic location picker dropdown replacing hardcoded branch list. Build: 0 errors, tests pass. |
| 4.0 | 2026-03-18 | Comprehensive UI wiring audit + critical fixes + committee setup. Phase 1: Report pages wired to ReportingService (Performance + Committee); M-3 RequestBureauReportCommand migrated to ISmartComplyProvider; M-5 NonPerformingAccounts→DelinquentFacilities rename (10 files + migration); M-4 in-process concurrency lock; removed mock product fallback. Phase 2: 4 Detail tabs wired to real backend (Workflow, Advisory, Committee, Comments); DownloadDocumentAsync implemented; GetMyPendingTasksAsync fixed (Amount/ProductName); collateral mapping fixed. Phase 3: Committee voting authorization guard (role-based, 3 states); SetupCommitteeModal (2-step wizard: configure committee + add members with roles/chairperson); `CanSetupCommitteeReview` for CreditOfficer role at CommitteeCirculation status. 6 new DI registrations. Build: 0 errors, tests pass. |
| 4.1 | 2026-03-18 | Standing committee infrastructure + automatic routing. New `StandingCommittee` aggregate with amount thresholds, permanent member rosters, quorum rules. `Committees.razor` admin page at `/admin/committees` with CRUD + member management. `SetupCommitteeModal` rewritten: auto-routes to matching standing committee by loan amount, pre-populates members; falls back to ad-hoc if no match. 5 standing committees seeded (Branch N0-50M, Regional N50-200M, HO N200-500M, Management N500M-2B, Board N2B+). Migration `20260318120000_AddStandingCommittees`. 8 new DI registrations. Build: 0 errors, tests pass. |
| 4.2 | 2026-03-18 | Overdue functionality bug fix (5 bugs). BUG-1: NavMenu badge counts hardcoded (2,5,1) — now fetched from backend. BUG-2: ReportingService used `IsSLABreached` flag vs repository `SLADueAt < now` — aligned to use `SLADueAt < now`. BUG-3/4: `IsSLABreached` flag never set (no background job) — now irrelevant. BUG-5: NavMenu had no `OnInitializedAsync`. Added 3 count methods to ApplicationService (`GetOverdueCountAsync`, `GetMyQueueCountAsync`, `GetMyPendingVotesCountAsync`). NavMenu now loads real counts on init. Build: 0 errors, tests pass. |
| 4.3 | 2026-03-18 | P3 gaps resolved. Template CRUD: `NotificationTemplateCommands.cs` (3 commands+handlers), `INotificationTemplateRepository.GetAllAsync()`, 5 DI registrations, `Templates.razor` rewrite with real backend. Bureau report detail modal: `ViewBureauReportModal.razor` (new) with accounts, fraud risk, alerts; `BureauTab.razor` OnViewReport param + view buttons; `Detail.razor` wired. Guarantor credit check trigger confirmed N/A (auto-triggered via `ProcessLoanCreditChecksCommand`). Build: 0 errors, tests pass. |
| 4.4 | 2026-03-18 | Hybrid AI Advisory architecture: `RuleBasedScoringEngine.cs` (deterministic scoring), `LLMNarrativeGenerator.cs` (prompt building + OpenAI calls), `HybridAIAdvisoryService.cs` (orchestration + fallback), `AIAdvisorySettings.cs` (config). DI updated with config toggle (`UseLLMNarrative`). appsettings.json updated (API + Web.Intranet). LLM enhances narrative text only — never changes scores or recommendations. Build: 0 errors, tests pass. |
| 4.5 | 2026-03-20 | Fineract Direct API integration: `IFineractDirectService` (schedule preview + customer exposure), `FineractDirectAuthHandler` (Basic Auth + tenant header), `FineractDirectService` (hybrid: Fineract API first, in-house fallback), `MockFineractDirectService` (real financial math). `FineractProductId` added to `LoanProduct` entity/DTOs/admin UI. Migration `20260320100000_AddFineractProductIdToLoanProduct`. Config: `FineractDirect` section in appsettings.json. Products admin page updated with Fineract Product ID field. Build: 0 errors, tests pass. |
| 4.7 | 2026-03-21 | Critical migration bug fix: 4 missing Designer.cs files caused EF Core to skip migrations. `IndustrySector` column absent from DB broke all loan application queries — Detail page fell back to mock data (status "HOReview"), hiding Loan Pack and Offer Letter buttons. Fixed by creating Designer.cs files, updating model snapshot, and making migrations idempotent. All 4 migrations now apply on startup. |
| 4.8 | 2026-03-22 | Bug fixes from TabModalReviewReport: (C7) Settings persistence via localStorage — `SaveSettings`/`ResetToDefaults` now read/write `localStorage["userSettings"]`. (C8) Audit trail pagination wired to `SearchAuditLogsHandler` — real `totalCount`/`totalPages`, Previous/Next buttons functional. (C-4) Null-user auth guard in Detail.razor — `EnsureAuthenticated` helper redirects to `/login` on expired session; 15 occurrences replaced. (C-5) Collateral `MarketValue`/`ForcedSaleValue` now fetched from full `CollateralDto` via `GetCollateralByIdHandler` per item. (C-6) Per-item LTV computed from real `loanAmount / acceptableValue`. Build: 0 errors. |
| 5.0 | 2026-03-25 | External bank statement transaction pipeline complete: `StatementFileParserService` (CSV/Excel auto-parsing, 18 date formats, 40+ column aliases), `ManageStatementTransactionsModal` (entry grid + live reconciliation), `AddStatementTransactionsAsync` + `ValidateDataIntegrity()` call (unblocks Verify + Analyze), `InputFile` on upload modal + format guide panel, Help page Bank Statements section rewritten (5 sections: workflow, format guide, export instructions, troubleshooting). |
| 5.1 | 2026-03-25 | `ManageStatementTransactionsModal` save button hardening: (1) `ToString("N2")` → `ToString("F2", CultureInfo.InvariantCulture)` on number inputs — comma-formatted strings were silently rejected by browser, causing preloaded amounts to appear empty and `CanSave = false`; (2) `StateHasChanged()` after `isSaving = true` — spinner now appears immediately; (3) `catch (Exception ex)` block — unhandled exceptions now show error instead of crashing Blazor circuit; (4) `min-height: 0` on modal body div — prevents footer clipping in flex layouts; (5) disabled-state hint below Save button. |
| 4.9 | 2026-03-22 | M-series + L-series bug fixes (all 19 M + all 8 L). M: FSV validation, committee min-approval guard, auth in SetupCommitteeModal, configurable collateral haircuts (IOptions<CollateralHaircutSettings>), FillPartyInfoModal IsValid fix, vote range validation, offer letter enum status check, BankSettings config, balance sheet validation, UpdateCollateralHandler/UpdateGuarantorHandler (new), admin pages SystemAdmin authorization, LocationId on UserSummary/Users, committee FinalDecision, overdue null SLADueAt guard, audit free-text search through all layers, dashboard demo data removed. L: AppStatus constants class — all status strings in Detail.razor replaced (CanManageValuation, CanManageGuarantors, FormatStatus, GetStatusBadgeClass, action buttons); client-side pagination (page size 15) added to Users/Products/Templates/Committees admin pages; Help page search now filters via HelpNavItems list (40 entries); AddComment loading state + error handling (CommentsTab IsSubmitting/SubmitError params); calendar month calculation fix in UploadExternalStatementModal. Build: 0 errors. |
| 4.6 | 2026-03-20 | Offer letter generation: `OfferLetter` aggregate (versioning, schedule summary). `GenerateOfferLetterCommand` + handler. `OfferLetterPdfGenerator` (QuestPDF: facility details, full repayment schedule table, conditions, acceptance section). `OfferLetterRepository` + EF config. Migration `20260320110000_AddOfferLettersTable`. Detail.razor "Offer Letter" button (Approved/Disbursed). Help page updated: new "Offer Letter" section, Operations workflow updated with offer letter step, Approved status card updated. Build: 0 errors. |
| 5.2 | 2026-03-25 | Offer letter download + history: `GetAllByLoanApplicationIdAsync` on `IOfferLetterRepository`/`OfferLetterRepository`. New `GetOfferLettersByApplicationQuery` + handler. DI registered. `OfferLetterInfo` model. `ApplicationService.GetOfferLettersByApplicationAsync` + `DownloadOfferLetterAsync(offerLetterId)` (looks up `StoragePath` from DB, streams via `IFileStorageService`). Detail.razor "Offer Letters" tab (Approved/Disbursed only) — version badge, filename, size, status badge, who generated, when, per-row download with spinner. After generate: list auto-refreshes. `FormatFileSize` helper added. Build: 0 errors. |
| 5.4 | 2026-04-03 | Lifecycle gap fixes G4/G6/G7/G9. G6: persistent `CreditCheckOutbox` DB table replaces in-memory `Channel`; `ApproveBranchHandler` atomically commits outbox entry with branch approval; background service polls DB, retries 3×, marks Completed/Failed. G7: `FinalApproval` status fully wired — `MoveToFinalApproval()` domain method; `CommitteeDecisionWorkflowHandler` auto-transitions CommitteeApproved→FinalApproval; workflow seeder updated; `Detail.razor` gates on `FinalApprover` role. G9: `Guid.Empty` replaced throughout `WorkflowIntegrationHandlers.cs` — committee decisions use `domainEvent.DecisionByUserId` (chairman's real ID); system-driven transitions use `SystemConstants.SystemUserId`; `ApplicationService` resolves `SystemConstants.SystemUserId` → "System Process" in workflow history. G4: `BureauTab.razor` consent-blocked banner (lists party names, directs to Re-run Credit Checks); business report card footer now shows ConsentRequired/Failed/NotFound badges (previously missing). Build: 0 errors. |
| 5.7 | 2026-04-09 | Session 46: Full disbursement checklist feature (post-approval pre-disbursement). Domain: `DisbursementChecklistItem` state machine (CP/CS, 9 statuses, 6 role-based operations). Application: `ConfirmOfferAcceptanceCommand`, `GetDisbursementChecklistQuery`, 8 checklist item-action handlers, 3 admin template management handlers, `LoanPackData.ApprovalConditions` added. Infrastructure: `DisbursementMemoPdfGenerator` (QuestPDF: loan summary, CP table, CS table, signature block), `CsMonitoringBackgroundService` (daily cycle, `MarkOverdue()`, tiered T-7/T-1/T+0/T+7/T+30/T+90 warnings, EF join query), all 13 handlers + hosted service registered in DI. UI: `OfferAcceptanceTab.razor` (NEW) — CP/CS tables, summary banner, single-modal state machine for all 7 actions, role-filtered buttons; `Detail.razor` updated — hardcoded `cpChecklist` removed, "Disbursement Checklist" tab added (OfferGenerated/OfferAccepted/Disbursed), `ShowRecordAcceptanceButton` now Operations/SystemAdmin only, CP gate moved to domain; `LoanPackPdfGenerator` Section 12 "Conditions of Approval" added; `ApplicationService` — 8 new methods + `IssueOfferLetterAsync` seeds checklist + `RecordOfferAcceptanceAsync` calls `ConfirmOfferAcceptanceHandler`; `ApplicationModels.cs` — 3 new model classes. Migration `20260409123746_AddDisbursementChecklist` created (PENDING — apply with `dotnet ef database update`). Pending: admin UI for checklist template management on `/admin/products`. Build: 0 errors. |
| 5.6 | 2026-04-03 | G11: Credit check retry gaps (residual after G6). (1) `RecoverOrphanedEntriesAsync()` added to `CreditCheckBackgroundService` — on startup resets any stuck `Processing` entries to `Pending` (crash recovery). (2) `ProcessedAt` set on terminal failure in both result-failure and exception paths. (3) `IBureauReportRepository.Delete` + `BureauReportRepository.Delete` added. (4) `ProcessLoanCreditChecksCommand` — `Failed` reports are now retryable: `alreadyCountedBvns`/`alreadyCountedBusiness` sets track BVNs whose `RecordCreditCheckCompleted` was already called; `Failed` reports deleted before retry loop; `RecordCreditCheckCompleted` skipped for retried-but-already-counted checks to prevent double-counting. (5) `CanRerunCreditChecks` in `Detail.razor` expanded to show for `Failed` reports (previously `ConsentRequired` only). Build: 0 errors, 0 warnings. |
| 5.5 | 2026-04-03 | G10: Committee deferral dual-status desync + no UI indicator. (1) `LoanApplication.DeferFromCommittee(userId, rationale)` domain method added — transitions `CommitteeCirculation → HOReview`, writes status history. (2) `CommitteeDecisionRecordedEvent` gains `string? Rationale` field; `RecordDecision()` passes rationale into event. (3) `CommitteeDecisionWorkflowHandler` Deferred case now calls `DeferFromCommittee()` with error guard — fixes permanent dual-status desync where `LoanApplication.Status` was never updated. (4) `Detail.razor` shows yellow deferral banner at `HOReview` status when `Committee.Decision == "Deferred"` — displays date and committee rationale inline. No migration. Build: 0 errors. |
| 5.3 | 2026-03-26 | Submit for Review end-to-end bug fixes (4 bugs). BUG-1: `BankStatementRepository.Update()` — new `StatementTransaction` entities tracked as `Modified` not `Added` by EF Core graph traversal → INSERTs silently dropped → "Failed to add transactions". Fixed by capturing Detached transactions before `Update()` and re-marking as `Added`. BUG-2: `ValidateForSubmission()` checked `application.Documents` (wrong aggregate — LoanApplicationDocument) instead of `application.BankStatements`; domain `Submit()` made same wrong-collection check → always returned failure. Fixed: UI now checks `BankStatements.Any()`; domain check removed (cross-aggregate validation moved to handler). BUG-3: `SubmitForReview()` had no else branch — failure result silently ignored, no feedback shown. Fixed: `submitError` field + alert in modal body. BUG-4: `LoanApplicationRepository.Update()` had same EF Core tracking issue — new `LoanApplicationStatusHistory` added by `AddStatusHistory()` tracked as Modified → DB exception → "failed to submit application". Fixed with same pattern as BUG-1. Build: 0 errors. |
