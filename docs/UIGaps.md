# CRMS Intranet UI - Gap Analysis

**Document Version:** 3.4
**Date:** 2026-03-01
**Status:** Priority 1 RESOLVED | Priority 2 RESOLVED | Priority 3 Pending

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
| Run credit check | ❌ Pending | On hold pending credit bureau provider decision |
| View credit report | ❌ Pending | No detail modal |

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
| Request check | ⏸️ On Hold | Manual re-check button not yet implemented |
| View report detail | ❌ Pending | No detail modal (click to expand) |

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
| Upload external statement | ✅ Done | `UploadExternalStatementModal` — bank name, account, period, balances |
| Verify external statement | ✅ Done | Inline button; calls `VerifyStatementAsync`; trust weight → 85% |
| Reject external statement | ✅ Done | Reason modal; calls `RejectStatementAsync` |
| Trigger cashflow analysis | ✅ Done | "Analyze" button per statement; calls `AnalyzeStatementAsync` |
| Cashflow metrics display | ✅ Done | Credits, Debits, Avg Balance, Net Cashflow, Bounced/Gambling tx counts |

---

## 3. Remaining Issues (Priority 3)

### 3.1 Admin Pages

| Page | Route | Status | Issues |
|------|-------|--------|--------|
| Scoring Config | `/admin/scoring` | Display Only | No edit functionality |
| Templates | `/admin/templates` | Display Only | No CRUD operations |
| Users | `/admin/users` | Display Only | No create/edit/deactivate |
| Products | `/admin/products` | Partial | Create works, edit/delete missing |

### 3.2 Report Pages

| Page | Route | Status | Issues |
|------|-------|--------|--------|
| Performance | `/reports/performance` | Mock Data | Not connected to `ReportingService` |
| Committee | `/reports/committee` | Mock Data | Not connected to backend |
| Audit | `/reports/audit` | Partial | Connected but limited filtering |

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

### Priority 3 (Admin & Reports)
1. User management CRUD (create, edit, deactivate)
2. Product edit/delete
3. Scoring configuration editor
4. Template management CRUD
5. Connect performance/committee report pages to `ReportingService`

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
