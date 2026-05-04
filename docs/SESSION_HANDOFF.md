# CRMS — Session Handoff Document

**Last Updated:** 2026-05-04 (Session 55)
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

### What Works (as of 2026-04-14)

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
| **Offer Acceptance modal — KFS acknowledgement, acceptance method enum, customerSignedAt** | ✅ |
| **OfferAcceptanceMethod enum (InBranchSigning / Courier / Electronic) + EF migration** | ✅ |
| **Loan Pack PDF — full data (approved terms, bureau active loans table, committee decision vote tally + member breakdown)** | ✅ |
| **Loan Pack button — visible to all workflow actors from HOReview onwards (role + status guard)** | ✅ |
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
| **Template management CRUD (`/admin/templates`) — create/edit/toggle/preview, wired to real backend** | ✅ |
| **Bureau report detail modal (click to expand) — accounts, fraud risk, alerts** | ✅ |
| **Hybrid AI Advisory (rule-based scoring + optional LLM narrative generation)** | ✅ |
| **Fineract Direct API integration (Basic Auth + tenant header)** | ✅ |
| **Repayment schedule preview (hybrid: Fineract API first, in-house fallback)** | ✅ |
| **Customer exposure via Fineract (clientId → active loans → outstanding balances)** | ✅ |
| **FineractProductId mapping on LoanProduct (admin-editable, optional)** | ✅ |
| **Offer letter PDF generation with proposed repayment schedule** | ✅ |
| **OfferLetter domain entity with versioning and schedule summary** | ✅ |
| **Offer Letter button on Detail page (Approved/Disbursed status)** | ✅ |
| **Help & Guide page updated with Offer Letter section** | ✅ |
| **Mock data scoped to `admin` user only — all other users see real DB data** | ✅ |
| **TabModalReview C1-C5: ExecuteAction/VerifyDocument/VerifyStatement error feedback wired** | ✅ |
| **TabModalReview C2: RequestBureauCheck wired to ProcessLoanCreditChecksCommand** | ✅ |
| **TabModalReview C5: Financial Statement ×1000 on create path fixed** | ✅ |
| **Settings page persistence via localStorage (C7)** | ✅ |
| **Audit trail pagination + search wired to SearchAuditLogsHandler (C8)** | ✅ |
| **AuthService.ChangePasswordAsync + UpdateLocalUserAsync wired to real backend (C6)** | ✅ |
| **Profile page: SaveProfile + ChangePassword use real handlers, no Task.Delay stubs** | ✅ |
| **Null-user auth guard on all workflow actions in Detail.razor (C-4)** | ✅ |
| **Collateral MarketValue/ForcedSaleValue correctly mapped from full CollateralDto (C-5)** | ✅ |
| **Per-item LTV calculated from real loan amount and acceptable value (C-6)** | ✅ |
| **External bank statement transaction entry — `ManageStatementTransactionsModal` with live reconciliation** | ✅ |
| **CSV/Excel bank statement file parsing — `StatementFileParserService` auto column detection, 18 date formats** | ✅ |
| **Upload modal collapsible format guide panel (column name table, sample header, link to Help)** | ✅ |
| **Help page Bank Statements section rewritten — format guide, bank export instructions, troubleshooting** | ✅ |
| **`AddStatementTransactionsAsync` + `ValidateDataIntegrity()` call — enables Verify and Analyze after entry** | ✅ |
| **Offer letter download + history tab (re-download any version, per-row spinner, empty state)** | ✅ |
| **"Add Txns" on existing statement shows prior transactions read-only + correct running balance** | ✅ |
| **`ManageStatementTransactionsModal` — 4 UX/functional bugs fixed (bind clobber, partial save, premature warning, misleading labels)** | ✅ |
| **G6: Credit check outbox — persistent DB table replaces in-memory Channel (survives restarts)** | ✅ |
| **G7: `FinalApproval` status wired — CommitteeApproved auto-transitions; FinalApprover role gates UI** | ✅ |
| **G9: `Guid.Empty` replaced — chairman ID from `DecisionByUserId`; system actions use `SystemConstants.SystemUserId`; display resolves to "System Process"** | ✅ |
| **G4: Bureau tab consent-blocked banner + business card status badges (Consent Required / Failed / Not Found)** | ✅ |
| **G10: Committee deferral — `DeferFromCommittee()` domain method; dual-status desync fixed; deferral banner at HOReview** | ✅ |
| **G11: Credit check retry — orphaned `Processing` recovery on startup; `ProcessedAt` on failure; `Failed` reports retryable without double-counting; "Re-run" button visible for `Failed`** | ✅ |
| **Workflow CreditAnalysis stage fully wired — Credit Officer sees Approve + Return buttons; approval no longer throws exception; Return sends app back to BranchReview** | ✅ |
| **4 missing handler DI registrations added: `ApproveCreditAnalysisHandler`, `ReturnFromCreditAnalysisHandler`, `ReturnFromHOReviewHandler`, `FinalApproveHandler`** | ✅ |
| **HOReview → CommitteeCirculation domain desync fixed — `MoveToCommitteeHandler` registered; `ApproveApplicationAsync` now calls it so `LoanApplication.Status` stays in sync with `WorkflowInstance.CurrentStatus`** | ✅ |
| **Disbursement Checklist (post-approval pre-disbursement) — admin-configurable CP/CS items, full state machine, role-based actions, Disbursement Memo PDF, CS background monitoring** | ✅ |
| **LoanPack PDF — Section 12 "Conditions of Approval" from committee decision appended when present** | ✅ |
| **Stale-state fix — `StateHasChanged()` added to `LoadApplication()` in Detail.razor; covers all 20+ action handlers** | ✅ |
| **Document viewer modal height increased to 92vh; body uses `flex: 1; min-height: 0` to fill space** | ✅ |
| **SetupCommitteeModal: `show` class added to backdrop, `.modal` nested inside backdrop, `@onclick:stopPropagation` added** | ✅ |
| **Business proposal: `docs/CRMS_Proposal.md` (24-slide PPT-ready) and `docs/CRMS_Proposal.html` (standalone HTML)** | ✅ |
| **Configurable CollateralTypeConfig (`/admin/collateral-types`) — CRUD, haircut rate, valuation basis, activate/deactivate** | ✅ |
| **11 default collateral types seeded (CashDeposit 0%, FixedDeposit 5%, RealEstate 20%/FSV, Vehicle 30%/FSV, etc.)** | ✅ |
| **AddCollateralModal — dynamic type dropdown from DB; haircut info callout; indicative value field; formal valuation removed from this modal** | ✅ |
| **SetCollateralValuationModal — valuation basis selector (MarketValue/FSV), valuer details, AcceptableValue auto-computed with haircut** | ✅ |
| **CanManageValuation — now role-gated (CreditOfficer/CreditManager/HOReviewer/SystemAdmin only); Loan Officer can no longer see valuation buttons** | ✅ |
| **Approval Gate — per-stage ratification gate (strict/flexible mode), `ApprovalGateModal` (hard block / override note / soft warning), override banners on Detail page, inline rejection notes on all 5 item tabs (Documents, Statements, Financials, Collateral, Guarantors), FinancialStatement Verify/Reject buttons, `ApprovalOverrideRecord` entity + repository + EF migration** | ✅ |
| **Bureau tab business card always renders (no longer vanishes when no report exists — shows placeholder with guidance)** | ✅ |
| **`CanRerunCreditChecks` extended — detects absent individual/business reports (directors with BVN but no bureau record; RC number present but no business report)** | ✅ |
| **SmartComply RC prefix normalization in `SmartComplyProvider.GetBusinessCreditReportAsync` — bare numerics prefixed with "RC" before API call** | ✅ |
| **`ProcessLoanCreditChecksCommand` idempotency gate extended — `hasMissingIndividualReport` + `hasMissingBusinessReport` checks prevent early return when reports are absent** | ✅ |
| **`CheckApprovalGateQuery` CreditAnalysis gate — bank statements now checked at CreditAnalysis stage; gate checks `AnalysisStatus == Completed` (not `VerificationStatus`)** | ✅ |
| **Collateral indicative value displayed in `CollateralTab` — shows "Indicative — pending valuation" label when no formal valuation exists** | ✅ |
| **`RefetchInternalBankStatementCommand` — deletes existing CoreBanking statement and re-fetches 6 months from today via CBS; `StatementsTab` shows "Refresh from Core Banking" button (with spinner) when `TransactionCount == 0`** | ✅ |
| **`CheckApprovalGateQuery` collateral check split by stage — `Valued` passes at CreditAnalysis; only `Approved`/`Perfected` required at FinalApproval** | ✅ |
| **Collateral document `Description` threaded through DTO chain and displayed in `ViewCollateralModal`; document type promoted to prominent label** | ✅ |
| **Collateral `IndicativeValue` shown in VALUATION section of `ViewCollateralModal`** | ✅ |
| **AI Advisory `Math.Round(decimal, -3)` crash fixed in `RuleBasedScoringEngine` and `MockAIAdvisoryService`** | ✅ |
| **LegalReview/LegalApproval wired into active workflow sequence (HOReview → LegalReview → LegalApproval → CommitteeCirculation)** | ✅ |
| **`ReturnFromLegalReview` domain method + handler — Legal Officer can return application to HOReview** | ✅ |
| **Approval gate at LegalReview — strict mode, blocks on uncleared collateral (`IsLegalCleared == false`)** | ✅ |

### What Is Pending

| Feature | Priority | Notes |
|---------|----------|-------|
| Wire customer exposure into AI Advisory (replace bureau-derived exposure) | P2 | `IFineractDirectService.GetCustomerExposureAsync` ready; needs wiring into `GenerateCreditAdvisoryHandler` to replace/supplement `corporateBureauReport.TotalOutstandingBalance` |
| **Collateral approval — multi-actor role design** | P2 | Currently a single "Approve" button with no role separation. Design decision: requires at minimum Legal clearance (title/encumbrance check) + Credit/Risk Officer adequacy sign-off as two distinct steps. Valuation comes from external certified valuer. Existing state machine (`Proposed → UnderValuation → Valued → Approved → Perfected`) has the right shape but approval roles per step are undefined. Revisit when implementing collateral perfection stage. See `memory/project_collateral_approval.md`. |
| G8: Domain events with no handlers (`LoanApplicationCreatedEvent`, `SubmittedEvent`, `ApprovedEvent`, `DisbursedEvent`) | P3 | Deferred to next sprint — no downstream automation on key lifecycle events |

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
- **Application (full lifecycle — defined in `AppStatus.cs` and `LoanApplicationStatus` enum):**
  `"Draft"` → `"BranchReview"` → `"HOReview"` → `"CreditAnalysis"` → `"HOReview"` → `"LegalReview"` → `"LegalApproval"` → `"CommitteeCirculation"` → `"CommitteeApproved"` → `"FinalApproval"` → `"Approved"` → `"OfferGenerated"` → `"OfferAccepted"` → `"SecurityPerfection"` → `"SecurityApproval"` → `"DisbursementPending"` → `"DisbursementBranchApproval"` → `"DisbursementHQApproval"` → `"Disbursed"` / `"Rejected"` / `"Cancelled"` / `"Closed"`
  - Forward path: `CreditAnalysis` → `HOReview` → `LegalReview` → `LegalApproval` → `CommitteeCirculation`
  - `LegalOfficer` at LegalReview: Submit Opinion (Approve) → LegalApproval; Return → HOReview
  - `HeadOfLegal` at LegalApproval: Approve → CommitteeCirculation; Return → LegalReview
  - `LegalReview` and `LegalApproval` are also defined in the enum/AppStatus but not yet wired into the workflow transition sequence — where exactly they slot in has not been confirmed with the user.

### Access Control Rules
- `IsApplicationEditable` = `application.Status == "Draft"` — data entry (add/edit/delete) only allowed in Draft
- `CanManageValuation` = user has role `CreditOfficer`, `CreditManager`, `HOReviewer`, or `SystemAdmin` **AND** status is NOT `Draft`, `Approved`, `CommitteeApproved`, `Rejected`, or `Disbursed` — valuation/approval is Credit Officer territory; Loan Officers are excluded even during active review
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
        if (!EnsureAuthenticated(out var userId)) return;
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
├── UploadExternalStatementModal.razor     ← upload other-bank statement (InputFile + format guide panel)
├── ManageStatementTransactionsModal.razor ← transaction entry grid with live reconciliation, preload support
├── FillPartyInfoModal.razor               ← fill null BVN/shareholding for a party
├── SetupCommitteeModal.razor              ← auto-routes from standing committee or falls back to ad-hoc
└── ViewBureauReportModal.razor            ← bureau report detail with accounts, fraud, alerts
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
└── BureauTab.razor           ← params: OnViewReport (click to expand detail modal)
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

## 5. Last Session Summary (2026-05-04 Session 55)

### Completed — AI Advisory Score Breakdown / Red Flags Persistence Fix

**Root cause:** `RiskScores`, `RedFlags`, `Conditions`, `Covenants` on `CreditAdvisory` are backed by private `readonly` `List<T>` fields. EF was configured with `builder.Ignore()` for all four — they were never written to or read from the DB. After generation, `ApplicationService.GenerateAdvisoryAsync` discarded the in-memory DTO and reloaded from DB, at which point all four collections were empty.

**Fix — JSON snapshot columns:**
- **Domain** (`CreditAdvisory.cs`): Added `RiskScoresJson`, `RedFlagsJson`, `ConditionsJson`, `CovenantsJson` (`string?`) properties + `SetPersistedData(...)` method
- **Command** (`GenerateCreditAdvisoryCommand.cs`): After `advisory.Complete()` succeeds, serializes all four collections to JSON (anonymous object shape for risk scores) and calls `SetPersistedData()` before `AddAsync`/`SaveChangesAsync`
- **EF Config** (`CreditAdvisoryConfiguration.cs`): Added 4 `builder.Property(...).HasColumnType("text")` mappings for the new columns
- **Mapper** (`GetCreditAdvisoryQueries.cs` → `CreditAdvisoryMapper.ToDto`): Falls back to JSON deserialization when in-memory collections are empty (i.e. loaded from DB); uses `PersistedRiskScore` internal record; recalculates `hasCriticalRedFlags` from resolved lists
- **Migration** (`20260504074944_AddCreditAdvisoryJsonColumns.cs`): Applied — adds 4 `text` columns to `CreditAdvisories` table

**Existing advisory records** (generated before this fix) will still show empty scores — only newly generated advisories will have data. Re-run "Generate AI Advisory" for any affected application.

---

### Previous Session Summary (2026-05-04 Session 54)

### Completed — LegalReview/LegalApproval Fully Wired + Return from LegalReview + Legal Gate

---

#### Feature 1 — LegalReview/LegalApproval confirmed in active workflow chain
Placement confirmed: `HOReview → LegalReview → LegalApproval → CommitteeCirculation`. All backend handlers (`MoveToLegalReviewHandler`, `SubmitLegalOpinionHandler`, `ApproveLegalReviewHandler`, `ReturnFromLegalApprovalHandler`) were already implemented and registered. UI role guards (`LegalOfficer` at LegalReview, `HeadOfLegal` at LegalApproval) were already in place.

#### Feature 2 — `ReturnFromLegalReview`: Legal Officer can return to HOReview
Previously the Legal Officer had no Return button — they could only submit their opinion forward. Added full stack:
- **Domain** (`LoanApplication.cs`): `ReturnFromLegalReview(Guid userId, string reason)` — guards `LegalReview` status, transitions to `HOReview`, adds status history + comment
- **Application** (`SubmitLoanApplicationCommand.cs`): `ReturnFromLegalReviewCommand` + `ReturnFromLegalReviewHandler`
- **DI** (`DependencyInjection.cs`): `ReturnFromLegalReviewHandler` registered
- **`ApplicationService.ReturnApplicationAsync`**: `LegalReview` branch added (before existing `LegalApproval` branch); `LegalReview → HOReview` added to `targetStatus` switch
- **`Detail.razor`** `ShowReturnButton`: `AppStatus.LegalReview → LegalOfficer` entry added
- **Seeder** (`ComprehensiveDataSeeder.cs`): `LegalReview → HOReview` (Return, LegalOfficer) added to both the fresh-seed transitions list and the upgrade block

#### Feature 3 — Approval gate at LegalReview (strict, collateral legal clearance)
The approval gate was entirely absent for Legal stages. Added:
- **`appsettings.json`**: `"LegalReview": { "StrictApprovalGate": true }` — hard block if any collateral is uncleared
- **`CheckApprovalGateQuery.cs`**: `checkLegalClearance = request.Stage is "LegalReview"` — fetches collaterals via `_collateralRepo`, adds a `"Pending"` gate item for every non-rejected collateral where `IsLegalCleared == false`
- `HeadOfLegal` at `LegalApproval` has no gate (nothing new to check at countersignature stage)

