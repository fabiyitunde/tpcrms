# CRMS Detail Page – Tab & Modal Review Report

**Date:** 2026-03-21  
**Scope:** 12 tabs, 16+ modals in `Detail.razor` and child components  
**Methodology:** Static code review of Blazor component markup, parameters, event callbacks, data binding, validation, error handling, loading states, status-gating, and domain correctness.

---

## 1. CRITICAL BUGS (Functionality Broken)

### C1. `ExecuteAction` silently fails — no error display to user
**File:** `Detail.razor` → `ExecuteAction()`  
**Issue:** When `ApproveApplicationAsync`, `ReturnApplicationAsync`, or `RejectApplicationAsync` returns `result.Success == false`, the modal stays open but **no error message is shown to the user**. The `result.Error` is never displayed.  
**Impact:** Users click Approve/Reject/Return, the spinner disappears, but they receive no feedback on failure — they have no idea the action failed.  
**Fix:** Add an `actionError` field and display it in the action modal body.

### C2. `RequestBureauCheck` is a no-op — does nothing but reload
**File:** `Detail.razor` → `RequestBureauCheck(Guid partyId)`  
**Issue:** The method body is `await LoadApplication();` with a comment "Bureau check requires consent record — simplified for now." The director/signatory "Check" button in `PartiesTab` does nothing meaningful — no API call is made, no bureau check is triggered.  
**Impact:** Bureau checks for directors/signatories cannot actually be initiated from the UI. Core lending workflow feature is non-functional.

### C3. `VerifyStatement` and `AnalyzeStatement` — no error feedback
**File:** `Detail.razor`  
**Issue:** `VerifyStatement` sets `isProcessingStatement` but if `result.Success` is false, nothing happens — no error is shown. Similarly `AnalyzeStatement` silently swallows failures.  
**Impact:** Lending officers get no indication when statement verification/analysis fails.

### C4. `VerifyDocument` — no error feedback  
**File:** `Detail.razor` → `VerifyDocument()`  
**Issue:** If verification fails, the result error is silently discarded. No user notification.

### C5. Financial Statement edit mode divides by 1000 — data corruption risk
**File:** `FinancialStatementModal.razor` → `LoadStatementForEdit()`  
**Issue:** When loading for edit, all values are divided by 1000 (`bsData.CashAndCashEquivalents / 1000`). But when **creating** a new statement, values are NOT multiplied by 1000 before saving — the `BalanceSheetInput` object is passed directly. This means if the API stores raw values, then editing an existing statement and re-saving will divide by 1000 again, causing data shrinkage with each save.  
**Impact:** Financial data may be corrupted progressively on each edit cycle (values get smaller by factor of 1000 each time), unless the backend handles this consistently. At minimum, this is fragile and relies on undocumented backend behavior.

---

## 2. HIGH PRIORITY ISSUES (Incomplete Features)

### H1. `PartiesTab` — "View" button for directors with bureau reports does nothing
**File:** `PartiesTab.razor`  
**Issue:** When `director.HasBureauReport` is true, a visibility icon button is rendered, but it has **no `@onclick` handler**. Same issue for signatories. The user sees a view button but clicking it does nothing.  
**Impact:** Cannot view bureau report details for directors/signatories from the Parties tab.

### H2. `CommitteeTab` — Voting lacks loading state / error handling
**File:** `CommitteeTab.razor` → `CastVote()`  
**Issue:** The "Submit Vote" button has no spinner/loading state during the async call. If the call fails, the user gets no error message. There is no `isVoting` guard, allowing double-clicks.  
**Impact:** Users can accidentally submit duplicate votes. No feedback on failure.

### H3. `CommitteeTab` — No quorum/majority tracking or auto-decision
**File:** `CommitteeTab.razor` & `Detail.razor`  
**Issue:** The committee tab shows individual votes but has no visible quorum tracker (e.g., "2 of 3 votes cast, 2 needed for quorum"). The `Decision`, `DecisionDate`, `DecisionComments` fields exist in the DTO but there's no mechanism shown in the UI for the chairperson to finalize the committee decision when all votes are in.  
**Impact:** No clear way to close out a committee review — the voting flow appears incomplete.

