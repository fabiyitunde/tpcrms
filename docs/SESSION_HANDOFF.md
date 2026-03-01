# CRMS — Session Handoff Document

**Last Updated:** 2026-03-01 (Session 7)
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

### What Works (as of 2026-03-01)

| Feature Area | Status |
|---|---|
| Create new application (auto-fetches directors/signatories from core banking) | ✅ |
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
| **Credit Bureau UI (SmartComply)** | ✅ |

### What Is Pending

| Feature | Priority | Notes |
|---|---|---|
| Bank Statement tab (view, analyze, upload external) | P2 | ✅ UI built; `GetBankStatementsAsync`, `UploadExternalStatementAsync`, `VerifyStatementAsync`, `RejectStatementAsync`, `AnalyzeStatementAsync` all wired |
| Editable fallback for null core-banking fields | P2 | ✅ Done — RC number + IncorporationDate in New.razor; BVN + shareholding % in PartiesTab via FillPartyInfoModal |
| Bank statement auto-fetch at creation | P2 | ✅ Done — `InitiateCorporateLoanCommand` now persists CoreBanking statement on application create |
| User management CRUD | P3 | Currently display-only (`/admin/users`) |
| Product edit / delete | P3 | Create works; edit/delete missing (`/admin/products`) |
| Scoring config editor | P3 | Display-only (`/admin/scoring`) |
| Connect report pages to ReportingService | P3 | Performance/Committee pages show mock data |

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
- Directors and Signatories are **auto-fetched from core banking at application creation** — PartiesTab is intentionally read-only for structure; null fields (BVN, shareholding %) can be filled via FillPartyInfoModal (Draft only)

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
├── UploadExternalStatementModal.razor     ← NEW: upload other-bank statement
└── FillPartyInfoModal.razor               ← NEW: fill null BVN/shareholding for a party
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

## 5. Last Session Summary (2026-03-01 Session 7)

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

### Option A — End-to-End Test the Session 7 Features

Before moving to admin pages, run the app and manually test:

1. Create application with account `1234567890` → open Statements tab → should show 1 CoreBanking statement
2. Upload an external statement → appears in "Other Banks" section as Pending Review
3. Click Verify on external statement → status changes to Verified (trust 85%)
4. Click Reject on external statement → reason modal → status changes to Rejected
5. Open Parties tab on a Draft application → check "Complete info" button for directors with null BVN
6. Fill in BVN → save → button disappears
7. On New Application page, use account that has null RC number → verify editable field appears

---

### Option B — Fix Remaining Medium Issues (code quality, from Session 5 review)

1. **M-1**: Add EF indexes on `ConsentRecords.BVN` and `ConsentRecords.NIN`
2. **M-2**: Configure `BureauReport.ConsentRecordId` in `BureauReportConfiguration.cs`
3. **M-3**: Migrate `RequestBureauReportCommand` to use `ISmartComplyProvider` instead of legacy `ICreditBureauProvider`
4. **M-4**: Add distributed/DB lock on `LoanApplicationId` in `ProcessLoanCreditChecksCommand`
5. **M-5**: Rename `BureauReport.NonPerformingAccounts` → `DelinquentFacilities`

---

### Option C — User Management CRUD (`/admin/users`)

The Users page currently only displays users. Add full CRUD:

**What to build:**
- "Create User" button → modal with: name, email, role dropdown, branch
- "Edit" button per row → modal pre-filled with user data
- "Deactivate" button per row → confirmation modal

**Backend:** Handlers likely exist in `Application/Identity/Commands/`. Check and register if needed.

**Template to follow:** `AddGuarantorModal.razor` (Add) and collateral approve confirmation modal (Deactivate).

---

### Option D — Product Edit/Delete (`/admin/products`)

Create works; add edit/delete functionality.

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
```

`MSB3026` / `MSB3021` errors = app is running and holding DLL locks. **Not real errors.** Only `error CS` lines are compiler errors.

---

## 8. Mock Data Reference

Core banking mock only has data for account `1234567890` ("Acme Industries Ltd"):
- 3 directors: John Adebayo (40%), Amina Ibrahim (35%), Chukwuma Okonkwo (25%)
- 2 signatories: MD (Class A), Finance Director (Class B)

Any other account number returns an empty directors/signatories list. Use `1234567890` when testing the New Application flow end-to-end.

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