---

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [ ] `docs/UIGaps.md` → v6.4 (no UI changes this session)
- [ ] `docs/ImplementationTracker.md` → v7.6 (no new modules this session)

---

## 5. Previous Session Summary (2026-05-04 Session 52)

### Completed — Bureau Fixes, Credit Check Gaps, Approval Gate Gap, Collateral Display, Internal Statement Re-fetch

---

#### Fix 1 — Business Bureau Card Vanishing (`BureauTab.razor`, `Detail.razor`)
Business credit report section was wrapped in `@if (BusinessReports.Any())` — deleting a failed report from DB caused the entire section to disappear. Restructured so the outer `<div>` always renders, with an `@else` placeholder ("No business credit report yet. Use Re-run Credit Checks"). `CanRerunCreditChecks` in `Detail.razor` extended from a simple expression property to a full `get` block that also detects: directors with a BVN but no bureau record, and an RC number present with no completed business report.

---

#### Fix 2 — SmartComply RC Prefix Normalization (`SmartComplyProvider.cs`)
SmartComply API returns `{"status":"failed","message":"Validation failed"}` for bare numeric RC numbers (e.g. `1275857`). Added normalization in `GetBusinessCreditReportAsync`: if the RC doesn't start with "RC" (case-insensitive), prefix it. DB record also corrected directly.

---

#### Fix 3 — Idempotency Gate Missing Report Detection (`ProcessLoanCreditChecksCommand.cs`)
When all existing reports were `Completed` but a report was absent (e.g. business report deleted for retry), the gate's `AllCreditChecksCompleted = true` check caused an early return before running the missing check. Added `hasMissingIndividualReport` (BVN with no bureau record) and `hasMissingBusinessReport` (RC number with no completed business report) guards before the early return.

---

#### Fix 4 — Bank Statement Gate at CreditAnalysis (`CheckApprovalGateQuery.cs`)
`checkBankStatements` was scoped to `BranchReview | HOReview | FinalApproval`, omitting `CreditAnalysis`. Credit Officers could approve without analyzing bank statements. Added `CreditAnalysis` to the scope. At CreditAnalysis the check uses `AnalysisStatus != Completed` (not `VerificationStatus`, which is always `Verified` for internal CoreBanking statements).

---

#### Fix 5 — Collateral Indicative Value Display (`ApplicationModels.cs`, `ApplicationService.cs`, `CollateralTab.razor`)
`IndicativeValue` entered at collateral creation was never mapped through to `CollateralInfo` — the field existed in the domain and DTO but was dropped at the service mapping layer. Added `IndicativeValue` to `CollateralInfo`, added `IndicativeValue = c.IndicativeValue` in `GetCollateralsForApplicationAsync`, and updated `CollateralTab` "Acceptable Value" column to show indicative value with a "Indicative — pending valuation" label when no formal market value exists.

---