### H4. `AdvisoryTab` — No loading state for advisory generation
**File:** `AdvisoryTab.razor` → "Generate Advisory" button  
**Issue:** Button calls `OnGenerateAdvisory.InvokeAsync()` but has no `disabled` state, no spinner, and no error display. `Detail.razor`'s `GenerateAdvisory()` also lacks error handling.  
**Impact:** User can click the button multiple times; no feedback on success/failure.

### H5. `StatementsTab` — Verify/Reject/Analyze buttons not gated by role
**File:** `StatementsTab.razor`  
**Issue:** Verify/Reject buttons are shown for any user when `VerificationStatus == "Pending"`. There's no role-based check (e.g., only Credit Analysts or reviewers should verify statements). Only `IsEditable` gates the upload button.  
**Impact:** Any user viewing the application can potentially verify or reject bank statements.

### H6. `DocumentsTab` — Verify/Reject buttons not gated by role or status
**File:** `DocumentsTab.razor`  
**Issue:** Verify and Reject buttons appear for documents in "Pending" or "Uploaded" status regardless of the current user's role or the application status. A loan officer viewing their own draft should not verify their own documents.  
**Impact:** Segregation of duties violation — the person uploading can verify their own documents.

### H7. `CollateralTab` — Shows `MarketValue` column header as "Acceptable Value" but displays `MarketValue`
**File:** `CollateralTab.razor`  
**Issue:** Table header says "Acceptable Value" but the column data shows `collateral.MarketValue`. These are different figures — acceptable value = market value × (1 − haircut%). The footer also sums `MarketValue` but labels it "Total Acceptable Collateral Value".  
**Impact:** Misleading collateral coverage figures for lending officers making credit decisions.

### H8. `SetupCommitteeModal` — `Close()` doesn't await the callback
**File:** `SetupCommitteeModal.razor`  
**Issue:** `private void Close() => OnClose.InvokeAsync();` — the EventCallback is invoked but not awaited (synchronous `void` method calling async method). This could cause race conditions.  
**Fix:** Change to `private async Task Close() => await OnClose.InvokeAsync();`

---

## 3. MEDIUM PRIORITY ISSUES (Missing Validation, UX Issues)

### M1. `AddCollateralModal` — Optional valuation fires separate API call without error handling
**File:** `AddCollateralModal.razor` → `Submit()`  
**Issue:** After adding collateral, if `MarketValue > 0`, a second call `SetCollateralValuationAsync` is made. If this second call fails, the collateral is already created but the valuation is lost — and the user only sees success because `OnSuccess` was already invoked.  
**Fix:** Either make valuation atomic with creation, or show an error if valuation save fails.

### M2. `AddGuarantorModal` — Minimal validation
**File:** `AddGuarantorModal.razor`  
**Issue:** Only validates `FullName` is not empty. A guarantor in corporate lending should require: BVN (for Nigerian regulation), guarantee type, and relationship at minimum. BVN format validation (exactly 11 digits) is also missing.  
**Impact:** Incomplete guarantor records can be submitted.

### M3. `UploadExternalStatementModal` — Missing file upload functionality
**File:** `UploadExternalStatementModal.razor`  
**Issue:** Despite being named "Upload External Bank Statement", the modal only collects metadata (bank name, account number, period, balances). There is no file upload input — the actual statement PDF is not uploaded. It's just a data entry form.  
**Impact:** No supporting documentation for external bank statements — reduces auditability.

### M4. `FillPartyInfoModal` — `IsValid` logic is counterintuitive
**File:** `FillPartyInfoModal.razor`  
**Issue:** `IsValid` returns `false` when `!showBvn && !showShareholding` — meaning if a party already has both BVN and shareholding filled, the Save button is disabled. This is correct in terms of "nothing to fill", but the modal shouldn't open at all in that case. The check in `PartiesTab` (`string.IsNullOrEmpty(director.RawBVN) || director.ShareholdingPercentage == null`) only shows the button when data is missing, but the `PartyType` check in the modal may not match expectations.  
**Minor Issue:** `showShareholding` checks `Party?.PartyType == "Director"` but the `PartyInfo` DTO may not have `PartyType` populated for all sources.

### M5. `CommentsTab` — No loading state on comment submission
**File:** `CommentsTab.razor`  
**Issue:** "Add Comment" button has no loading spinner during the async `OnAddComment.InvokeAsync(newComment)` call. No error handling if the call fails.

