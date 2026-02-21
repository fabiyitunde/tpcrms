# CRMS — Session Handoff Document

**Last Updated:** 2026-02-21 (updated end-of-session #2)
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

### What Works (as of 2026-02-21)

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

### What Is Pending

| Feature | Priority | Notes |
|---|---|---|
| Credit bureau check UI | ⏸️ On Hold | Provider change pending — do not implement yet |
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
- Directors and Signatories are **auto-fetched from core banking at application creation** — PartiesTab is intentionally read-only, never add Add/Edit modals for these

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
├── UploadCollateralDocumentModal.razor    ← NEW: upload docs to collateral
├── AddGuarantorModal.razor
├── EditGuarantorModal.razor
├── ViewGuarantorModal.razor
├── UploadDocumentModal.razor
├── FinancialStatementModal.razor
└── UploadFinancialStatementModal.razor
```

### Tabs Directory
```
src/CRMS.Web.Intranet/Components/Pages/Applications/Tabs/
├── CollateralTab.razor     ← params: CanManageValuation, OnSetValuation, OnApproveCollateral, OnUploadDocument
├── DocumentsTab.razor      ← params: OnVerifyDocument, OnRejectDocument
├── GuarantorsTab.razor     ← params: CanManageGuarantors, OnApproveGuarantor, OnRejectGuarantor
├── FinancialsTab.razor
├── PartiesTab.razor        ← read-only; directors/signatories from core banking
└── ...
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

## 5. Last Session Summary (2026-02-21)

### Completed
1. **Collateral Document Management** (full CRUD)
   - Created `ICollateralDocumentRepository` interface and `CollateralDocumentRepository` implementation
   - Created `UploadCollateralDocumentCommand` + `UploadCollateralDocumentHandler` in `CollateralCommands.cs`
   - Created `DeleteCollateralDocumentCommand` + `DeleteCollateralDocumentHandler`
   - Added `RemoveDocument()` method to `Collateral` aggregate
   - Added `UploadCollateralDocumentAsync()` and `DeleteCollateralDocumentAsync()` to `ApplicationService.cs`
   - Created `UploadCollateralDocumentModal.razor` with document type selection, file upload
   - Updated `ViewCollateralModal.razor`: added DOCUMENTS section with view/download/delete buttons + delete confirmation dialog
   - Updated `CollateralTab.razor`: added `OnUploadDocument` param and upload button (visible in Draft or review stages)
   - Updated `Detail.razor`: wired upload modal state, handlers, and `OnCollateralDocumentDeleted` callback
   - Added API endpoints: `/api/collateral-documents/{id}/view` and `/api/collateral-documents/{id}/download`
   - Added DTOs: `UploadCollateralDocumentRequest`, `CollateralDocumentResult`, `CollateralDocumentInfo`
   - Registered `UploadCollateralDocumentHandler`, `DeleteCollateralDocumentHandler`, `ICollateralDocumentRepository` in DI
   - Delete removes **both** database record AND file from storage
   - Delete available when: `IsApplicationEditable` (Draft) OR `CanManageValuation` (review stages)
   - Delete NOT available when: Approved, CommitteeApproved, Rejected, Disbursed

### Key Files Changed
- `src/CRMS.Domain/Interfaces/ICollateralRepository.cs` — added `ICollateralDocumentRepository`
- `src/CRMS.Domain/Aggregates/Collateral/Collateral.cs` — added `RemoveDocument()`
- `src/CRMS.Application/Collateral/Commands/CollateralCommands.cs` — added upload/delete handlers
- `src/CRMS.Infrastructure/Persistence/Repositories/CollateralRepository.cs` — added `CollateralDocumentRepository`
- `src/CRMS.Infrastructure/DependencyInjection.cs` — registered new handlers and repository
- `src/CRMS.Web.Intranet/Program.cs` — added collateral document API endpoints
- `src/CRMS.Web.Intranet/Services/ApplicationService.cs` — added upload/delete methods
- `src/CRMS.Web.Intranet/Services/ApplicationServiceDtos.cs` — added DTOs
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Modals/UploadCollateralDocumentModal.razor` — NEW
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Modals/ViewCollateralModal.razor` — added docs section + delete confirmation
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Tabs/CollateralTab.razor` — added upload button
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Detail.razor` — wired upload modal

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v3.2
- [x] `docs/ImplementationTracker.md` → v2.8

---

## 6. Suggested Next Task

### User Management CRUD (`/admin/users`)

The page currently shows a read-only list of users. The backend handlers exist — this is UI work only.

**What to build:**
- "Create User" button → modal with: name, email, role dropdown, branch
- "Edit" button per row → modal pre-filled with user data
- "Deactivate" button per row → confirmation modal

**Step 1 — Check existing Application layer handlers:**
```
src/CRMS.Application/Identity/Commands/  ← look for CreateUserHandler, UpdateUserHandler, DeactivateUserHandler
```

**Step 2 — Add methods to `ApplicationService.cs`:**
```csharp
public async Task<ApiResponse> CreateUserAsync(CreateUserRequest request)
public async Task<ApiResponse> UpdateUserAsync(Guid userId, UpdateUserRequest request)
public async Task<ApiResponse> DeactivateUserAsync(Guid userId)
```

**Step 3 — Create modals:**
```
src/CRMS.Web.Intranet/Components/Pages/Admin/Modals/
├── CreateUserModal.razor
└── EditUserModal.razor
```

**Step 4 — Update `/admin/users` page (`src/CRMS.Web.Intranet/Components/Pages/Admin/Users.razor`):**
- Add modal state variables and event handlers in `@code`
- Wire up Create/Edit/Deactivate buttons

**Template to follow:** `AddGuarantorModal.razor` (Add) and collateral approve confirmation modal (Deactivate).

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