#### Feature — Internal Bank Statement Re-fetch (`RefetchInternalBankStatementCommand.cs` + UI)
New command: `RefetchInternalBankStatementCommand` in `src/CRMS.Application/StatementAnalysis/Commands/`. Deletes existing `CoreBanking`-sourced statement(s) for the application, then re-fetches from CBS using a 6-month window from today (temporary — originally designed as 6 months from `application.CreatedAt` but CBS doesn't support backdated transactions in test environment; easy to revert by changing `DateTime.UtcNow` → `loanApp.CreatedAt`).
- Handler registered in `DependencyInjection.cs`
- `ApplicationService.RefetchInternalBankStatementAsync` added
- `StatementsTab.razor`: "Refresh from Core Banking" button shown when `TransactionCount == 0 && CanAnalyzeStatements`; button disables itself and shows spinner (`isRefetching` field + `HandleRefetch()` method) to prevent double-clicks
- `Detail.razor`: `OnRefetch="RefetchInternalStatement"` wired; `RefetchInternalStatement()` calls service and reloads

---

#### Design Discussion — Guarantor Approval Roles (No Code Written)
Confirmed: Credit Officer owns final approval/rejection of guarantors in Nigerian commercial banking (verifies financial capacity, reviews bureau check, assesses net worth vs loan exposure). Branch/RM initiates and collects. Legal team separately signs off on guarantee deed enforceability in some banks — this additional step is deferred. Current UI (approve/reject buttons visible to CreditOfficer on GuarantorsTab) is operationally correct as-is.

---

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v6.3
- [x] `docs/ImplementationTracker.md` → v7.5

---

## 5. Previous Session Summary (2026-04-30 Session 51)

### Completed — Approval Gate Feature (Full Implementation)

Full end-to-end implementation of the per-stage approval gate as designed in `memory/project_approval_gate.md`. Build: 0 errors.

---

#### Config & Settings
- `appsettings.json` — added `WorkflowApprovalGates` section (BranchReview/HOReview = flexible, CreditAnalysis/FinalApproval = strict)
- `src/CRMS.Infrastructure/Workflow/WorkflowApprovalGateSettings.cs` — settings POCO + `ApprovalGateConfig` implementing `IApprovalGateConfig`
- `src/CRMS.Application/Workflow/Interfaces/IApprovalGateConfig.cs` — interface for Application layer

#### Domain
- `src/CRMS.Domain/Aggregates/LoanApplication/ApprovalOverrideRecord.cs` — new entity (fields: LoanApplicationId, Stage, ActorId, ActorName, NoteText, CreatedAt, IsResolved, ResolvedAt)
- `src/CRMS.Domain/Aggregates/LoanApplication/LoanApplication.cs` — added `_overrideRecords` collection + `OverrideRecords` property
- `src/CRMS.Domain/Interfaces/IApprovalOverrideRepository.cs` — repository interface

#### Infrastructure
- `src/CRMS.Infrastructure/Persistence/Repositories/ApprovalOverrideRepository.cs` — repository implementation
- `src/CRMS.Infrastructure/Persistence/Configurations/LoanApplication/LoanApplicationConfiguration.cs` — `ApprovalOverrideRecordConfiguration` + `HasMany(x => x.OverrideRecords)` relation
- `src/CRMS.Infrastructure/Persistence/CRMSDbContext.cs` — `DbSet<ApprovalOverrideRecord>` added
- EF migration `AddApprovalOverrideRecords` — created and applied

#### Application Layer
- `src/CRMS.Application/Workflow/Queries/CheckApprovalGateQuery.cs` — gate check handler (checks documents, bank statements, financial statements, collateral, guarantors per stage scope)
- `src/CRMS.Application/Workflow/Commands/SaveApprovalOverrideCommand.cs` — save override note + `GetApprovalOverridesHandler`
- `src/CRMS.Application/FinancialAnalysis/Commands/FinancialStatementCommands.cs` — added `RejectFinancialStatementCommand` + handler
- `src/CRMS.Application/FinancialAnalysis/DTOs/FinancialStatementDtos.cs` — added `RejectionReason` to `FinancialStatementSummaryDto`
- `src/CRMS.Application/FinancialAnalysis/Queries/FinancialStatementQueries.cs` — updated mapping to include `RejectionReason`
- `src/CRMS.Application/StatementAnalysis/DTOs/StatementAnalysisDtos.cs` — added `VerificationNotes` to `BankStatementSummaryDto`
- `src/CRMS.Application/StatementAnalysis/Queries/GetStatementQuery.cs` — updated mapping to include `VerificationNotes`
- `src/CRMS.Application/Guarantor/DTOs/GuarantorDtos.cs` — added `RejectionReason` to `GuarantorSummaryDto`
- `src/CRMS.Application/Guarantor/Queries/GuarantorQueries.cs` — updated mapping to include `RejectionReason`
- `src/CRMS.Infrastructure/DependencyInjection.cs` — registered all new handlers + `IApprovalGateConfig` + `WorkflowApprovalGateSettings`

#### UI — Models & Service
- `src/CRMS.Web.Intranet/Models/ApplicationModels.cs` — added `ApprovalGateResultModel`, `GateItemModel`, `ApprovalOverrideInfo`; added `RejectionReason` to `DocumentInfo`, `CollateralInfo`, `FinancialStatementInfo`, `GuarantorInfo`; added `VerificationNotes` to `BankStatementInfo`
- `src/CRMS.Web.Intranet/Services/ApplicationService.cs` — added `CheckApprovalGateAsync`, `SaveApprovalOverrideAsync`, `GetApprovalOverridesAsync`, `VerifyFinancialStatementAsync`, `RejectFinancialStatementAsync`; updated all DTO→model mappings to include new fields

#### UI — Components
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Modals/ApprovalGateModal.razor` — new modal (3 modes: hard block / override note required / soft warning)
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Detail.razor` — gate check wired in `HandleApproveClick`; override banners rendered above tabs; `showRejectFinancialModal`, `VerifyFinancialStatement`, `ShowRejectFinancialStatementModal`, `CanVerifyFinancials` added; override records loaded in `LoadApplication()`
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Tabs/DocumentsTab.razor` — inline rejection reason shown under Rejected status badge
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Tabs/StatementsTab.razor` — inline `VerificationNotes` shown under Rejected badge
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Tabs/FinancialsTab.razor` — inline `RejectionReason`, new `CanVerifyFinancials` parameter, Verify/Reject buttons per year
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Tabs/CollateralTab.razor` — inline `RejectionReason` shown under Rejected badge
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Tabs/GuarantorsTab.razor` — inline `RejectionReason` shown under Rejected badge

---

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v6.2
- [x] `docs/ImplementationTracker.md` → v7.4

---

## 5. Previous Session Summary (2026-04-15 Session 49)

### Completed — Maker-Checker Enforcement + UX Improvements + Design Decisions

---

#### Fix 1 — Statement Verification Role Gating (StatementsTab.razor + Detail.razor)

Loan officers should not verify statements they collected (maker-checker). Added `CanVerifyStatements` parameter (bool) to `StatementsTab.razor`, gating:
- External statement Verify + Reject buttons
- External statement Analyze button
- Own bank statement Analyze button

`CanVerifyStatements` computed in `Detail.razor` — same allowed roles as `CanVerifyDocuments`: BranchApprover, CreditOfficer, HOReviewer, FinalApprover, RiskManager, SystemAdmin.

---

#### Fix 2 — Valuation Info Callout (AddCollateralModal.razor)

Added an info callout below the "VALUATION (Optional)" heading explaining that valuation is done later by an independent certified valuer, and that liquid assets (cash deposits, FDs, T-bills) may have their face value entered directly.

---

#### Fix 3 — AI Advisory Locked During Draft (AdvisoryTab.razor + Detail.razor)

Advisory generation is meaningless before credit bureau checks and document verification. Added `CanGenerate` parameter to `AdvisoryTab`. When false (application is in Draft), the empty state shows "Advisory not available yet" with an explanation instead of the generate button.

`CanGenerateAdvisory` in `Detail.razor` = `application.Status != AppStatus.Draft`.

---

#### Design Decision — Collateral Approval Roles (Deferred)

Discussed who should approve collateral: requires at minimum Legal clearance (title/encumbrance) + Credit/Risk Officer adequacy sign-off as two distinct steps. External valuer produces the report both rely on. Single "Approve" button is insufficient. Deferred for implementation at collateral perfection stage. Saved to `memory/project_collateral_approval.md`.

---

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [ ] `docs/UIGaps.md` → not updated (no new gaps discovered or closed)
- [ ] `docs/ImplementationTracker.md` → not updated (UI-only fixes, no new backend)

---

## 5. Previous Session Summary (2026-04-14 Session 48)

### Completed — Stale-State Audit Fix + UI Bug Fixes + Business Proposal Documents

---

#### Fix 1 — Stale State After Workflow Actions (Detail.razor)

All action handlers in `Detail.razor` call `await LoadApplication()` after success, but `LoadApplication()` never called `StateHasChanged()`. The result was that the UI would not re-render after committee voting and other actions without a page refresh.

**Root cause:** Inconsistency — some handlers (`StartCommitteeVoting`, `ConfirmCommitteeDecision`, `RemoveCommitteeMember`, `AddCommitteeMember`, `SubmitForReview`) called `await InvokeAsync(StateHasChanged)` explicitly while all other handlers (20+) only called `await LoadApplication()`.

**Fix:** Added `StateHasChanged()` at the end of `LoadApplication()`, after `isLoading = false`. This single change covers all 20+ callers uniformly.

**File modified:** `src/CRMS.Web.Intranet/Components/Pages/Applications/Detail.razor`

---

#### Fix 2 — Document Viewer Modal Too Small (DocumentsTab.razor)

The document viewer modal body was capped at `height: 70vh` and the modal itself at `max-height: 90vh`, making the iframe too small for reading PDFs.

**Fix:** Set `height: 92vh; max-height: 92vh` on the `.modal` div and replaced the hardcoded body height with `flex: 1; overflow: hidden; min-height: 0`. The base `.modal` CSS (`display: flex; flex-direction: column`) means `flex: 1` on the body fills the remaining space between header and footer.

**File modified:** `src/CRMS.Web.Intranet/Components/Pages/Applications/Tabs/DocumentsTab.razor`

---

#### Fix 3 — SetupCommitteeModal Rendering Inline Instead of as Overlay

Two structural bugs caused the modal to render in document flow rather than as a centred overlay:
1. `.modal-backdrop` was missing the `show` class → CSS `opacity: 0; visibility: hidden` → invisible overlay
2. `.modal` div was a sibling root element of `.modal-backdrop`, not a child → rendered in normal document flow

**Fix:**
- Added `show` class to `.modal-backdrop`
- Nested `.modal` inside `.modal-backdrop` (not as a sibling)
- Added `@onclick:stopPropagation` on `.modal` to prevent backdrop click-through
- Added missing closing `</div>` for the backdrop wrapper

**File modified:** `src/CRMS.Web.Intranet/Components/Pages/Applications/Modals/SetupCommitteeModal.razor`

---

#### Feature 4 — Business Proposal Documents

Created two pre-sale proposal documents in `docs/`:

**`docs/CRMS_Proposal.md`** — 24-slide Markdown structured for Claude chat PPT conversion:
- Slides separated by `---`, titles use `#`, bullets use `-`
- Covers: Title, Agenda, Problem (×2), Introducing CRMS, Differentiators, 13 Capabilities, Loan Application, Workflow, AI Scoring, Credit Bureau, Bank Statements, Committee, Document Generation, Disbursement Checklist, Lifecycle, Compliance, Tech, Security/Roles, Implementation, Support/SLA, About Us, Next Steps, Closing

**`docs/CRMS_Proposal.html`** — Fully standalone single-file HTML proposal (no external JS dependencies):
- Fixed dark glass nav, full-viewport hero with gradient + stats bar (17 stages, 13 modules, 9 roles, 4 documents)
- 13 capability cards (3-column responsive grid), loan lifecycle flow with arrow connectors
- AI scoring section with animated bar charts per category + 5 outcome badges
- Compliance, Technology stack, 9 role cards + 4 security control cards
- 5-phase implementation timeline, 4 SLA priority cards, About Us, 4-step CTA, closing quote, footer
- All placeholders: `[Your Company Name]`, `[Client Institution Name]`, `[Your Name]`, `[Title]`, contact fields

---

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v5.9
- [x] `docs/ImplementationTracker.md` → v7.0

---

## 5. Previous Session Summary (2026-04-13 Session 47)

### Completed — Offer Acceptance Enhancement + Loan Pack PDF Full Data + Loan Pack Visibility Fix

---

#### Feature 1 — Record Offer Acceptance: Structured Operational Fields

Added three CBN Consumer Protection-compliant fields to the offer acceptance flow (required before disbursement in Nigerian banking).

**Domain (`LoanApplication.cs`):**
- New properties: `CustomerSignedAt` (DateTime?), `AcceptanceMethod` (OfferAcceptanceMethod?), `KfsAcknowledged` (bool)
- `AcceptOffer()` signature updated to accept all three; KFS gate added — returns failure if `kfsAcknowledged == false`
- `OfferAcceptanceMethod` enum added to `src/CRMS.Domain/Enums/LoanApplicationEnums.cs` (values: `InBranchSigning`, `Courier`, `Electronic`)

**Infrastructure:**
- `LoanApplicationConfiguration.cs` — EF mappings: `CustomerSignedAt` (nullable datetime), `AcceptanceMethod` (string 30, nullable), `KfsAcknowledged` (tinyint default false)
- Migration `AddOfferAcceptanceFields` created and applied

**Application (`ConfirmOfferAcceptanceCommand.cs`):**
- Command record extended with `CustomerSignedAt`, `AcceptanceMethod`, `KfsAcknowledged`
- `SystemAdmin` removed from role check (Operations only)
- `AcceptOffer()` call updated to pass new params

**UI (`Detail.razor`):**
- Record Acceptance modal: Acceptance Date (date picker), Acceptance Method dropdown, KFS Acknowledged checkbox
- Confirm button disabled unless all three are set
- `ShowRecordAcceptanceButton` — `SystemAdmin` removed (Operations only)
- `ConfirmRecordAcceptance` parses `CRMS.Domain.Enums.OfferAcceptanceMethod` (fully qualified — `_Imports.razor` does not import `CRMS.Domain.Enums`)

**Service (`ApplicationService.cs`):**
- `RecordOfferAcceptanceAsync` extended with `customerSignedAt`, `acceptanceMethod`, `kfsAcknowledged` params

**Evidence viewer (`OfferAcceptanceTab.razor`):**
- View button (eye icon) opens an in-app iframe modal (92vh) at `/api/documents/{id}/view`; Download button in modal footer

---

#### Feature 2 — Loan Pack PDF: Full Application Data

Comprehensive rewrite of `GenerateLoanPackCommand.BuildLoanPackDataAsync` and additions to `LoanPackPdfGenerator`.

**`LoanPackData.cs` — new fields:**
- `decimal? ApprovedAmount`, `int? ApprovedTenorMonths`, `decimal? ApprovedInterestRate`
- `CommitteeDecisionData?` — decision string, vote tallies (Approve/Reject/Abstain/Pending), rationale, recommended terms, `List<CommitteeMemberVoteData>`
- `CommitteeMemberVoteData` — MemberName, MemberRole, Vote, VoteComment, VotedAt

**`GenerateLoanPackCommand.cs` — data population fixes:**
- Product name resolved via `ILoanProductRepository` (was empty string)
- Parties loaded via `GetByIdWithPartiesAsync`
- Bureau reports loaded via new `GetByLoanApplicationIdWithDetailsAsync` (includes `Accounts` + `ScoreFactors`)
- `bureauByPartyId` lookup enables director/signatory cross-referencing for credit score, rating, loan/delinquency flags
- `CustomerProfileData` populated from real aggregate fields (`RegistrationNumber`, `IncorporationDate`, `IndustrySector`)
- Financial statements: `TotalCurrentAssets`, `TotalNonCurrentAssets`, `TotalCurrentLiabilities` (correct property names on `BalanceSheet`)
- Revenue/profit YoY growth calculated from two most recent years (non-nullable `decimal` arithmetic — no `.Value` calls)
- Cashflow: aggregated from `CashflowSummary` across all analysed bank statements (real values, not zeros)
- Collateral: `ValuationDate`/`ValuerName` from `Valuations.OrderByDescending().FirstOrDefault()`; coverage uses `ApprovedAmount` if set
- Advisory: `MitigatingFactors` split from newline-separated string; recommendation strings built from values
- Committee: comments resolved via `memberLookup` from `CommitteeReview.Members` (stored `UserName`); `CommitteeDecisionData` built with `FinalDecision?.ToString()`, vote tally (`ApprovalVotes`/`RejectionVotes`/`AbstainVotes`/`PendingVotes`), per-member votes
- Approved terms: `loanApp.ApprovedAmount?.Amount`, `loanApp.ApprovedTenorMonths`, `loanApp.ApprovedInterestRate`

**`IBureauReportRepository` + `BureauReportRepository`:**
- New method `GetByLoanApplicationIdWithDetailsAsync` (includes `Accounts` + `ScoreFactors`)

**`LoanPackPdfGenerator.cs` — new rendering:**
- Executive Summary: approved terms rows (Amount, Tenor, Rate, Committee Decision) when set
- Bureau Reports: Active Facilities table per report (lender, facility type, original, outstanding, status); Delinquent Accounts as table (was bullet list)
- Section 12 → now Section 13: Committee Decision page (new Section 12): decision banner with colour coding, vote tally boxes, recommended terms table, member votes breakdown table

**Bug fixes in `GenerateLoanPackCommand.cs`:**
- `AccountStatus.Active` → `AccountStatus.Performing` (correct enum value for performing loans)
- `BalanceSheet.CurrentAssets` → `TotalCurrentAssets`, `FixedAssets` → `TotalNonCurrentAssets`, `CurrentLiabilities` → `TotalCurrentLiabilities`
- `decimal.Value` calls removed (properties are non-nullable `decimal`)
- `committeeReview.Decision` → `committeeReview.FinalDecision`

---

#### Feature 3 — Loan Pack Button: Visible to All Workflow Actors

**`Detail.razor` — `CanGeneratePack`:**
- Before: only `Approved | CommitteeApproved | Disbursed`
- After: all active statuses (excludes Draft / Rejected / CommitteeRejected / Cancelled / Closed) AND role check: `LoanOfficer | CreditOfficer | HOReviewer | BranchApprover | FinalApprover | Operations | SystemAdmin` or current user is a committee member on this application

**Build:** 0 errors, 0 warnings (Infrastructure + Web.Intranet verified).

---

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v5.8
- [x] `docs/ImplementationTracker.md` → v7.0

---

## 5. Last Session Summary (2026-04-09 Session 46)

### Completed — Post-Approval Pre-Disbursement: Full Disbursement Checklist Feature

Implemented the complete disbursement checklist feature end-to-end (8 tasks). Replaced the hardcoded throwaway `cpChecklist` in `Detail.razor` with a persisted, domain-driven, admin-configurable checklist.

---

#### Task 1–2 — Domain + Infrastructure (completed in prior session, confirmed this session)

`DisbursementChecklistItem` entity (full state machine):
- `ChecklistItemStatus`: `Pending → PendingLegalReview → LegalReturned → Satisfied | WaiverPending → Waived | Overdue → ExtensionPending`
- `ConditionType`: `Precedent` (blocks disbursement) and `Subsequent` (monitored post-disbursement)
- Roles: LoanOfficer satisfies/submits/proposes waiver; LegalOfficer ratifies legal items; RiskManager ratifies waivers and CS extensions; Operations confirms acceptance
- `MarkOverdue()` changed from `internal` to `public` to allow access from Infrastructure
- Migration `20260409123746_AddDisbursementChecklist` created — **PENDING** (needs `dotnet ef database update`)

---

#### Task 3 — Application Layer

**New files:**
- `src/CRMS.Application/OfferAcceptance/Commands/ConfirmOfferAcceptanceCommand.cs` — validates all mandatory CP items resolved, calls `loanApp.AcceptOffer()`, generates Disbursement Memo PDF, uploads to `disbursementmemos` container
- `src/CRMS.Application/OfferAcceptance/Queries/GetDisbursementChecklistQuery.cs` — fetches all checklist items ordered by `SortOrder`, maps to `ChecklistItemDto`, computes `AllPrecedentResolved`
- `src/CRMS.Application/OfferAcceptance/Commands/` — 8 item-action handlers: `SatisfyChecklistItemCommand`, `SubmitForLegalReviewCommand`, `RatifyLegalItemCommand`, `ReturnByLegalCommand`, `ProposeWaiverCommand`, `RatifyWaiverCommand`, `RequestCsExtensionCommand`, `RatifyExtensionCommand`
- `src/CRMS.Application/ProductCatalog/Commands/ChecklistTemplateCommands.cs` — `AddChecklistTemplateItemCommand`, `UpdateChecklistTemplateItemCommand`, `RemoveChecklistTemplateItemCommand` (SystemAdmin/RiskManager only)

**Modified:**
- `src/CRMS.Application/LoanPack/DTOs/LoanPackData.cs` — Added `List<string> ApprovalConditions` field
- `src/CRMS.Application/LoanPack/Commands/GenerateLoanPackCommand.cs` — Parses `committeeReview.ApprovalConditions` (newline-separated → `List<string>`) and passes to `LoanPackData`

---

#### Task 4 — Infrastructure: Disbursement Memo PDF

**New file:** `src/CRMS.Infrastructure/Documents/DisbursementMemoPdfGenerator.cs`
- Full QuestPDF implementation: loan summary box, CP table, CS table, certification block with signature lines
- Status-colour badge mapping per `ChecklistItemStatus`
- Implements `IDisbursementMemoPdfGenerator` interface in `CRMS.Application.OfferAcceptance.Interfaces`

---

#### Task 5 — Infrastructure: CS Monitoring Background Service

**New file:** `src/CRMS.Infrastructure/BackgroundServices/CsMonitoringBackgroundService.cs`
- Runs every 24 hours; queries disbursed loans with active CS items via EF LINQ join (no navigation property on `DisbursementChecklistItem`)
- Calls `item.MarkOverdue()` when `DueDate` has passed and status is `Pending`
- Logs tiered warnings at T-7, T-1, T+0, T+7, T+30, T+90 relative to `DueDate`
- Join query pattern used (no `Include`): `from item in db.DisbursementChecklistItems join app in db.LoanApplications on item.LoanApplicationId equals app.Id`

---

#### Task 6 — DI Registration

**Modified:** `src/CRMS.Infrastructure/DependencyInjection.cs`
- `IDisbursementMemoPdfGenerator → DisbursementMemoPdfGenerator`
- All 9 offer acceptance handlers + `GetDisbursementChecklistHandler`
- 3 checklist template command handlers
- `CsMonitoringBackgroundService` as hosted service

---

#### Task 7 — UI: ApplicationService + Models

**Modified:** `src/CRMS.Web.Intranet/Services/ApplicationService.cs`
- `IssueOfferLetterAsync` now also calls `IssueOfferLetterCommand` (seeds checklist from product template)
- `RecordOfferAcceptanceAsync` extended with `string userName` — calls `ConfirmOfferAcceptanceHandler` first (validates CP gate + generates memo PDF), then transitions workflow
- 8 new methods: `GetDisbursementChecklistAsync`, `SatisfyChecklistItemAsync`, `SubmitForLegalReviewAsync`, `RatifyLegalItemAsync`, `ProposeWaiverAsync`, `RatifyWaiverAsync`, `RequestCsExtensionAsync`, `RatifyExtensionAsync`

**Modified:** `src/CRMS.Web.Intranet/Models/ApplicationModels.cs`
- Added `DisbursementChecklistModel`, `ChecklistItemModel` (with computed `StatusBadgeClass` and `StatusDisplay`), `ChecklistTemplateItemModel`

---

#### Task 8 — UI: OfferAcceptanceTab + Detail.razor + LoanPack PDF

**New file:** `src/CRMS.Web.Intranet/Components/Pages/Applications/Tabs/OfferAcceptanceTab.razor`
- CP section table + CS section table (only shown when items exist)
- Summary banner (green "all CP resolved" / amber "X items unresolved")
- Role-filtered action buttons per item
- Single modal state machine (one `activeModal` + `activeItem` pair handles all 7 action types)
- Parameters: `ApplicationId` (Guid), `ApplicationStatus` (string)

**Modified:** `src/CRMS.Web.Intranet/Components/Pages/Applications/Detail.razor`
- Added `Disbursement Checklist` tab (visible at OfferGenerated/OfferAccepted/Disbursed)
- `ShowRecordAcceptanceButton` now requires Operations/SystemAdmin (not LoanOfficer)
- `ConfirmRecordAcceptance` passes `userName` to `RecordOfferAcceptanceAsync`
- Removed `CpCheckItem` class, `cpChecklist` list, `InitialiseCpChecklist()` — replaced by domain gate
- Disburse modal body replaced hardcoded checklist with green "CPs verified on checklist tab" banner
- CP gate removed from Disburse button (enforcement moved to domain: `loanApp.AcceptOffer()` validates)

**Modified:** `src/CRMS.Infrastructure/Documents/LoanPackPdfGenerator.cs`
- Added Section 12 "Conditions of Approval" — numbered table rendered when `data.ApprovalConditions.Any()`

**Build:** 0 errors, 0 warnings (Application + Infrastructure + Web.Intranet all verified).

---

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v5.7
- [x] `docs/ImplementationTracker.md` → v6.9

---

## 5. Last Session Summary (2026-04-08 Session 45)

### Completed — Three Bug Clusters Fixed: Seeder Crash, File Upload, Offer Letter/Loan Pack Generation

---

#### Bug 1 — `DbUpdateConcurrencyException` on App Restart (Seeder)

`ComprehensiveDataSeeder.SeedWorkflowDefinitionAsync` crashed on every restart when the DB already had workflow data. Root cause: the two upgrade blocks (adding `FinalApproval` stage, adding `OfferGenerated`/`OfferAccepted` stages) mixed raw SQL DELETEs with EF `AddStage`/`AddTransition` + `SaveChangesAsync`. EF's snapshot still included the deleted transitions, generating phantom DELETEs that returned 0 rows → `DbUpdateConcurrencyException`.

**Fix:** Completely rewrote both upgrade blocks to use **only** `ExecuteSqlRawAsync` — no EF tracking, no `SaveChangesAsync` in the upgrade path at all.

| Operation | SQL pattern used |
|-----------|-----------------|
| DELETE old transitions | `DELETE FROM WorkflowTransitions WHERE ...` (idempotent — no error if absent) |
| INSERT stage | `INSERT IGNORE INTO WorkflowStages (...)` (idempotent via unique index on `WorkflowDefinitionId+Status`) |
| INSERT transition | `INSERT INTO WorkflowTransitions ... SELECT ... WHERE NOT EXISTS (...)` (idempotent) |

Added private `InsertTransitionIfMissingAsync` helper to avoid repetition. After all upgrades, `ChangeTracker.Clear()` + reload returns a fresh entity.

**Files modified:**
- `src/CRMS.Infrastructure/Persistence/ComprehensiveDataSeeder.cs`

---

#### Bug 2 — Offer Letter Generation and Loan Pack Download Failing

Both always returned "Failed to generate offer letter" / "Loan pack generated but could not be downloaded."

**Root cause:** `LocalFileStorageService.UploadAsync` only created the container directory (`storage/offerletters`). The callers passed `fileName: $"{loanApp.ApplicationNumber}/{fileName}"` — a sub-path with a `/`. The computed `filePath` therefore pointed into a subdirectory (`storage/offerletters/GUID_CL-2026-0001/OfferLetter.pdf`) that was never created. `File.WriteAllBytesAsync` threw `DirectoryNotFoundException`, caught by the handler's try-catch → failure result.

**Fix (one line of logic):** Before `File.WriteAllBytesAsync`, call `Directory.CreateDirectory(Path.GetDirectoryName(filePath))` — a no-op when directory already exists.

**Files modified:**
- `src/CRMS.Infrastructure/Storage/LocalFileStorageService.cs`

---

#### Bug 3 — "Failed to Generate Offer Letter" Persisting After Storage Fix

Even after Bug 2 was fixed, a second attempt to generate always failed because a prior `Failed` record was left in the DB from the original broken attempt.

**Root cause chain (3 compounding issues):**

| # | Issue | Effect |
|---|-------|--------|
| 1 | `OfferLetter.Create()` hardcoded `Version = 1` | Every generation attempt creates a `Version = 1` entity |
| 2 | Unique index on `(LoanApplicationId, Version)` | Re-inserting `Version = 1` violates DB constraint → `DbUpdateException` |
| 3 | Catch block's `SaveChangesAsync` was unprotected | Constraint violation thrown *inside* catch propagated up, masking the real error with generic "Failed" message |
| 4 | `GetVersionCountAsync` counted Failed records | Version numbering by COUNT was wrong; MAX version is correct |
| 5 | Failed records shown in UI | Empty filename, 0 bytes, non-functional download buttons — confusing users |

**Fixes:**

| File | Change |
|------|--------|
| `OfferLetter.cs` | `Create()` accepts `version` param (default 1); entity `Version` set correctly; domain event uses `letter.Version` |
| `LoanPack.cs` | Same fix |
| `IOfferLetterRepository` | `GetVersionCountAsync` → `GetMaxVersionAsync` (returns `MAX(Version) ?? 0`) |
| `ILoanPackRepository` | Same |
| `OfferLetterRepository` | Implements `GetMaxVersionAsync` using EF `MaxAsync` |
| `LoanPackRepository` | Same |
| `GenerateOfferLetterCommand` | Uses `nextVersion = maxExisting + 1` everywhere; passes to `Create()`; catch block protected with inner try-catch |
| `GenerateLoanPackCommand` | Same |
| `GetOfferLettersByApplicationQuery` | Filters out `Status == Failed` records — UI shows only actionable records |

**Files modified:**
- `src/CRMS.Domain/Aggregates/OfferLetter/OfferLetter.cs`
- `src/CRMS.Domain/Aggregates/LoanPack/LoanPack.cs`
- `src/CRMS.Domain/Interfaces/IOfferLetterRepository.cs`
- `src/CRMS.Domain/Interfaces/ILoanPackRepository.cs`
- `src/CRMS.Infrastructure/Persistence/Repositories/OfferLetterRepository.cs`
- `src/CRMS.Infrastructure/Persistence/Repositories/LoanPackRepository.cs`
- `src/CRMS.Application/OfferLetter/Commands/GenerateOfferLetterCommand.cs`
- `src/CRMS.Application/LoanPack/Commands/GenerateLoanPackCommand.cs`
- `src/CRMS.Application/OfferLetter/Queries/OfferLetterQueries.cs`

---

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [ ] `docs/UIGaps.md` → not updated (no new UI gaps discovered)
- [x] `docs/ImplementationTracker.md` → v6.8

---

## 5. Last Session Summary (2026-04-07 Session 43)

### Completed — CreditAnalysis Workflow Fully Wired (Approve + Return) + 4 Missing DI Registrations

This session fixed a cluster of persistent bugs in the CreditAnalysis and HOReview workflow stages. The investigation revealed a systemic problem: **handlers in this codebase must be explicitly registered in `DependencyInjection.cs`** — there is no MediatR assembly scanning. Several handlers added in prior sessions were never registered.

#### Root Cause: Missing DI Registrations

`ApplicationService.cs` resolves all handlers via `_sp.GetRequiredService<ConcreteHandlerType>()`. If the handler is not in `DependencyInjection.cs`, this throws `InvalidOperationException` — caught by the catch block — returning the generic "Failed to X" error to the UI.

**Four handlers were unregistered:**

| Handler | Used by | Symptom when missing |
|---------|---------|---------------------|
| `ApproveCreditAnalysisHandler` | `ApproveApplicationAsync` (CreditAnalysis) | "Failed to approve application" |
| `ReturnFromCreditAnalysisHandler` | `ReturnApplicationAsync` (CreditAnalysis) | "Failed to return application" |
| `ReturnFromHOReviewHandler` | `ReturnApplicationAsync` (HOReview) | "Failed to return application" |
| `FinalApproveHandler` | `ApproveApplicationAsync` (FinalApproval) | "Failed to approve application" |

**Fix:** Added all four to `DependencyInjection.cs` lines 321–326.

**Rule for future sessions:** Every new `*Handler` class added to the Application layer MUST immediately get a corresponding `services.AddScoped<...>()` line in `DependencyInjection.cs`.

#### CreditAnalysis Return Capability (full stack)

Previously Credit Officers at the CreditAnalysis stage had no Return button. Added end-to-end:

| Layer | Change |
|-------|--------|
| `LoanApplication.cs` | Added `ReturnFromCreditAnalysis(userId, reason)` — guards on `CreditAnalysis` status, sets `Status = BranchReview` |
| `SubmitLoanApplicationCommand.cs` | Added `ReturnFromCreditAnalysisCommand` + `ReturnFromCreditAnalysisHandler` |
| `ApplicationService.cs` | Added `ReturnFromCreditAnalysisHandler` call in `ReturnApplicationAsync` for `currentStatus == "CreditAnalysis"` |
| `Detail.razor` `ShowReturnButton` | Added `AppStatus.CreditAnalysis => CreditOfficer` case |
| `ComprehensiveDataSeeder.cs` | Added `(CreditAnalysis, BranchReview, Return, Roles.CreditOfficer)` transition |
| `WorkflowCommands.cs` | Fixed `CreditAnalysis→HOReview` from `MoveToNextStage/"System"` to `Approve/"CreditOfficer"`; added `Return/"CreditOfficer"` transition |
| DB (live SQL) | Inserted `CreditAnalysis → BranchReview, Return, CreditOfficer` row into `WorkflowTransitions` |

#### Prior Session Fixes Confirmed Still Active

From the session that preceded this (Session 42 carried forward):
- `AllCreditChecksCompletedWorkflowHandler` — no longer auto-transitions; Credit Officer must manually approve
- `GetWorkflowInstanceByApplicationIdAsync` — uses `_sp.CreateScope()` (fresh DbContext) to avoid stale entity tracking
- All `TransitionWorkflowHandler` calls use fresh scopes

**Build:** 0 errors, 0 warnings (Application + Infrastructure verified).

**Files Modified This Session:**
- `src/CRMS.Infrastructure/DependencyInjection.cs`
- `src/CRMS.Domain/Aggregates/LoanApplication/LoanApplication.cs`
- `src/CRMS.Application/LoanApplication/Commands/SubmitLoanApplicationCommand.cs`
- `src/CRMS.Application/Workflow/Commands/WorkflowCommands.cs`
- `src/CRMS.Infrastructure/Persistence/ComprehensiveDataSeeder.cs`
- `src/CRMS.Infrastructure/Events/Handlers/WorkflowIntegrationHandlers.cs`
- `src/CRMS.Web.Intranet/Services/ApplicationService.cs`
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Detail.razor`

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [ ] `docs/UIGaps.md` → not updated (no new UI gaps discovered)
- [x] `docs/ImplementationTracker.md` → v6.6

---

## 5. Last Session Summary (2026-04-03 Session 40)

### Completed — Lifecycle Gap Fixes G4, G6, G7, G9 + Comprehensive G1–G9 Code Review

This session resumed from a context exhaustion mid-session. A code review of all G1–G9 fixes was conducted to confirm what was and wasn't completed, followed by targeted fixes for G4, G7 (completion), G9, and the outbox work from G6.

#### G6 — Persistent Credit Check Outbox (completed in prior partial session, confirmed this session)

**Previously:** `CreditCheckBackgroundService` used an in-memory `Channel<CreditCheckRequest>`. App restart permanently lost any pending credit check requests.

**Now:** Replaced with a DB-backed outbox pattern.

| File | Role |
|------|------|
| `src/CRMS.Infrastructure/Persistence/Outbox/CreditCheckOutboxEntry.cs` | Outbox entity (Id, LoanApplicationId, SystemUserId, Status, AttemptCount, ErrorMessage) |
| `src/CRMS.Application/CreditBureau/Interfaces/ICreditCheckOutbox.cs` | Interface: `EnqueueAsync` — adds to DbContext without saving |
| `src/CRMS.Infrastructure/BackgroundServices/CreditCheckBackgroundService.cs` | Rewritten: polls DB every 30s; claims entries (Processing); processes each in isolated scope; retries up to 3×; marks Completed/Failed |
| `src/CRMS.Infrastructure/Persistence/Configurations/Outbox/CreditCheckOutboxConfiguration.cs` | EF config |
| Migration `20260402140707_AddCreditCheckOutbox` | Creates `CreditCheckOutbox` table + indexes on `LoanApplicationId` and `Status` |

**Key design:** `ApproveBranchHandler` calls `_outbox.EnqueueAsync(...)` then `_unitOfWork.SaveChangesAsync()` — both the approval and the outbox entry commit in one atomic transaction. No gap, no lost checks.

#### G7 — FinalApproval Status Wired (completed in prior partial session, confirmed this session)

**Previously:** `FinalApproval` and `OfferAccepted` enum values were defined but never set by any code path.

**Now:**
- `LoanApplication.MoveToFinalApproval(userId)` domain method added — sets `Status = FinalApproval`
- `CommitteeDecisionWorkflowHandler` (Approved path) now calls it as an auto-transition after `CommitteeApproved`
- Workflow seeder updated: `CommitteeApproved → FinalApproval` transition (system-driven); `FinalApproval → Approved/Rejected` (FinalApprover role)
- `ApplicationService.ApproveApplicationAsync` maps `FinalApproval → Approved` and calls `FinalApproveHandler`
- `Detail.razor` `ShowApproveButton` / `ShowRejectButton` gate on `FinalApprover` role at `FinalApproval` status

#### G9 — `Guid.Empty` Replaced with Correct Actor IDs

**Previously:** All event handlers passed `Guid.Empty` as the user ID for every action — including the committee chairman's decision — making the audit trail unintelligible.

**Root cause confirmed:** `CommitteeDecisionRecordedEvent` carries `DecisionByUserId` (the chairman's actual ID set when `RecordDecision(decidedByUserId, ...)` is called), but it was never used.

**Fix — two distinct cases correctly separated:**

| Call site | Before | After |
|-----------|--------|-------|
| `loanApplication.ApproveCommittee(...)` | `Guid.Empty` | `domainEvent.DecisionByUserId` |
| `loanApplication.RejectCommittee(...)` | `Guid.Empty` | `domainEvent.DecisionByUserId` |
| `TransitionAsync` → CommitteeApproved | `Guid.Empty` | `domainEvent.DecisionByUserId` |
| `TransitionAsync` → FinalApproval (auto) | `Guid.Empty` | `SystemConstants.SystemUserId` |
| `TransitionAsync` → Rejected/Deferred | `Guid.Empty` | `domainEvent.DecisionByUserId` |
| `loanApplication.MoveToHOReview(...)` | `Guid.Empty` | `SystemConstants.SystemUserId` |
| `TransitionAsync` in `AllCreditChecksCompletedWorkflowHandler` | `Guid.Empty` | `SystemConstants.SystemUserId` |

**New files:**
- `src/CRMS.Domain/Constants/SystemConstants.cs` — `SystemUserId = new("00000000-0000-0000-0000-000000000001")`

**Display fix:** `ApplicationService.cs` workflow history mapping now resolves `SystemConstants.SystemUserId` → `"System Process"` instead of raw GUID string.

#### G4 — Bureau Tab Consent-Blocked UI Signal

**Previously:** Individual bureau report cards showed a per-card "Consent Required" badge in their footer, but:
1. Business entity report cards had no status badges at all (ConsentRequired/Failed/NotFound were missing from the business card footer)
2. No tab-level explanation of why the loan was stuck or what the credit officer needed to do

**Fix 1 — Business card footer:** Added `ConsentRequired`, `Failed`, and `NotFound` status badges to the business report card footer (same pattern already used by individual cards).

**Fix 2 — Consent-blocked banner in `BureauTab.razor`:** When any report has `ConsentRequired` status, a yellow warning banner appears at the top of the reports section listing the blocked parties by name and directing the credit officer to obtain NDPA consent then click **Re-run Credit Checks** in the action bar.

**Files modified:**
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Tabs/BureauTab.razor`
- `src/CRMS.Infrastructure/Events/Handlers/WorkflowIntegrationHandlers.cs`
- `src/CRMS.Web.Intranet/Services/ApplicationService.cs`
- `src/CRMS.Infrastructure/BackgroundServices/CreditCheckBackgroundService.cs`
- `src/CRMS.Infrastructure/DependencyInjection.cs`
- `src/CRMS.Application/LoanApplication/Commands/SubmitLoanApplicationCommand.cs`

**Build:** 0 errors (Infrastructure + Web.Intranet both verified).

#### G10 — Committee Deferral Dual-Status Desync + No UI Indicator (Session 41)

**Issues found:**

1. **Critical (undocumented):** `CommitteeDecisionWorkflowHandler` Deferred case had a bare `break` — no domain method called on `LoanApplication`. `WorkflowInstance.CurrentStatus` was updated to `HOReview` but `LoanApplication.Status` remained `CommitteeCirculation`. UI button visibility (driven by `LoanApplication.Status`) showed committee-stage buttons instead of HOReview approve/return/reject. The loan could never advance.

2. **UX gap (original G10):** No banner at HOReview indicating the application was deferred, what the rationale was, or when it was deferred.

**Fix:**

| What | Where | Change |
|------|-------|--------|
| Add `DeferFromCommittee(userId, rationale)` | `LoanApplication.cs` | New domain method — validates `CommitteeCirculation` status, sets `Status = HOReview`, writes status history entry |
| Add `Rationale` to event | `CommitteeReview.cs` | `CommitteeDecisionRecordedEvent` gains `string? Rationale = null`; `AddDomainEvent` call passes `rationale` |
| Call domain method | `WorkflowIntegrationHandlers.cs` | Deferred `case` now calls `loanApplication.DeferFromCommittee(...)`, guards on failure |
| Deferral banner | `Detail.razor` | Yellow warning banner shown at `HOReview` status when `Committee.Decision == "Deferred"` — shows date and rationale inline |

**No migration needed.** Data already in `CommitteeInfo.DecisionComments` / `DecisionDate` — no new DB columns.

**Files modified:**
- `src/CRMS.Domain/Aggregates/LoanApplication/LoanApplication.cs`
- `src/CRMS.Domain/Aggregates/Committee/CommitteeReview.cs`
- `src/CRMS.Infrastructure/Events/Handlers/WorkflowIntegrationHandlers.cs`
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Detail.razor`

**Build:** 0 errors (Domain + Infrastructure + Web.Intranet all verified).

#### G11 — Credit Check Retry Gaps (Session 42)

**Issues fixed:**

1. **Orphaned `Processing` entries after crash:** The poll query only picked up `Pending` entries. If the app crashed after claiming an entry (setting `Status = Processing`) but before saving the result, the entry was permanently stuck. Fixed by adding `RecoverOrphanedEntriesAsync()` at service startup — resets any `Processing` entries to `Pending` before the first poll cycle.

2. **`ProcessedAt` not set on terminal failure:** `entry.ProcessedAt` was only set on success. Added `if (isFinalAttempt) entry.ProcessedAt = DateTime.UtcNow` to both the result-failure and exception paths in `ProcessEntryAsync`.

3. **`Failed` bureau reports were not retryable (3 sub-problems):**
   - **Button invisible:** `CanRerunCreditChecks` only showed for `ConsentRequired` — expanded to `ConsentRequired || Failed`.
   - **Re-run was a no-op:** `Failed` was in the idempotency "done" set alongside `Completed`/`NotFound`. Changed to: build `alreadyCountedBvns`/`alreadyCountedBusiness` sets from existing `Failed` reports, delete those `Failed` reports, then remove `Failed` from `existingBvns` so the retry loop processes them fresh.
   - **Double-counting `CreditChecksCompleted`:** `RecordCreditCheckCompleted` had already been called for the original `Failed` run. Added `!alreadyCountedBvns.Contains(party.BVN!)` guard to all three `RecordCreditCheckCompleted` call sites (parties, guarantors, business) to prevent double-incrementing.

**Files modified:**
- `src/CRMS.Domain/Interfaces/IBureauReportRepository.cs` — Added `void Delete(BureauReport report)`
- `src/CRMS.Infrastructure/Persistence/Repositories/BureauReportRepository.cs` — Implemented `Delete`
- `src/CRMS.Application/CreditBureau/Commands/ProcessLoanCreditChecksCommand.cs` — `Failed` reports retryable with double-count guard
- `src/CRMS.Infrastructure/BackgroundServices/CreditCheckBackgroundService.cs` — Startup recovery + `ProcessedAt` on failure
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Detail.razor` — `CanRerunCreditChecks` gate expanded

**Build:** 0 errors, 0 warnings (Domain + Infrastructure + Web.Intranet verified).

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v5.6
- [x] `docs/ImplementationTracker.md` → v6.5

---

## 5. Last Session Summary (2026-03-18 Session 24)

### Completed — P3 UI Gaps: Template CRUD + Bureau Report Detail Modal

Addressed the remaining P3 UI gaps from the gap analysis. 2 of 3 items implemented; the third (guarantor credit check trigger) is N/A since credit checks are already auto-triggered after branch approval.

#### 1. Template Management CRUD (`/admin/templates`)

**Previously:** Display-only page with hardcoded mock data and no-op save.

**Now:** Full CRUD wired to real backend.

**Application Layer (NEW):**
- `NotificationTemplateCommands.cs` — `CreateNotificationTemplateCommand` + handler, `UpdateNotificationTemplateCommand` + handler, `ToggleNotificationTemplateCommand` + handler

**Domain/Infrastructure changes:**
- `INotificationTemplateRepository` — Added `GetAllAsync()` (returns all templates including inactive)
- `NotificationTemplateRepository` — Implemented `GetAllAsync()`
- `GetAllNotificationTemplatesQuery` — Updated with `IncludeInactive` parameter (default true)
- `DependencyInjection.cs` — 5 new handler registrations

**UI changes:**
- `Templates.razor` — Complete rewrite: fetches real data on init, create/edit modal with validation (code+channel immutable on edit), activate/deactivate toggle, preview modal, search/filter by channel/status
- `ApplicationService.cs` — 4 new methods: `GetNotificationTemplatesAsync`, `CreateNotificationTemplateAsync`, `UpdateNotificationTemplateAsync`, `ToggleNotificationTemplateAsync`
- `ApplicationModels.cs` — Added `NotificationTemplateInfo`, `CreateTemplateRequest`, `UpdateTemplateRequest`

#### 2. Bureau Report Detail Modal (Click to Expand)

**Previously:** Bureau report cards in `BureauTab` showed summary only with no way to see full details.

**Now:** Click view button on any bureau card → opens `ViewBureauReportModal` with full detail.

**UI changes:**
- `ViewBureauReportModal.razor` (NEW) — Shows: subject header with score circle, 4 key metrics (active loans, total exposure, total overdue, max delinquency), fraud risk assessment section with color-coded score, alerts/red flags section, credit accounts table
- `BureauTab.razor` — Added `OnViewReport` EventCallback<Guid> parameter; view button on each card footer (business + individual)
- `Detail.razor` — Added bureau report modal state (`showBureauReportModal`, `viewingBureauReport`, `viewingBureauAccounts`); `ShowBureauReportModal` calls `GetBureauReportDetailAsync`; `CloseBureauReportModal`; modal rendering block
- `ApplicationService.cs` — Added `GetBureauReportDetailAsync(Guid reportId)` returning `(BureauReportInfo?, List<BureauAccountInfo>)` — calls `GetBureauReportByIdHandler` which returns full report with accounts
- `ApplicationModels.cs` — Added `BureauAccountInfo` model

#### 3. Guarantor Credit Check Trigger — N/A

Credit checks are already auto-triggered after branch approval via `ProcessLoanCreditChecksCommand`. This processes all directors, signatories, and guarantors in one batch. A manual per-guarantor trigger button is unnecessary given this design.

**Build:** 0 errors, 25 warnings (all pre-existing). **Tests:** Domain + Application pass (2/2).

### Files Created This Session
- `src/CRMS.Application/Notification/Commands/NotificationTemplateCommands.cs`
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Modals/ViewBureauReportModal.razor`

### Files Modified This Session
- `src/CRMS.Domain/Interfaces/INotificationRepository.cs` — Added `GetAllAsync()`
- `src/CRMS.Infrastructure/Persistence/Repositories/NotificationRepositories.cs` — Implemented `GetAllAsync()`
- `src/CRMS.Application/Notification/Queries/NotificationQueries.cs` — `IncludeInactive` param on `GetAllNotificationTemplatesQuery`
- `src/CRMS.Infrastructure/DependencyInjection.cs` — 5 new handler registrations
- `src/CRMS.Web.Intranet/Services/ApplicationService.cs` — 5 new methods (4 template + 1 bureau detail)
- `src/CRMS.Web.Intranet/Models/ApplicationModels.cs` — 4 new models
- `src/CRMS.Web.Intranet/Components/Pages/Admin/Templates.razor` — Complete rewrite
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Tabs/BureauTab.razor` — Added `OnViewReport` param + view buttons
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Detail.razor` — Bureau report modal wiring

---

## 5. Last Session Summary (2026-03-20 Session 26)

### Completed — Fineract Direct API Integration (Schedule Preview + Customer Exposure)

Implemented a direct Fineract API client for two critical capabilities: (1) repayment schedule preview for offer letter generation, and (2) customer existing loan exposure aggregation. This is separate from the existing middleware (`CoreBankingService`) which handles account details and transactions.

#### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Existing Middleware (CoreBankingService)                     │
│  Auth: OAuth 2.0 Client Credentials (bearer token)          │
│  Endpoints: /core/account/fulldetailsbynuban, /transactions │
│  Purpose: Account details, directors, signatories, txns     │
└─────────────────────────────────────────────────────────────┘
                              +
┌─────────────────────────────────────────────────────────────┐
│  NEW: Fineract Direct (FineractDirectService)                │
│  Auth: HTTP Basic Auth + fineract-platform-tenantid header   │
│  Endpoints:                                                  │
│    POST /loans?command=calculateLoanSchedule (schedule)      │
│    GET /clients/{id}/accounts (all accounts)                 │
│    GET /loans/{id}?associations=repaymentSchedule (detail)   │
│  Purpose: Schedule preview, customer loan exposure           │
└─────────────────────────────────────────────────────────────┘
```

#### Hybrid Schedule Calculation

| Scenario | Behavior |
|----------|----------|
| `FineractProductId` set on CRMS product + Fineract API reachable | Calls Fineract API — exact schedule matching core banking |
| `FineractProductId` set but Fineract API fails | Falls back to in-house financial math |
| `FineractProductId` not set (null) | Uses in-house calculation directly (EMI/flat/equal-principal) |

#### Customer Exposure Flow

```
clientDetails.id (from middleware) → GET /clients/{id}/accounts → filter loanAccounts by status=300 (Active) → GET /loans/{id} for each → aggregate outstanding balances
```

#### FineractProductId on LoanProduct

Added `int? FineractProductId` to the `LoanProduct` domain entity. Editable in `/admin/products` page. Maps a CRMS product to its Fineract counterpart. When set, enables Fineract API schedule calculation. When null, in-house calculation is used.

#### Files Created

| File | Purpose |
|------|---------|
| `src/CRMS.Domain/Interfaces/IFineractDirectService.cs` | Interface: 4 methods + all domain records (schedule, installments, loan detail, exposure) |
| `src/CRMS.Infrastructure/ExternalServices/FineractDirect/FineractDirectSettings.cs` | Config class (BaseUrl, Username, Password, TenantId, UseMock) |
| `src/CRMS.Infrastructure/ExternalServices/FineractDirect/FineractDirectDtos.cs` | Fineract JSON response DTOs (dates as `[year,month,day]` arrays, status objects) |
| `src/CRMS.Infrastructure/ExternalServices/FineractDirect/FineractDirectAuthHandler.cs` | `POST /authentication` → caches `base64EncodedAuthenticationKey`; SSL cert tolerance |
| `src/CRMS.Infrastructure/ExternalServices/FineractDirect/FineractDirectService.cs` | Real HTTP client with hybrid fallback |
| `src/CRMS.Infrastructure/ExternalServices/FineractDirect/MockFineractDirectService.cs` | Mock with real financial math (PMT formula, flat, equal-principal) |
| `src/CRMS.Infrastructure/Persistence/Migrations/20260320100000_AddFineractProductIdToLoanProduct.cs` | Adds nullable INT column to LoanProducts |

#### Files Modified

| File | Change |
|------|--------|
| `src/CRMS.Domain/Aggregates/ProductCatalog/LoanProduct.cs` | Added `FineractProductId` property + `Update()` parameter |
| `src/CRMS.Application/ProductCatalog/DTOs/LoanProductDto.cs` | Added `FineractProductId` to both `LoanProductDto` and `LoanProductSummaryDto` |
| `src/CRMS.Application/ProductCatalog/Mappings/LoanProductMappings.cs` | Maps `FineractProductId` in both `ToDto()` and `ToSummaryDto()` |
| `src/CRMS.Application/ProductCatalog/Commands/UpdateLoanProductCommand.cs` | Added `FineractProductId` parameter |
| `src/CRMS.Application/ProductCatalog/Commands/UpdateLoanProductHandler.cs` | Passes `FineractProductId` to `product.Update()` |
| `src/CRMS.Infrastructure/DependencyInjection.cs` | Registered `IFineractDirectService` with UseMock toggle, SSL handler, retry policy |
| `src/CRMS.Web.Intranet/appsettings.json` | Added `FineractDirect` config section |
| `src/CRMS.API/appsettings.json` | Added `FineractDirect` config section |
| `src/CRMS.Web.Intranet/Models/ApplicationModels.cs` | Added `FineractProductId` to `LoanProduct` model |
| `src/CRMS.Web.Intranet/Services/ApplicationService.cs` | `UpdateLoanProductAsync` + `GetAllLoanProductsAsync` map `FineractProductId` |
| `src/CRMS.Web.Intranet/Components/Pages/Admin/Products.razor` | Added FineractProductId input field in product modal |

#### Configuration

```json
// appsettings.json
{
  "FineractDirect": {
    "BaseUrl": "https://<host>:8443/core_banking/api",
    "Username": "<AdminUser>",
    "Password": "<AdminOffset>",
    "TenantId": "default",
    "TimeoutSeconds": 30,
    "UseMock": true
  }
}
```

To enable real Fineract: set `UseMock: false` and fill in credentials matching `TPDirectConfig` values from the TPM codebase.

**Build:** 0 errors. **Tests:** Domain + Application pass (2/2).

#### Offer Letter Generation (Session 27 continuation)

Complete end-to-end offer letter generation with proposed repayment schedule.

**Domain:** `OfferLetter` aggregate (versioning, schedule summary tracking: TotalInterest, TotalRepayment, MonthlyInstallment, InstallmentCount, ScheduleSource). `IOfferLetterRepository` interface.

**Application:** `GenerateOfferLetterCommand` + handler — loads approved application, resolves `FineractProductId` from product, calls `IFineractDirectService.CalculateRepaymentScheduleAsync` (hybrid), extracts committee conditions from `ApprovalConditions`, generates PDF, stores to file storage.

**Infrastructure:** `OfferLetterPdfGenerator` (QuestPDF) — professional PDF with: header, addressee, facility details table, **full repayment schedule table** (installment #, due date, principal, interest, total, outstanding), schedule summary box, numbered conditions, acceptance/signature section, footer. `OfferLetterRepository`. `OfferLetterConfiguration` (EF Core). Migration `20260320110000_AddOfferLettersTable`.

**UI:** "Offer Letter" button on Detail.razor action bar — visible only when `status == "Approved" || status == "Disbursed"`. Uses same pattern as Loan Pack button.

**Help page:** Added "Offer Letter" section under Loan Process sidebar nav. Full documentation covering: what it contains, how to generate, repayment schedule calculation (hybrid), versioning, who can generate, admin Fineract product mapping. Updated Operations role workflow to include offer letter step. Updated Approved status card to mention offer letter.

**Files created:** `OfferLetter.cs` (domain), `IOfferLetterRepository.cs`, `OfferLetterRepository.cs`, `OfferLetterConfiguration.cs`, `GenerateOfferLetterCommand.cs`, `IOfferLetterPdfGenerator.cs`, `OfferLetterPdfGenerator.cs`, `20260320110000_AddOfferLettersTable.cs`

**Files modified:** `DependencyInjection.cs` (4 registrations), `ApplicationService.cs`, `ApplicationServiceDtos.cs`, `Detail.razor`, `CRMSDbContext.cs`, `Help/Index.razor`

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v4.6
- [x] `docs/ImplementationTracker.md` → v5.0

---

## 5. Last Session Summary (2026-03-30 Session 39)

### Completed — Workflow Save Bugs (3 fixes)

Three related bugs in the workflow approval/return flow were fixed this session.

#### Bug 1 — "Failed to approve application" UI error despite DB success (`WorkflowInstanceRepository.Update`)

**Symptom:** Clicking "Approve" in BranchReview succeeded in the database (status changed to BranchApproved + CreditAnalysis), but the UI showed "Failed to approve application".

**Root cause:** `WorkflowInstanceRepository.Update()` unconditionally called `_context.WorkflowInstances.Update(instance)`. When the `WorkflowInstance` was already tracked in the circuit-scope DbContext (from the earlier `GetWorkflowByLoanApplicationHandler` call), EF Core's `DbSet.Update()` graph traversal marked ALL related entities — including existing `WorkflowTransitionLog` rows — as `Modified`. This generated unnecessary UPDATE statements that failed with constraint errors, causing `SaveChangesAsync` to throw inside `TransitionWorkflowHandler`, which propagated to the catch block returning "Failed to approve application".

**Fix — `WorkflowInstanceRepository.Update()`:** Applied the same `AutoDetectChangesEnabled = false` pattern as `LoanApplicationRepository.Update()`. When the entity is already tracked, skips `DbSet.Update()` entirely and only explicitly sets new (detached) `WorkflowTransitionLog` entries to `EntityState.Added`. When detached, calls `DbSet.Update()` but then corrects new log entries to `Added`.

```
src/CRMS.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs
```

#### Bug 2 — `AllCreditChecksCompletedWorkflowHandler` never persisted workflow transition to HOReview

**Symptom:** After credit checks completed, workflow stayed in CreditAnalysis instead of auto-advancing to HOReview.

**Root cause:** `AllCreditChecksCompletedWorkflowHandler.HandleAsync()` called `_workflowService.TransitionAsync()` (which calls `_instanceRepository.Update(instance)`) but never called `_unitOfWork.SaveChangesAsync()`. The handler runs in a fresh DI scope created by `DomainEventPublishingInterceptor` — it has its own `IUnitOfWork` instance, but SaveChanges was never invoked, so all changes were discarded when the scope disposed.

**Fix:** Injected `IUnitOfWork` into the handler and called `await _unitOfWork.SaveChangesAsync(ct)` after a successful `TransitionAsync`.

#### Bug 3 — `CommitteeDecisionWorkflowHandler` never persisted workflow/loan app changes

**Same root cause as Bug 2.** `CommitteeDecisionWorkflowHandler` called `_workflowService.TransitionAsync` and `_loanApplicationRepository.Update(loanApplication)` but never saved. Applied the same `IUnitOfWork` injection + `SaveChangesAsync` fix.

```
src/CRMS.Infrastructure/Events/Handlers/WorkflowIntegrationHandlers.cs
```

#### Bug 4 — `ReturnApplicationAsync` wrong status mapping + missing domain command

**Symptom:** Returning from BranchReview failed with a workflow transition error; HOReview return also mapped incorrectly.

**Root causes:**
1. Status mapping was wrong: `BranchReview → Draft` (should be `BranchReturned`) and `HOReview → CreditAnalysis` (should be `BranchReview` per workflow definition seeder)
2. For `BranchReview` return, `LoanApplication.Status` was never updated (only `WorkflowInstance.CurrentStatus` was changed, same gap as approve flow)

**Fix — `ApplicationService.ReturnApplicationAsync()`:**
- Fixed mapping: `BranchReview → BranchReturned`, `HOReview → BranchReview`
- Added domain command call for BranchReview: creates fresh scope, calls `ReturnFromBranchHandler` with `ReturnFromBranchCommand` (sets `LoanApplication.Status = BranchReturned`) before the workflow transition

```
src/CRMS.Web.Intranet/Services/ApplicationService.cs
```

#### Files Modified This Session

| File | Change |
|------|--------|
| `src/CRMS.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs` | `WorkflowInstanceRepository.Update()` rewritten with `AutoDetectChangesEnabled` pattern to prevent existing `WorkflowTransitionLog` rows being marked Modified |
| `src/CRMS.Infrastructure/Events/Handlers/WorkflowIntegrationHandlers.cs` | `AllCreditChecksCompletedWorkflowHandler` + `CommitteeDecisionWorkflowHandler`: added `IUnitOfWork` injection + `SaveChangesAsync` call |
| `src/CRMS.Web.Intranet/Services/ApplicationService.cs` | `ReturnApplicationAsync`: fixed status mapping (BranchReview→BranchReturned, HOReview→BranchReview) + added `ReturnFromBranchHandler` domain call for BranchReview returns |

**Build:** 0 compiler errors (MSB3026/MSB3021 are IIS file-lock warnings, not errors).

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [ ] `docs/UIGaps.md` → no new gaps discovered
- [x] `docs/ImplementationTracker.md` → v6.2

---

## 5. Previous Session Summary (2026-03-30 Session 38)

### Completed — `DbUpdateConcurrencyException` on Submit + Page Refresh Fix

Two bugs fixed this session, both related to the "Submit for Review" action on the Application Detail page.

#### Bug 1 — `DbUpdateConcurrencyException` on Submit

**Symptom:** Clicking "Submit for Review" always failed with `DbUpdateConcurrencyException`: EF Core generated `UPDATE LoanApplicationStatusHistory WHERE Id=@p9; SELECT ROW_COUNT()` and ROW_COUNT() returned 0.

**Root cause:** When `GetByIdAsync` loads the `LoanApplication` with `Include(x => x.StatusHistory)`, EF Core starts tracking the entity. The `application.Submit()` method adds a new `LoanApplicationStatusHistory` entry to the internal collection. When `_repository.Update(application)` subsequently called `_context.Entry(application)` (with `AutoDetectChangesEnabled = true`), EF Core ran `DetectChanges()`, found the new entry in the tracked collection, and began tracking it. Because the entry had a non-empty GUID PK, EF Core inferred it was an existing row and marked it `Modified`. The `else` branch then checked `State == EntityState.Detached` → false (already `Modified`), so nothing corrected it. `SaveChangesAsync` generated `UPDATE` instead of `INSERT` → row not found → ROW_COUNT() = 0 → exception.

**Fix — `LoanApplicationRepository.Update()`:** Wrapped the entire method body with `_context.ChangeTracker.AutoDetectChangesEnabled = false/true` in a try/finally. This prevents `_context.Entry(application)` from triggering premature `DetectChanges`. New `StatusHistory` entries remain `Detached` until explicitly checked and set to `Added`. `SaveChangesAsync` internally calls `DetectChanges` for root entity property changes (Status, SubmittedAt) correctly.

```csharp
_context.ChangeTracker.AutoDetectChangesEnabled = false;
try { ... explicit state assignments ... }
finally { _context.ChangeTracker.AutoDetectChangesEnabled = true; }
```

#### Bug 2 — Page Doesn't Refresh After Successful Submit

**Symptom:** After a successful submit, the page still showed "Draft" status. Required navigating away and back for the change to appear.

**Root cause:** In Blazor Server, `StateHasChanged()` was not being called explicitly after the async `LoadApplication()` completed in the event handler's success path.

**Fix — `Detail.razor`:** Added `StateHasChanged()` immediately after `await LoadApplication()` in the `if (result.Success)` block of the submit handler.

#### Files Modified This Session

| File | Change |
|------|--------|
| `src/CRMS.Infrastructure/Persistence/Repositories/LoanApplicationRepository.cs` | Wrapped `Update()` body with `AutoDetectChangesEnabled = false/true` to prevent premature `DetectChanges` from marking new `StatusHistory`/`Comments`/`Documents` as `Modified` |
| `src/CRMS.Web.Intranet/Components/Pages/Applications/Detail.razor` | Added `StateHasChanged()` after `LoadApplication()` in submit success path |

**Note — Debug logging left in (cleanup recommended):**
- `appsettings.Development.json`: `"Microsoft.EntityFrameworkCore.Database.Command": "Debug"` — added for diagnosis
- `DependencyInjection.cs`: `MaxBatchSize(1)` and `EnableSensitiveDataLogging()` — added for diagnosis; consider reverting before production

**Build:** 0 errors (no DI changes required).

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [ ] `docs/UIGaps.md` → no new gaps discovered
- [x] `docs/ImplementationTracker.md` → v6.1

---

## 5. Previous Session Summary (2026-03-29 Session 37)

### Completed — Bank Statement "Add Transactions" UI Fixes

Two separate sets of fixes in `ManageStatementTransactionsModal.razor` and `Detail.razor`:

#### Part 1 — "Add Txns" on existing statement shows no prior records

**Problem:** Clicking "Add Txns" on a statement that already had transactions opened the modal with only a blank row. No existing transactions were shown, and the running balance/reconciliation computed from `Statement.OpeningBalance` as if no transactions had been saved yet.

**Fix — `Detail.razor`:**
- `ShowManageStatementTransactionsModal` changed from `void` to `async Task`
- When `stmt.TransactionCount > 0`, calls `AppService.GetStatementTransactionsAsync(statementId)` and stores in new `existingStatementTransactions` field
- `existingStatementTransactions` passed to modal as `ExistingTransactions` parameter; cleared on close and on save success

**Fix — `ManageStatementTransactionsModal.razor`:**
- New `[Parameter] ExistingTransactions` (`List<StatementTransactionInfo>?`)
- New `BaseBalance` computed property: last existing transaction's `RunningBalance` (ordered by date), or `Statement.OpeningBalance` when none
- `ComputeRunningBalance`, `ComputedClosing`, and `Save()` running-balance stamp all switched from `Statement.OpeningBalance` to `BaseBalance`
- Existing transactions rendered as a read-only section at the top of the table (gray rows, lock icon banner); a blue "New transactions — continuing from ₦X" separator precedes the editable rows

#### Part 2 — Four additional bugs in `ManageStatementTransactionsModal`

| # | Bug | Fix |
|---|-----|-----|
| 1 | **`@bind` on Description/Reference clears typed text** — when `OnDebitChanged`/`OnCreditChanged` called `StateHasChanged()`, Blazor re-rendered and reset `value=@row.Description` to the stale model value (empty), wiping whatever the user had typed before tabbing to the amount field | Changed to `@bind:event="oninput"` so `row.Description`/`row.Reference` are always in sync with keystrokes; DOM patch is a no-op on re-render |
| 2 | **Partial save never refreshed parent** — when backend returned `Success=true` with a warning message (some rows outside period), code showed the message but never called `OnSuccess`, so `LoadApplication()` never ran; statement list stayed stale | Removed the message gate; `OnSuccess` is now always called on any `result.Success` |
| 3 | **"Not Reconciled" warning fired prematurely** — empty new row (`rows.Any() = true`) triggered the orange warning immediately on modal open, before any data was entered | Tightened condition to `rows.Any(r => r.DebitAmount > 0 \|\| r.CreditAmount > 0)` |
| 4 | **"Total Credits / Total Debits" labels misleading in Add-Txns mode** — these summary stats only counted new rows; with existing transactions loaded, users saw partial totals with no explanation | Labels now read "New Credits" / "New Debits" when `ExistingTransactions?.Any() == true` |

#### Files Modified This Session

| File | Change |
|------|--------|
| `src/CRMS.Web.Intranet/Components/Pages/Applications/Detail.razor` | `ShowManageStatementTransactionsModal` → async; `existingStatementTransactions` field; pass `ExistingTransactions` to modal; clear on close/success |
| `src/CRMS.Web.Intranet/Components/Pages/Applications/Modals/ManageStatementTransactionsModal.razor` | `ExistingTransactions` parameter; `BaseBalance` property; read-only existing rows section; `@bind:event="oninput"` on Description/Reference; always invoke `OnSuccess` on success; tightened reconciliation warning; "New Credits"/"New Debits" labels |

**Build:** Not verified this session (UI-only changes, no new handlers, no DI changes needed).

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [ ] `docs/UIGaps.md` → no new gaps discovered
- [x] `docs/ImplementationTracker.md` → v6.0

---

## 5. Previous Session Summary (2026-03-28 Session 36)

### Completed — `DbUpdateConcurrencyException` Bug Fix in Save Transactions

Fixed the crash that occurred when clicking "Save Transactions" in `ManageStatementTransactionsModal` after uploading an Excel statement. The bank statement header was successfully created, but saving transactions always failed with "Failed to add transactions".

#### Root Cause

EF Core 9's handling of the optional `OwnsOne(CashflowSummary)` owned entity using table-splitting on the `BankStatements` table. `CashflowSummary` is null after initial upload (no transactions analysed yet). When `ValidateDataIntegrity()` is called at the end of `AddTransactionsHandler`, it sets three null→non-null properties (`BalanceReconciled`, `CalculatedClosingBalance`, `BalanceDiscrepancy`) on the `BankStatement`, marking it as `Modified`. EF Core 9 then generates a **separate UPDATE** for the owned entity entry, using `CS_PeriodMonths = NULL` (and similar for all other CS_* columns) instead of `IS NULL` in the WHERE clause. MySQL's `column = NULL` evaluates to UNKNOWN (never TRUE) → 0 rows affected → `DbUpdateConcurrencyException` thrown.

Confirmed by server log: `Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException: The database operation was expected to affect 1 row(s), but actually affected 0 row(s)` at `AddTransactionsCommand.cs:62`.

`UseAffectedRows=false` in the connection string means MySQL returns *matched* rows — so 0 truly means the WHERE clause matched nothing, not "no values changed".

#### Three Fixes Applied

**Fix 1 — `BankStatementConfiguration.cs`:** Added `builder.Navigation(x => x.CashflowSummary).IsRequired(false)` after the `OwnsOne` block. This marks the navigation as optional so EF Core does not generate owned-entity null-check WHERE conditions.

**Fix 2 — `AddTransactionsCommand.cs`:** Changed from calling `_repository.Update(statement)` (which would trigger a full graph update) to collecting the new `StatementTransaction` entities via `result.Value` from each `AddTransaction` call, then calling `_repository.AttachNewTransactions(newTransactions)`. Since the `BankStatement` is already tracked by EF Core (loaded via `GetByIdWithTransactionsAsync`), EF's automatic `DetectChanges()` at `SaveChangesAsync` correctly picks up the scalar property changes without touching the owned entity.

**Fix 3 — `IBankStatementRepository` + `BankStatementRepository.cs`:** Added `AttachNewTransactions(IEnumerable<StatementTransaction>)` to both the interface and the implementation. The implementation marks each new transaction as `EntityState.Added`.

#### Why This Differs From the Session 35 Fix

Session 35 fixed `BankStatementRepository.Update()` to handle the detached-entity problem when `Update()` IS called. This session's fix goes further: `Update()` is no longer called at all in `AddTransactionsHandler` — the `BankStatement` is already tracked and EF handles its scalar changes automatically. This avoids the EF Core 9 owned-entity UPDATE generation entirely.

#### Files Modified This Session

| File | Change |
|------|--------|
| `src/CRMS.Infrastructure/Persistence/Configurations/StatementAnalysis/BankStatementConfiguration.cs` | Added `builder.Navigation(x => x.CashflowSummary).IsRequired(false)` |
| `src/CRMS.Application/StatementAnalysis/Commands/AddTransactionsCommand.cs` | Collect `result.Value` per transaction; call `AttachNewTransactions`; removed `_repository.Update(statement)` |
| `src/CRMS.Domain/Interfaces/IBankStatementRepository.cs` | Added `AttachNewTransactions(IEnumerable<Aggregates.StatementAnalysis.StatementTransaction>)` |
| `src/CRMS.Infrastructure/Persistence/Repositories/BankStatementRepository.cs` | Implemented `AttachNewTransactions` |

**Build:** 0 errors (file-lock MSB3021/MSB3027 only — IIS Express holding DLLs, not compiler errors).

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [ ] `docs/UIGaps.md` → no UI changes this session
- [x] `docs/ImplementationTracker.md` → v5.9

---

## 5. Previous Session Summary (2026-03-26 Session 35)

### Completed — Submit for Review End-to-End Bug Fixes (4 bugs)

The entire Submit for Review flow was broken across four separate layers. All four bugs were identified and fixed in this session.

#### Bug 1 — `ManageStatementTransactionsModal` Save giving "Failed to add transactions"

**Root cause:** `BankStatementRepository.Update()` called `_context.BankStatements.Update(statement)`, which traversed the entity graph and marked newly-added `StatementTransaction` entities as `Modified` (not `Added`). EF Core's `Update()` does this because new entities with non-empty Guid keys (assigned in the `Entity` base constructor) are indistinguishable from existing ones. EF generated UPDATE SQL for rows that didn't exist → silent no-op / DB exception.

**Fix in** `src/CRMS.Infrastructure/Persistence/Repositories/BankStatementRepository.cs`:
```csharp
public void Update(BankStatement statement)
{
    var newTransactions = statement.Transactions
        .Where(t => _context.Entry(t).State == EntityState.Detached).ToList();
    _context.BankStatements.Update(statement);
    foreach (var txn in newTransactions)
        _context.Entry(txn).State = EntityState.Added;
}
```

#### Bug 2 — "Bank statement is required" despite statements being uploaded

**Two instances of the same wrong-collection check:**

- `Detail.razor ValidateForSubmission()` checked `application.Documents.Any(d => d.Category == "BankStatement")` — wrong. `Documents` contains `LoanApplicationDocument` (Documents tab). Bank statements live in `application.BankStatements` (Statements tab, separate `BankStatement` aggregate). Fixed to `application.BankStatements.Any()`.

- `LoanApplication.Submit()` also checked `_documents` (same wrong collection). Removed the check entirely — cross-aggregate validation belongs in the Application command handler, not the domain aggregate.

#### Bug 3 — Submit button "not doing anything"

**Root cause (two parts):**
1. The domain `Submit()` always returned failure due to Bug 2, so the command handler always returned `ApplicationResult.Failure(...)`.
2. `SubmitForReview()` in `Detail.razor` had no `else` branch — `result.Success == false` was completely silently ignored. Modal stayed open, no feedback shown.

**Fix in** `Detail.razor`: added `submitError` field, populated on failure, displayed as alert in modal body. Also added `submitError = null` in `ShowSubmitForReviewModal` and `CloseSubmitReviewModal`.

#### Bug 4 — "Failed to submit application" exception after fixes 1-3

**Root cause:** `LoanApplicationRepository.Update()` had the exact same EF Core tracking issue as Bug 1. When `application.Submit()` calls `AddStatusHistory()`, a new `LoanApplicationStatusHistory` entity is added to `_statusHistory`. `_context.LoanApplications.Update(application)` then marked that new entity as `Modified` → EF tried to UPDATE a non-existent row → DB exception → caught in `ApplicationService.SubmitApplicationAsync` catch block → returned "Failed to submit application".

**Fix in** `src/CRMS.Infrastructure/Persistence/Repositories/LoanApplicationRepository.cs`:
```csharp
public void Update(LA.LoanApplication application)
{
    var newStatusHistory = application.StatusHistory
        .Where(h => _context.Entry(h).State == EntityState.Detached).ToList();
    var newComments = application.Comments
        .Where(c => _context.Entry(c).State == EntityState.Detached).ToList();
    var newDocuments = application.Documents
        .Where(d => _context.Entry(d).State == EntityState.Detached).ToList();
    _context.LoanApplications.Update(application);
    foreach (var h in newStatusHistory) _context.Entry(h).State = EntityState.Added;
    foreach (var c in newComments) _context.Entry(c).State = EntityState.Added;
    foreach (var d in newDocuments) _context.Entry(d).State = EntityState.Added;
}
```

#### Systemic Pattern Note

The root cause of Bugs 1 and 4 is the same EF Core behavior: any repository `Update()` method that calls `DbSet.Update(aggregate)` will silently fail to INSERT new child entities with non-empty Guid keys. The fix pattern (capture Detached entities before `Update()`, re-mark as `Added` after) has now been applied to both `BankStatementRepository` and `LoanApplicationRepository`. **Any other repository using the same pattern should receive the same fix.**

#### Files Modified This Session

| File | Change |
|------|--------|
| `src/CRMS.Infrastructure/Persistence/Repositories/BankStatementRepository.cs` | Fix EF tracking bug — re-mark new transactions as `Added` after `Update()` |
| `src/CRMS.Infrastructure/Persistence/Repositories/LoanApplicationRepository.cs` | Fix EF tracking bug — re-mark new StatusHistory/Comments/Documents as `Added` after `Update()` |
| `src/CRMS.Domain/Aggregates/LoanApplication/LoanApplication.cs` | Remove bank statement check from `Submit()` (wrong aggregate; moved to handler) |
| `src/CRMS.Application/LoanApplication/Commands/SubmitLoanApplicationCommand.cs` | Inject `IBankStatementRepository`; add cross-aggregate bank statement check before `Submit()` |
| `src/CRMS.Web.Intranet/Components/Pages/Applications/Detail.razor` | Fix `ValidateForSubmission()` (Documents → BankStatements); add `submitError` field + alert; add error handling in `SubmitForReview()` |

**Build:** 0 errors (no new files, no DI changes needed).

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v5.3
- [x] `docs/ImplementationTracker.md` → v5.8

---

## 5. Last Session Summary (2026-03-25 Session 34)

### Completed — Offer Letter Download + History Tab

Implemented comprehensive offer letter download: both immediate download after generation (already partially wired) and re-download of any previously generated version via a new "Offer Letters" tab.

#### What Was Missing

The `GenerateOfferLetter` button already called `DownloadGeneratedFileAsync` + `downloadFileFromBytes` for immediate download. However:
- No history was shown — once the user left the page, there was no way to download a previously generated offer letter without regenerating it.
- The SESSION_HANDOFF described this as "shows alert with filename" which was superseded by earlier work, but the re-download gap remained.

#### Architecture

```
IOfferLetterRepository.GetAllByLoanApplicationIdAsync (new)
→ GetOfferLettersByApplicationQuery / Handler (new Application layer)
→ ApplicationService.GetOfferLettersByApplicationAsync + DownloadOfferLetterAsync
→ Detail.razor "Offer Letters" tab
```

#### Files Created
- `src/CRMS.Application/OfferLetter/Queries/OfferLetterQueries.cs` — `GetOfferLettersByApplicationQuery` + `GetOfferLettersByApplicationHandler` returning `List<OfferLetterSummaryDto>`

#### Files Modified
- `src/CRMS.Domain/Interfaces/IOfferLetterRepository.cs` — added `GetAllByLoanApplicationIdAsync`
- `src/CRMS.Infrastructure/Persistence/Repositories/OfferLetterRepository.cs` — implemented it (ordered by version desc)
- `src/CRMS.Infrastructure/DependencyInjection.cs` — registered `GetOfferLettersByApplicationHandler`
- `src/CRMS.Web.Intranet/Models/ApplicationModels.cs` — added `OfferLetterInfo` model
- `src/CRMS.Web.Intranet/Services/ApplicationService.cs` — added `GetOfferLettersByApplicationAsync` + `DownloadOfferLetterAsync(offerLetterId)`
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Detail.razor`:
  - `offerLetters` list loaded in `LoadApplication()` for Approved/Disbursed
  - After generate: list refreshed before auto-download
  - "Offer Letters" tab (only when `CanGenerateOfferLetter`) with count badge
  - Table: version badge, filename, size, status badge, generated-by, timestamp, per-row download with individual spinner
  - `DownloadOfferLetter(Guid)` method + `FormatFileSize` helper

**Build:** 0 errors.

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v5.2
- [x] `docs/ImplementationTracker.md` → v5.7

---

## 5. Last Session Summary (2026-03-25 Session 33)

### Completed — Bank Statement Save Button Fix + Display/UX Hardening

Four bugs in `ManageStatementTransactionsModal.razor` were identified and fixed. All are in the same file.

#### Root Causes and Fixes

| Bug | Symptom | Fix |
|-----|---------|-----|
| `ToString("N2")` on `<input type="number">` | Comma-formatted strings (e.g. `"2,300,000.00"`) are invalid for number inputs — browser silently discards → field shows placeholder "0.00" → preloaded debit/credit appear blank → `CanSave = false` → button disabled | Changed to `ToString("F2", CultureInfo.InvariantCulture)` (plain decimal, no commas) |
| Missing `StateHasChanged()` after `isSaving = true` | Spinner never appeared because Blazor wouldn't re-render until the first `await` returned — by which time saving was complete | Added `StateHasChanged()` immediately after `isSaving = true` |
| No `catch` block in `Save()` | Unhandled exceptions (DI resolution failure, network error, etc.) propagated to Blazor circuit — crashed silently with no user feedback | Added `catch (Exception ex)` → sets `error` field with readable message |
| Modal body missing `min-height: 0` | Flex child without `min-height: 0` can overflow its container in some browsers → footer (Save button) scrolled off-screen | Added `min-height: 0` to the scrollable body div |

Also added a disabled-state hint: when Save is disabled and rows exist, a small note says "All rows need a description and at least one amount." so users know what's blocking them.

#### Why `CanSave` Was Consistently False

The `ToString("N2")` bug caused a cascade:
1. Preloaded rows displayed as empty in the number inputs (placeholder "0.00" visible)
2. If user ever focused and blurred those inputs, `@onchange` fired with an empty string → `OnDebitChanged`/`OnCreditChanged` set both amounts to `null`
3. `CanSave` requires `DebitAmount > 0 OR CreditAmount > 0` per row → false → button disabled
4. Disabled HTML buttons silently swallow all click events — no transition, no error

### Files Modified This Session
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Modals/ManageStatementTransactionsModal.razor`
  - `value=` for debit/credit inputs: `"N2"` → `"F2"` with `CultureInfo.InvariantCulture`
  - `Save()`: added `StateHasChanged()` after `isSaving = true`
  - `Save()`: added `catch (Exception ex)` block
  - Modal body div: added `min-height: 0` to flex style
  - Footer: disabled-state hint below Save button

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v5.1
- [x] `docs/ImplementationTracker.md` → v5.6

---

## 5. Previous Session Summary (2026-03-25 Session 32)

### Completed — External Bank Statement Transaction Entry Pipeline + CSV/Excel Auto-Parsing + Format Guidance

This session completed the missing half of the Other Bank Statements feature: previously only header details (bank name, account, period, balances) could be saved. Now loan officers can upload a file, have transactions auto-detected, review/correct rows in a grid, and save them — making Verify and Analyze functional.

#### 1. Transaction Entry Pipeline (Four Root Causes Fixed)

| Root cause | Fix |
|---|---|
| No `<InputFile>` in upload modal | Added `InputFile` accepting `.pdf,.csv,.xlsx,.xls`; `HandleFileSelected`; file size hint |
| File path always `null` in storage | Read file into `byte[]` once; upload via `IFileStorageService.UploadAsync` |
| No mechanism to enter/save transactions | Created `ManageStatementTransactionsModal.razor` + `AddStatementTransactionsAsync` |
| `Verify` always failed (`BalanceReconciled` never set); `Analyze` always failed (0 transactions) | Called `ValidateDataIntegrity()` at end of `AddTransactionsCommand` handler |

#### 2. CSV/Excel Auto-Parsing (`StatementFileParserService.cs` — NEW)

Stateless singleton. Routes to `ParseExcel` (ClosedXML) or `ParseCsv` by extension. No new NuGet packages needed — ClosedXML was already in `CRMS.Web.Intranet.csproj`.

Key capabilities:
- Scans up to 20 rows to find header row
- Auto-detects CSV delimiter (comma, pipe, tab, semicolon) from first 5 lines
- Recognises 40+ column name variants across 7 fields (Date, Description, Debit, Credit, Amount, Balance, Reference)
- 18 Nigerian bank date formats (`TryParseDate`)
- `CleanAmount` strips ₦, #, commas, leading currency letters
- ±5 day tolerance on period boundary validation

#### 3. `ManageStatementTransactionsModal.razor` (NEW)

Full transaction entry grid with:
- Balance summary bar: Opening, Total Credits, Total Debits, Computed Closing (color-coded green/orange), Expected Closing, Discrepancy
- Row-by-row: Date (constrained to period), Description, Reference, Debit, Credit (mutually exclusive), Running Balance (auto-computed, read-only)
- Live reconciliation: `|ComputedClosing − ExpectedClosing| ≤ ₦1`
- Parse message banner (green when rows auto-populated, yellow for manual entry)
- `OnInitialized` seeds from `PreloadedTransactions` if provided, else adds one blank row
- `Save()` stamps running balances before calling `AppService.AddStatementTransactionsAsync`

#### 4. UX Flow

Upload modal → if CSV/Excel: auto-parse → `OnSuccess(StatementUploadResult)` → Detail.razor reloads → auto-opens `ManageStatementTransactionsModal` with pre-populated rows and parse message banner. If PDF or unparseable: modal opens with one blank row and informational note.

#### 5. Help Page — `RenderTabStatements()` Rewritten (5 sections)

1. Statement Sources & Trust (updated)
2. How to Add an External Bank Statement (5-step workflow)
3. File Format Guide (column table, 10 date format examples, sample CSV, export instructions for 6 Nigerian banks)
4. Troubleshooting (4 error messages with cause and fix)
5. Metrics Analyzed (retained)

#### 6. `AddTransactionsCommand.cs` Fix

Changed `GetByIdAsync` → `GetByIdWithTransactionsAsync` (safe with existing transactions). Added `statement.ValidateDataIntegrity()` after all transactions added — this sets `BalanceReconciled = true` when closing balance matches, which unblocks `VerifyStatementCommand` downstream.

### Files Created This Session
- `src/CRMS.Web.Intranet/Services/StatementFileParserService.cs`
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Modals/ManageStatementTransactionsModal.razor`

### Files Modified This Session
- `src/CRMS.Application/StatementAnalysis/Commands/AddTransactionsCommand.cs` — `GetByIdWithTransactionsAsync` + `ValidateDataIntegrity()`
- `src/CRMS.Web.Intranet/Services/ApplicationService.cs` — `UploadExternalStatementAsync` returns `ApiResponse<StatementUploadResult>`; new `AddStatementTransactionsAsync`
- `src/CRMS.Web.Intranet/Models/ApplicationModels.cs` — Added `StatementTransactionRow`, `StatementUploadResult`, `StatementParseResult`
- `src/CRMS.Web.Intranet/Program.cs` — Registered `StatementFileParserService` as Singleton
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Modals/UploadExternalStatementModal.razor` — `InputFile`, format guide panel, `EventCallback<StatementUploadResult>`
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Tabs/StatementsTab.razor` — `OnEnterTransactions` param; "Enter Txns" button; Verify/Analyze disabled when `TransactionCount == 0`
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Detail.razor` — `ManageStatementTransactionsModal` wiring; `OnBankStatementUploaded(StatementUploadResult)` auto-opens modal
- `src/CRMS.Web.Intranet/Components/Pages/Help/Index.razor` — `RenderTabStatements()` rewritten with 5 sections

**Build:** 0 errors (verified at 3 checkpoints). 0 new NuGet packages added.

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v5.0
- [x] `docs/ImplementationTracker.md` → v5.5

---

## 5. Previous Session Summary (2026-03-22 Session 31)

### Completed — M-Series + L-Series Bug Fixes (All 19 M + All 8 L)

Two-session run that addressed all 19 M-series bugs and all 8 L-series quality issues identified in a comprehensive bug review. All confirmed build-clean (0 errors).

#### M-Series Highlights (19 bugs)

| ID | Fix |
|----|-----|
| M-1 | FSV > MV validation in `SetCollateralValuationModal.razor` |
| M-2 | Committee setup `CanSubmit` guard: `manualMinApproval <= manualRequiredVotes` |
| M-3 | `SetupCommitteeModal` uses `AuthService.CurrentUser?.Id` instead of `Guid.Empty` |
| M-4 | Collateral haircut defaults read from `CollateralHaircutSettings` (injected via `IOptions<T>`) |
| M-5 | `FillPartyInfoModal.IsValid` false-return removed — form is valid when no required fields remain |
| M-6 | Committee vote amount/tenor/rate range validation before `RecordDecision()` |
| M-7 | Offer letter status check uses `LoanApplicationStatus` enum instead of magic strings |
| M-8 | Offer letter `GenerateOfferLetterCommand` accepts `BankName`/`BranchName`; `BankSettings` config class; `IOptions<BankSettings>` injected in `ApplicationService` |
| M-9 | Balance sheet save guarded: fails with clear message if `Assets ≠ Liabilities + Equity` |
| M-10 | `MandateType` mapping reverted to `p.Designation` (domain entity only has `Designation`; was a false positive) |
| M-12 | Audit trail free-text search extended through all 5 layers: `IAuditLogRepository.SearchAsync` → Application query → `ApplicationService` → `Audit.razor` |
| M-13 | Dashboard fake demo data removed (no longer fabricates 156 applications + 8 pending tasks) |
| M-14 | `UpdateCollateralHandler` created + DI registered; `UpdateCollateralAsync` wired in `ApplicationService` |
| M-15 | `UpdateGuarantorHandler` created + DI registered (decimal params — domain takes raw values, not Money objects) |
| M-16 | All 6 admin pages protected with `[Authorize(Roles = "SystemAdmin")]` |
| M-17 | `GetUsersAsync` maps `LocationId`; `UserSummaryDto`/`UserSummary` carry `LocationId`; Users admin wires it |
| M-18 | Committee context `RiskRating` changed from `"Medium"` to `"N/A"` (advisory data not available there) |
| M-19 | `CommitteeReviewSummaryDto` + `CommitteeReviewSummary` carry `FinalDecision`; Reviews page uses it |
| M-20 | Overdue items skip null `SLADueAt` before mapping `SLABreachedAt` |

**New files (M-series):** `BankSettings.cs`, `CollateralHaircutSettings.cs`, `AppStatus.cs` (started for L-3)

#### L-Series Highlights (8 issues)

| ID | Fix |
|----|-----|
| L-1 | Profile/Settings already use real backend (confirmed in code review — no fix needed) |
| L-2 | `CommentsTab.razor` already uses `DateTime.UtcNow` (confirmed — no fix needed) |
| L-3 | `AppStatus.cs` constants class created; all status string literals in `Detail.razor` replaced — `ShowApproveButton`, `ShowRejectButton`, `ShowReturnButton`, `CanGeneratePack`, `CanGenerateOfferLetter`, `IsApplicationEditable`, `ShowSubmitForReviewButton`, `CanSetupCommitteeReview`, `CanManageValuation`, `CanManageGuarantors`, `FormatStatus()`, `GetStatusBadgeClass()` |
| L-4 | Client-side pagination (page size 15) added to `Users.razor`, `Products.razor`, `Templates.razor`, `Committees.razor`; filter changes reset to page 1 |
| L-5 | Help page `searchQuery` now filters nav items via `HelpNavItems` list (40 entries) + `SearchResults` computed property; shows "Search Results" category when non-empty |
| L-6 | `AddComment` in `Detail.razor` has try/catch, `isAddingComment` loading state, `commentError` field; `CommentsTab` accepts `IsSubmitting`/`SubmitError` params; textarea + button disable while submitting |
| L-7 | Two-transaction pattern confirmed correct by design (application create + optional bank statement) |
| L-8 | Calendar month diff in `UploadExternalStatementModal.periodError` — uses `Year*12 + Month` diff with day adjustment, not `TotalDays/30` |

### Files Created This Session
- `src/CRMS.Web.Intranet/Models/AppStatus.cs` — status string constants
- `src/CRMS.Web.Intranet/Services/BankSettings.cs` — bank name/branch config
- `src/CRMS.Web.Intranet/Services/CollateralHaircutSettings.cs` — collateral haircut % by type

### Files Modified This Session (key)
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Detail.razor` — AppStatus constants, auth guard, error handling
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Tabs/CommentsTab.razor` — IsSubmitting/SubmitError params
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Modals/UploadExternalStatementModal.razor` — calendar month calc
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Modals/SetCollateralValuationModal.razor` — FSV validation, haircut from settings
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Modals/SetupCommitteeModal.razor` — CanSubmit guard, auth
- `src/CRMS.Web.Intranet/Components/Pages/Admin/Users.razor` — pagination, LocationId
- `src/CRMS.Web.Intranet/Components/Pages/Admin/Products.razor` — pagination
- `src/CRMS.Web.Intranet/Components/Pages/Admin/Templates.razor` — pagination with property-setter filter reset
- `src/CRMS.Web.Intranet/Components/Pages/Admin/Committees.razor` — pagination
- `src/CRMS.Web.Intranet/Components/Pages/Help/Index.razor` — search filtering (HelpNavItems + SearchResults)
- `src/CRMS.Application/Collateral/Commands/CollateralCommands.cs` — UpdateCollateralHandler
- `src/CRMS.Application/Guarantor/Commands/GuarantorCommands.cs` — UpdateGuarantorHandler
- `src/CRMS.Application/Committee/Commands/CommitteeCommands.cs` — vote range validation
- `src/CRMS.Application/FinancialAnalysis/Commands/FinancialStatementCommands.cs` — balance sheet validation
- `src/CRMS.Application/Committee/DTOs/CommitteeDtos.cs` — FinalDecision field
- `src/CRMS.Application/Identity/DTOs/AuthDtos.cs` — LocationId field
- `src/CRMS.Domain/Interfaces/IAuditRepository.cs` — searchTerm param
- `src/CRMS.Infrastructure/Persistence/Repositories/AuditRepositories.cs` — searchTerm filter
- `src/CRMS.Infrastructure/DependencyInjection.cs` — UpdateCollateralHandler + UpdateGuarantorHandler
- `src/CRMS.Web.Intranet/Program.cs` — BankSettings + CollateralHaircutSettings config
- `src/CRMS.Web.Intranet/appsettings.json` — BankSettings + CollateralHaircuts sections

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v4.9
- [x] `docs/ImplementationTracker.md` → v5.4

---

## 5. Previous Session Summary (2026-03-22 Session 30)

### Completed — Bug Fixes: Settings Persistence, Audit Pagination, Auth Guard, Collateral Mapping

Addressed 6 outstanding bugs from `TabModalReviewReport.md` (C-4, C-5, C-6) and prior session's C6/C7/C8 list. All confirmed build-clean.

#### 1. Settings Page Persistence (C7)

**Previously:** `SaveSettings()` was `await Task.Delay(300)` — settings were never saved.

**Now:** `Settings/Index.razor` uses `ILocalStorageService`:
- `OnInitializedAsync` loads saved settings from `localStorage["userSettings"]`
- `SaveSettings()` serializes all 9 settings fields to localStorage; shows success banner
- `ResetToDefaults()` resets in-memory state then calls `SaveSettings()` to persist the reset

#### 2. Audit Trail Pagination + Search (C8)

**Previously:** `totalCount = 150`, `totalPages = 8` hardcoded; Previous/Next buttons had no `@onclick`.

**Now:**
- Added `SearchAuditLogsAsync()` to `ApplicationService.cs` using existing `SearchAuditLogsHandler`
  - Passes action filter, date range, `pageNumber`, `pageSize` to backend
  - `SearchAuditLogsHandler` registered in `DependencyInjection.cs` (was missing)
- `totalCount` / `totalPages` populated from real backend result
- `PreviousPage()` and `NextPage()` methods wired to buttons; Search resets to page 1
- Free-text search term applied client-side (backend query has no free-text param)

#### 3. Null User Auth Guard on Workflow Actions (C-4)

**Previously:** 15+ places had `var userId = AuthService.CurrentUser?.Id ?? Guid.Empty` — if session expired, actions would fire with `Guid.Empty` userId and `"User"` role.

**Now:**
- Added `EnsureAuthenticated(out Guid userId)` helper in `Detail.razor`: if `CurrentUser == null`, navigates to `/login` with `forceLoad: true` and returns false
- All 15 occurrences replaced with `if (!EnsureAuthenticated(out var userId)) return;` via `replace_all`
- Single `userRole` line changed to `AuthService.CurrentUser!.Roles.FirstOrDefault() ?? "User"` (non-null after guard)

#### 4. Collateral MarketValue/ForcedSaleValue Mapping (C-5)

**Previously:** `GetCollateralsForApplicationAsync` used `GetCollateralByLoanApplicationHandler` which returns `CollateralSummaryDto` — this DTO only has `AcceptableValue`. Both `MarketValue` and `ForcedSaleValue` were mapped from `AcceptableValue` (wrong).

**Now:** Fetches summary list first (for IDs), then calls `GetCollateralByIdHandler` per item to get full `CollateralDto`. `MarketValue` = `c.MarketValue.GetValueOrDefault()`, `ForcedSaleValue` = `c.ForcedSaleValue.GetValueOrDefault()`.

#### 5. Per-Item LTV Calculation (C-6)

**Previously:** `LoanToValue = 0m` hardcoded on every collateral.

**Now:** `GetCollateralsForApplicationAsync` accepts `decimal loanAmount` (caller passes `app.RequestedAmount`). LTV = `Math.Round((loanAmount / acceptableValue) * 100, 2)` per item.

#### 6. ProcessLoanCreditChecksHandler Namespace Fix

`RequestBureauChecksAsync` in `ApplicationService.cs` used unqualified `ProcessLoanCreditChecksHandler` and `ProcessLoanCreditChecksCommand` — these aren't imported. Fixed with fully qualified names (`CRMS.Application.CreditBureau.Commands.*`).

**Build:** 0 errors. All fixes confirmed clean.

### Files Modified This Session
- `src/CRMS.Web.Intranet/Components/Pages/Settings/Index.razor` — localStorage load/save
- `src/CRMS.Web.Intranet/Components/Pages/Reports/Audit.razor` — SearchAuditLogsAsync + pagination
- `src/CRMS.Web.Intranet/Services/ApplicationService.cs` — SearchAuditLogsAsync, GetCollateralsForApplicationAsync refactor (full CollateralDto + LTV), namespace fix
- `src/CRMS.Infrastructure/DependencyInjection.cs` — SearchAuditLogsHandler registration
- `src/CRMS.Web.Intranet/Components/Pages/Applications/Detail.razor` — EnsureAuthenticated helper + 15 userId guard replacements

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v4.8
- [x] `docs/ImplementationTracker.md` → v5.3

---

## 5. Previous Session Summary (2026-03-21 Session 29)

### Completed — Comprehensive Scoring Parameters Seeder + Consent Flow Review

#### 1. Scoring Parameters Seeder Enhancement

**Problem:** The `/admin/scoring` page only showed 12 basic parameters (e.g., "Credit Score Weight", "Minimum Credit Score"), but the `ScoringConfigurationService` and `RuleBasedScoringEngine` expect ~80 parameters across 9 categories. Missing parameters fell back to hardcoded defaults and were NOT visible/editable in the admin UI.

**Solution:** Completely rewrote `ComprehensiveDataSeeder.SeedScoringParametersAsync()` to seed all 82 scoring parameters:

| Category | Count | Examples |
|----------|-------|----------|
| Weights | 5 | Category weights (must sum to 1.0) |
| CreditHistory | 21 | ExcellentCreditScoreBonus (+20), DefaultPenalty (-30), DelinquencyPenalty (-15), HighFraudRiskPenalty (-25), MissingBureauDataPenaltyPerParty (-5), etc. |
| FinancialHealth | 14 | StrongCurrentRatioBonus, HighLeveragePenalty, LossMakingPenalty, etc. |
| Cashflow | 20 | InternalStatementBonus, NegativeCashflowPenalty, GamblingPenalty, etc. |
| DSCR | 13 | ExcellentDSCR threshold, DSCR scores, InterestCoverage adjustments |
| Collateral | 9 | LTV thresholds and scores, lien status adjustments |
| Recommendations | 7 | StrongApproveMinScore, ApproveMaxRedFlags, CriticalRedFlagsThreshold |
| LoanAdjustments | 13 | Amount multipliers, interest rate adjustments, tenor restrictions |
| StatementTrust | 5 | Trust weights for CoreBanking, OpenBanking, MonoConnect, ManualUpload |

All parameters include proper min/max constraints and sort order for consistent UI display.

**To apply new parameters on existing database:**
1. Clear tables: `DELETE FROM ScoringParameterHistory; DELETE FROM ScoringParameters;`
2. Restart application — seeder will populate all 82 parameters
3. OR use "Seed Default Parameters" button in `/admin/scoring` (only works if table is empty)

#### 2. Consent Flow Review

Reviewed the `CRMS.Application.Consent.Commands` namespace and clarified its role:

- **`RecordConsentCommand`** — Records consent for a single party (individual or business)
- **`RecordBulkConsentCommand`** — Records consent for ALL parties in a loan application (directors, signatories, guarantors, business entity)
- **Integration:** `ProcessLoanCreditChecksCommand` verifies consent exists before calling credit bureau APIs (NDPA compliance)

**Current approach:** Offline consent (paper forms signed by parties, loan officer records in system). This is acceptable for banks still using physical consent forms.

**Future enhancement (not implemented):** OTP-based consent verification where system sends SMS to party's phone, party provides OTP to loan officer, system validates and activates consent. Infrastructure is ready to extend when needed.

**Build:** 0 errors. **Tests:** All pass.

### Files Modified This Session
- `src/CRMS.Infrastructure/Persistence/ComprehensiveDataSeeder.cs` — Complete rewrite of `SeedScoringParametersAsync()` (12 → 82 parameters)

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/ImplementationTracker.md` → v5.2
- [ ] `docs/UIGaps.md` → not updated (no UI changes)

---

## 5. Previous Session Summary (2026-03-21 Session 28)

### Completed — Critical Migration Bug Fix (4 Missing Designer.cs Files)

User reported: "I can't see the Offer Letter and Loan Pack buttons on the Detail page even for an Approved application."

#### Root Cause

Four hand-crafted EF Core migrations were missing their `.Designer.cs` files. Without these files, EF Core does not recognize the migrations, so they were **never applied to the database**. This caused `Unknown column` errors on every query touching the affected tables:

| Migration | Missing Column | Impact |
|-----------|---------------|--------|
| `20260316120000_AddIndustrySectorToLoanApplication` | `IndustrySector` on `LoanApplications` | **ALL loan application queries failed** — `GetApplicationDetailAsync` returned null → Detail page fell back to mock data (status `"HOReview"`) → buttons hidden |
| `20260318100000_RenameNonPerformingToDelinquentFacilities` | `DelinquentFacilities` on `BureauReports` | Bureau report queries failed |
| `20260320100000_AddFineractProductIdToLoanProduct` | `FineractProductId` on `LoanProducts` | Product listing queries failed (seeder crash) |
| `20260320110000_AddOfferLettersTable` | Entire `OfferLetters` table | Offer letter generation would have failed |

The `IndustrySector` migration was the critical one: since every `LoanApplication` query includes this column, the Detail page always caught the exception in its try/catch and returned `null`, causing the mock fallback with `"HOReview"` status — which doesn't show Loan Pack or Offer Letter buttons.

#### Fix Applied

1. Created 4 missing Designer.cs files (matching the empty `BuildTargetModel` pattern used by all other migrations in this project)
2. Updated `CRMSDbContextModelSnapshot.cs` to include `FineractProductId` on `LoanProduct` and full `OfferLetter` entity
3. Made `RenameNonPerformingToDelinquentFacilities` migration safe (conditional rename via `INFORMATION_SCHEMA` check)
4. Made `AddOfferLettersTable` migration safe (`DROP TABLE IF EXISTS` guard for orphaned table from prior failed attempt)
5. Removed invalid `IsDeleted` column from OfferLetters migration (not part of domain model)

#### Verification

- All 4 migrations applied successfully on startup
- Zero `Unknown column` errors in logs
- Application starts and runs cleanly on `http://localhost:5292`
- Build: 0 errors. Tests: Domain + Application pass (2/2)

#### Files Created

| File | Purpose |
|------|---------|
| `20260316120000_AddIndustrySectorToLoanApplication.Designer.cs` | Designer file for IndustrySector migration |
| `20260318100000_RenameNonPerformingToDelinquentFacilities.Designer.cs` | Designer file for DelinquentFacilities rename migration |
| `20260320100000_AddFineractProductIdToLoanProduct.Designer.cs` | Designer file for FineractProductId migration |
| `20260320110000_AddOfferLettersTable.Designer.cs` | Designer file for OfferLetters table migration |

#### Files Modified

| File | Change |
|------|--------|
| `CRMSDbContextModelSnapshot.cs` | Added `FineractProductId` on LoanProduct + full `OfferLetter` entity definition |
| `20260320110000_AddOfferLettersTable.cs` | Added `DROP TABLE IF EXISTS` guard; removed invalid `IsDeleted` column |
| `20260318100000_RenameNonPerformingToDelinquentFacilities.cs` | Conditional rename via `INFORMATION_SCHEMA` check |

#### Key Lesson

When hand-crafting EF Core migrations (instead of using `dotnet ef migrations add`), always create the companion `.Designer.cs` file with the `[Migration("...")]` attribute. Without it, EF Core silently ignores the migration.

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v4.7
- [x] `docs/ImplementationTracker.md` → v5.1

---

## 5. Previous Session Summary (2026-03-18 Session 25)

### Completed — Hybrid AI Advisory Architecture (Rule-Based Scoring + Optional LLM Narratives)

Implemented a hybrid AI advisory system that combines deterministic rule-based scoring with optional LLM-generated narrative text. The key principle: **LLM enhances presentation but never changes scores or recommendations**.

#### Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│            HybridAIAdvisoryService                          │
│            (implements IAIAdvisoryService)                  │
├─────────────────────────────────────────────────────────────┤
│  STEP 1: RuleBasedScoringEngine (deterministic)            │
│  → Calculates 5 risk category scores                       │
│  → Determines recommendation (Approve/Decline/Refer)       │
│  → Identifies red flags                                     │
│  → OUTPUT: Auditable, deterministic results                │
├─────────────────────────────────────────────────────────────┤
│  STEP 2: LLMNarrativeGenerator (optional)                  │
│  → Builds structured prompt with all data + scores         │
│  → Calls OpenAI GPT-4o-mini for narrative text             │
│  → OUTPUT: Executive summary, strengths/weaknesses text    │
├─────────────────────────────────────────────────────────────┤
│  STEP 3: Merge & Fallback                                  │
│  → Combines rule-based scores with LLM narratives          │
│  → Falls back to template text if LLM fails                │
└─────────────────────────────────────────────────────────────┘
```

#### Key Design Decisions

| Component | Responsibility | Auditable? |
|-----------|---------------|------------|
| **RuleBasedScoringEngine** | All scores, recommendations, red flags | Yes (deterministic) |
| **LLMNarrativeGenerator** | Executive summary, strengths/weaknesses text | Yes (logged) |
| **HybridAIAdvisoryService** | Orchestrates both, merges results, handles fallback | Yes |

#### Files Created

| File | Purpose |
|------|---------|
| `RuleBasedScoringEngine.cs` | Extracted scoring logic from MockAIAdvisoryService — calculates all 5 risk categories with configurable thresholds |
| `LLMNarrativeGenerator.cs` | Builds prompts and calls LLM for enhanced narrative text; includes detailed system prompt for Nigerian banking context |
| `HybridAIAdvisoryService.cs` | Main service combining rule-based + LLM with graceful fallback |
| `AIAdvisorySettings.cs` | Configuration class (UseLLMNarrative toggle, timeout, fallback settings) |

#### Files Modified

| File | Change |
|------|--------|
| `DependencyInjection.cs` | Config-based toggle: registers LLMNarrativeGenerator only when `UseLLMNarrative=true` |
| `appsettings.json` (API) | Added `AIAdvisory` section + `OpenAI.ApiKey` placeholder |
| `appsettings.json` (Web.Intranet) | Added `AIAdvisory` section + `OpenAI.ApiKey` placeholder |

#### Configuration

```json
// appsettings.json
{
  "OpenAI": {
    "ApiKey": "sk-your-key-here",
    "Model": "gpt-4o-mini",
    "Temperature": 0.3
  },
  "AIAdvisory": {
    "UseLLMNarrative": false,      // Toggle LLM on/off
    "LLMTimeoutSeconds": 30,
    "FallbackToTemplateOnFailure": true
  }
}
```

#### How to Enable LLM Narratives

1. Set OpenAI API key in `appsettings.json`
2. Set `"UseLLMNarrative": true`
3. Restart application

#### Cost & Performance

| Metric | Rule-Based Only | Hybrid (LLM enabled) |
|--------|-----------------|---------------------|
| Latency | ~50ms | ~3-5 seconds |
| Cost per Advisory | $0 | ~$0.005-0.02 |
| Availability | 100% | 99.9% (with fallback) |

**Build:** 0 errors, 25 warnings (all pre-existing). **Tests:** Domain + Application pass.

### Files Created This Session
- `src/CRMS.Infrastructure/ExternalServices/AIServices/RuleBasedScoringEngine.cs`
- `src/CRMS.Infrastructure/ExternalServices/AIServices/LLMNarrativeGenerator.cs`
- `src/CRMS.Infrastructure/ExternalServices/AIServices/HybridAIAdvisoryService.cs`
- `src/CRMS.Infrastructure/ExternalServices/AIServices/AIAdvisorySettings.cs`

### Files Modified This Session
- `src/CRMS.Infrastructure/DependencyInjection.cs` — Hybrid service registration with config toggle
- `src/CRMS.API/appsettings.json` — Added `AIAdvisory` + `OpenAI` sections
- `src/CRMS.Web.Intranet/appsettings.json` — Added `AIAdvisory` + `OpenAI` sections

### Docs Updated This Session
- [x] `docs/SESSION_HANDOFF.md` → updated (this file)
- [x] `docs/UIGaps.md` → v4.4
- [x] `docs/ImplementationTracker.md` → v4.8

---

## 5. Previous Session Summary (2026-03-18 Session 24)

### Completed — P3 UI Gaps: Template CRUD + Bureau Report Detail Modal

Addressed the remaining P3 UI gaps from the gap analysis. 2 of 3 items implemented; the third (guarantor credit check trigger) is N/A since credit checks are already auto-triggered after branch approval.

See Session 24 archive below for full details.

---

## 5. Previous Session Summary (2026-03-18 Session 23)

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

### Option A — Collateral Perfection: Multi-Actor Sign-Off (Legal + Credit)

The `LegalReview`/`LegalApproval` workflow stages are now fully wired. The next natural follow-on is completing the collateral perfection workflow, which uses the same actors (`LegalOfficer`, `HeadOfLegal`) on the collateral side. Currently a single "Approve" button with no role separation. The design calls for:
1. **Legal clearance** — `IsLegalCleared` already exists on the domain. Need: `RecordLegalClearanceCommand` handler, CollateralTab legal clearance button for `LegalOfficer`/`HeadOfLegal`
2. **Credit adequacy sign-off** — Credit Officer confirms value + adequacy. `ApproveCollateral` already exists; needs role-gating to `CreditOfficer`/`CreditManager` only

**Key files:**
```
src/CRMS.Domain/Aggregates/Collateral/Collateral.cs             ← RecordLegalClearance() already exists
src/CRMS.Application/Collateral/Commands/CollateralCommands.cs  ← add RecordLegalClearanceCommand handler
src/CRMS.Infrastructure/DependencyInjection.cs                  ← register new handler
src/CRMS.Web.Intranet/Components/Pages/Applications/Tabs/CollateralTab.razor  ← add Legal Clearance button
src/CRMS.Web.Intranet/Components/Pages/Applications/Detail.razor               ← wire modal
```

---

### Option B — Retail Loan System (Phase 2)

The corporate loan system is now fully feature-complete. Phase 2 introduces the customer-facing **Retail Loan** portal with automated decisioning.

**Key files to start with:**
```
src/CRMS.Domain/Aggregates/                        ← add RetailLoanApplication aggregate
src/CRMS.Application/RetailLoan/                   ← new use cases (Submit, Assess, Decide)
src/CRMS.Infrastructure/Persistence/               ← new EF configs + migrations
src/CRMS.Web.Portal/                               ← customer self-service Blazor pages (new project)
```

**Modules to build:**
- `CustomerPortal` — self-service application intake (Blazor Web App, public-facing)
- `DecisionEngine` — automated Approve/Decline/Refer based on bureau + scoring

---

### Option C — Guarantor Approval: Guarantee Deed Legal Sign-Off (Deferred Design Decision)

Discussed in Session 52. Credit Officer owns guarantor approval/rejection (bureau check, net worth vs loan exposure). This is already wired and operationally correct. Optional deferred addition: a **legal team sign-off** on guarantee deed enforceability (similar to `IsLegalCleared` on collateral). To implement when ready:
1. Add `IsLegalCleared` + `LegalClearedAt` to `Guarantor` aggregate
2. Add `RecordLegalClearance()` domain method + `RecordGuarantorLegalClearanceCommand` handler
3. `GuarantorsTab.razor`: `CanClearLegal` param, legal clearance button, badge (mirrors `CollateralTab` pattern exactly)
4. `ApprovalGate` may need updating to require legal clearance before guarantor is considered `Approved`

---

### Other Pending Tasks (lower priority)

**Option D — PartiesTab Bureau Report View Button**
Director/signatory view buttons have no `@onclick`. Wire to `ShowBureauReportModal`. `ViewBureauReportModal` already exists.

**Option E — Disbursement Checklist Template Admin UI**
`AddChecklistTemplateItemHandler`, `UpdateChecklistTemplateItemHandler`, `RemoveChecklistTemplateItemHandler` already registered in DI. Need `Products.razor` sub-section + `ApplicationService` methods.

**Option F — Internal Statement Re-fetch Window (Deferred)**
`RefetchInternalBankStatementCommand` currently uses `DateTime.UtcNow` as `toDate` (6 months back from today). When CBS supports backdated transactions in test env, change to `loanApp.CreatedAt` — one-line change in `RefetchInternalBankStatementCommand.cs`.

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