### M6. `BureauTab` — Individual report cards lack "View Details" button
**File:** `BureauTab.razor`  
**Issue:** Business reports have a "View Details" button (`OnViewReport.InvokeAsync(report.Id)`) in the card footer. But individual reports (Directors, Signatories, Guarantors) do NOT have a View Details button — no `OnViewReport` call is rendered in their card footers.  
**Impact:** Users cannot view detailed bureau reports for individual directors/signatories/guarantors.

### M7. `CollateralTab` — No "Reject Collateral" action
**File:** `CollateralTab.razor` & `Detail.razor`  
**Issue:** There's an Approve button for collateral (when status is "Valued"), but there's no Reject button. A reviewer who wants to reject collateral has no mechanism to do so.  
**Impact:** Collateral workflow is one-directional (can only approve, not reject).

### M8. `StatementsTab` — No delete capability for external statements
**File:** `StatementsTab.razor`  
**Issue:** Once an external statement is uploaded (via the metadata-only modal), there's no way to delete or remove it. No delete button is shown.  
**Impact:** Erroneous statement entries cannot be removed.

### M9. `FinancialsTab` — "Delete All" only works when ALL statements are in Draft
**File:** `FinancialsTab.razor`  
**Issue:** The "Delete All" button is only shown when `Statements.All(s => s.Status == "Draft")`. If even one statement is not Draft, the button disappears, even if most are Draft. Individual delete is also gated to `stmt.Status == "Draft"`.  
**Minor:** This may be intentional but could confuse users who expected a mixed-state delete.

### M10. `FinancialStatementModal` — Year dropdown only goes back 5 years
**File:** `FinancialStatementModal.razor`  
**Issue:** Year selection ranges from `DateTime.Now.Year` to `DateTime.Now.Year - 5`. For projected statements, users need future years. For established businesses, older years may be needed.  
**Impact:** Cannot enter projected financial statements for future years, and cannot enter historical data beyond 5 years ago.

### M11. Submit for Review — `submitComments` are collected but never sent
**File:** `Detail.razor` → `SubmitForReview()`  
**Issue:** The modal collects `submitComments` but `SubmitApplicationAsync(Id, userId)` only passes `Id` and `userId` — the comments are never sent to the backend.  
**Impact:** Review notes from the loan officer are silently discarded.

---

## 4. LOW PRIORITY ISSUES (Polish)

### L1. `OverviewTab` — Summary counts don't include all relevant entities
**File:** `OverviewTab.razor`  
**Issue:** Shows Directors, Documents, Collaterals, and Bureau Reports counts. Does not show Guarantors count, Financial Statements count, or Bank Statements count.  
**Impact:** Minor completeness gap in overview dashboard.

### L2. `WorkflowTab` — Status formatting duplicated
**File:** `WorkflowTab.razor` and `Detail.razor`  
**Issue:** `FormatStatus()` is defined in both files with slightly different mappings. Should be centralized.

### L3. `CollateralTab` — Status dot CSS classes differ from other tabs
**File:** `CollateralTab.razor`  
**Issue:** Uses `status-dot-success`, `status-dot-warning` etc. while other tabs use just `success`, `warning`. May cause styling inconsistency.

### L4. `EditGuarantorModal` — Default guarantee type differs from Add modal
**File:** `AddGuarantorModal.razor` defaults to `"Limited"`, while `EditGuarantorModal.razor` defaults to `"Unlimited"`. These should be consistent.

### L5. `AddGuarantorModal` — Relationship dropdown options differ between Add and Edit modals
**File:** Add modal has "Family Member", "Business Partner", "Parent Company". Edit modal has "Parent", "Sibling", "BusinessPartner" (without space). This creates data inconsistency.

### L6. `EditCollateralModal` — Ownership type options differ from Add modal
**File:** Add modal has "Leased", Edit modal has "ThirdParty". Options should be consistent.

### L7. `FinancialsTab` — Trend analysis balance sheet shows "Total Debt" but DTO has no direct `TotalDebt` field
**File:** `FinancialsTab.razor`  
**Issue:** Displays `stmt.TotalDebt` but the `FinancialStatementInfo` model is from the parent. This works if the DTO has the field, but the DTO structure in `ApplicationServiceDtos.cs` doesn't show a `TotalDebt` on `FinancialStatementInfo`. If this comes from the `LoanApplicationDetail.FinancialStatements` model, it should be verified to exist.

### L8. Multiple modals share `isDeleting` state
**File:** `Detail.razor`  
**Issue:** A single `isDeleting` boolean is shared between collateral delete, guarantor delete, financial statement delete, and delete-all. If two delete modals could theoretically be open simultaneously, they'd conflict. In practice only one modal is open at a time so this is low risk.

### L9. `SetupCommitteeModal` — Modal markup structure differs from other modals
**File:** `SetupCommitteeModal.razor`  
**Issue:** Uses `<div class="modal-backdrop" @onclick="Close"></div><div class="modal">` as separate siblings, while all other modals use `<div class="modal-backdrop show"><div class="modal" @onclick:stopPropagation>`. This means click-outside-to-close may not work as expected, and the "show" class for animation is missing.

---

## 5. SPECIAL FOCUS AREA FINDINGS

### CollateralTab: Valuation/Approve buttons based on status
**Status: MOSTLY CORRECT with one gap.**  
- ✅ "Set Valuation" button shows when status is `"Proposed"` or `"UnderValuation"` and `CanManageValuation` is true.
- ✅ "Approve Collateral" button shows when status is `"Valued"` and `CanManageValuation` is true.
- ✅ Edit/Delete gated to `IsEditable` AND correct status checks.
- ❌ **No "Reject" action for collateral** (see M7).
- ❌ **MarketValue vs AcceptableValue confusion** in display (see H7).

### GuarantorsTab: Approve/Reject gating
**Status: CORRECT but edge case gap.**
- ✅ Approve shows when status is `"CreditCheckCompleted"` and `CanManageGuarantors` is true.
- ✅ Reject shows when status is `"Proposed"`, `"PendingVerification"`, or `"CreditCheckCompleted"` and `CanManageGuarantors` is true.
- ✅ `CanManageGuarantors` correctly excludes Draft, Approved, CommitteeApproved, Rejected, Disbursed.
- ⚠️ The `GuarantorInfo` in mock data has `Status = "Active"` which doesn't appear in any gating logic — might represent already-approved guarantors but is a different string from "Approved". Verify domain model alignment.

### CommitteeTab: Voting flow
**Status: INCOMPLETE.**
- ✅ Vote casting UI is present (Approve/Reject/Abstain dropdown).
- ✅ Correctly checks if current user is a committee member.
- ✅ Shows "Already voted" state.
- ✅ Shows "Voting is closed" when status is Completed.
- ❌ **No loading state on vote submission** (see H2).
- ❌ **No quorum tracker** (see H3).
- ❌ **No mechanism for chairperson to finalize decision.**
- ❌ **No display of vote conditions required (e.g., "2 approvals needed for quorum").**

### FinancialsTab: Business age validation
**Status: CORRECT.**
- ✅ Both `FinancialsTab.razor` and `Detail.razor` have consistent validation logic.
- ✅ Correctly handles startup (0 years), 1 year, 2 years, and 3+ years scenarios.
- ✅ Displays clear requirements description based on business age.
- ⚠️ Minor: Year dropdown in modal doesn't include future years for projected statements (M10).

### StatementsTab: Verify/Reject/Analyze flow
**Status: FUNCTIONAL but missing role gating.**
- ✅ Verify/Reject buttons correctly gate to `VerificationStatus == "Pending"`.
- ✅ Analyze button gates to `AnalysisStatus == "Pending" && VerificationStatus != "Rejected"`.
- ✅ Internal statements can be analyzed directly.
- ❌ **No role-based access control on Verify/Reject** (see H5).
- ❌ **Silent error handling on verify/analyze** (see C3).

---

## SUMMARY TABLE

| Priority | Count | Examples |
|----------|-------|---------|
| **CRITICAL** | 5 | Silent action failures, bureau check no-op, data corruption risk |
| **HIGH** | 8 | Dead view buttons, no vote loading state, missing role gating |
| **MEDIUM** | 11 | Validation gaps, missing features, UX issues |
| **LOW** | 9 | Inconsistent options, duplicated code, naming mismatches |

**Top 3 recommendations for immediate action:**
1. **Fix silent error handling** across all action methods in `Detail.razor` (C1, C3, C4) — display errors to users.
2. **Implement the bureau check flow** or disable the button with a clear "coming soon" message (C2).
3. **Audit the financial statement ÷1000 logic** to confirm backend expectations and prevent data corruption (C5).
