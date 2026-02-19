# Audit Report: CorporateLoanInitiation Module

**Module ID:** 4
**Audit Date:** 2026-02-17
**Auditor:** Domain Expert Review
**Module Status (Documented):** 🟢 Completed
**Audit Verdict:** 🔴 Critical Bugs Present — Requires Fixes Before Testing

---

## 1. Executive Summary

The CorporateLoanInitiation module is the core of Phase 1 and the most critical module in the system. The aggregate design is generally sound, with good use of domain events and status history tracking. However, there are several serious bugs: a potential race condition in credit check tracking, unsafe application number generation, missing audit logging for key transitions, a workflow bypass path, and incomplete implementation of HO Review and committee-linked commands. Several items documented as pending in the module doc are marked complete in the Implementation Tracker.

---

## 2. Critical Bugs

### 2.1 Race Condition in Credit Check Completion Tracking (HIGH)

The `RecordCreditCheckCompleted()` method increments `CreditChecksCompleted++` without any synchronization. Since credit checks for all directors and signatories run in parallel (via background service), multiple concurrent writes to the same `LoanApplication` entity can occur simultaneously.

**Scenario:**
- 3 directors, each checked in parallel
- Thread A reads `CreditChecksCompleted = 0`, increments to 1
- Thread B reads `CreditChecksCompleted = 0` simultaneously, also increments to 1
- Result: Only 1 credit check is recorded instead of 3

**Impact:** The loan may never progress from `CreditAnalysis` to `HOReview`, or `AllCreditChecksCompleted` fires prematurely.

**Recommendation:**
- Use optimistic concurrency via EF Core's `RowVersion`/`Timestamp` concurrency token on `LoanApplication`
- Alternatively, use a dedicated counter table with a database-level atomic increment
- Or use a message queue to serialize credit check completion events per loan application

### 2.2 Application Number Collision Risk (MEDIUM)

```csharp
private static string GenerateApplicationNumber()
{
    return $"LA{DateTime.UtcNow:yyyyMMdd}{Guid.NewGuid().ToString()[..6].ToUpper()}";
}
```

Taking only the first 6 characters of a GUID gives 36^6 ≈ 2.1 billion combinations, but the alphanumeric space from a GUID substring is not uniformly distributed (only hex chars 0-9, a-f). With high concurrent application volumes, probability of collision is non-zero.

**Recommendation:**
- Use a database sequence (auto-increment) for the numeric suffix
- Enforce a unique index on `ApplicationNumber` in the database
- Handle `DbUpdateException` for duplicate key violation and return a meaningful error

### 2.3 Workflow Bypass: `SubmitForBranchReview()` Accepts Draft Status (MEDIUM)

```csharp
public Result SubmitForBranchReview(Guid userId)
{
    if (Status != LoanApplicationStatus.DataGathering && Status != LoanApplicationStatus.Draft)
        return Result.Failure("...");
```

This allows a Draft application to go directly to BranchReview, skipping the `Submitted → DataGathering` stages entirely. This bypasses document validation (which is checked in `Submit()`).

**Impact:** A loan application with no documents can reach Branch Review without any document check.

**Recommendation:** Remove `LoanApplicationStatus.Draft` from the `SubmitForBranchReview()` precondition. The correct flow is `Draft → Submit() → DataGathering → SubmitForBranchReview()`.

### 2.4 `UpdateLoanDetails()` Has No Domain Event (LOW-MEDIUM)

Changes to loan amount, tenor, and interest rate are critical business data but `UpdateLoanDetails()` fires no domain event. These changes are not audited via the domain event pipeline.

**Recommendation:** Add a `LoanApplicationDetailsUpdatedEvent` to capture old and new values for audit purposes.

---

## 3. Missing Implementation (Documented as Pending but Tracker says Complete)

### 3.1 HO Review and Committee Approval Commands

The module document's `Pending Enhancements` lists:
- "Add HO review and committee approval commands"

However, the `LoanApplication` aggregate already has `MoveToHOReview()`, `MoveToCommittee()`, `ApproveCommittee()`, and `FinalApprove()` methods. The gap is at the **Application layer** — there are no corresponding `Commands`, `Handlers`, or **API endpoints** for these operations.

**Impact:** The full corporate loan lifecycle cannot be executed end-to-end via the API. An HO Reviewer has no API endpoint to review or approve.

### 3.2 Offer Generation and Acceptance Commands

`LoanApplicationStatus` includes `OfferGenerated` and `OfferAccepted` states, but there are no commands, handlers, or API endpoints for these transitions.

### 3.3 File Storage Integration Missing

`AddDocument()` stores a `filePath` string, but the document is not actually saved to S3/Azure Blob Storage. Documents are accepted by the API but not persisted beyond the database path reference.

**Impact:** Document downloads will fail. Files uploaded will be lost.

---

## 4. Business Logic Gaps

### 4.1 No Interest Rate Validation

`InterestRatePerAnnum` is not validated against any bounds. A loan can be created with 0% interest or 1000% interest. The system should enforce:
- Rate > 0%
- Rate within product-defined range (linkage to `LoanProduct.PricingTiers`)

### 4.2 No Validation Against Product Min/Max Amount

`RequestedAmount` is not validated against the `LoanProduct.MinAmount` and `LoanProduct.MaxAmount`. A loan officer can request ₦1 for a product with a minimum of ₦50M.

**Recommendation:** In `InitiateCorporateLoanHandler`, load the product and validate the requested amount against product constraints before creating the application.

### 4.3 BranchId Is Optional but Required for Branch-Level Data Scoping

`BranchId` is nullable in the `LoanApplication` aggregate. The IntranetUI requirements show `BranchApprover` should only see applications from their branch. If `BranchId` is null, branch-level filtering cannot be applied, and all branch approvers would see all applications.

**Recommendation:** For corporate loan type, make `BranchId` mandatory at the domain level.

### 4.4 No "Closed" State Transition After Disbursement

After disbursement, a loan application stays in `Disbursed` status indefinitely. The `Closed` status exists in the enum but there is no domain method or command to transition to it. A closed loan signals end-of-lifecycle and is important for reporting accuracy.

---

## 5. NDPA Compliance Gaps

### 5.1 Director and Signatory BVN Stored in Plain Text

`LoanApplicationParty.BVN` is stored as a plain string. BVN is classified as sensitive data under NDPA. The design document requires sensitive field encryption.

**Recommendation:** Encrypt BVN before persistence and decrypt on read. This should use the application-level encryption service documented in the FullDesign.

### 5.2 Consent Not Captured for Corporate Applicants

The system automatically pulls director and signatory data from core banking and submits them for credit bureau checks. There is no consent capture mechanism for directors/signatories who did not personally agree to the credit check. This is a NDPA requirement.

**Recommendation:** For corporate loans, capture a consent record that covers the authorization letter/board resolution authorizing credit checks on directors and signatories.

---

## 6. Recommendations Summary

| Priority | Item |
|----------|------|
| CRITICAL | Fix race condition in `RecordCreditCheckCompleted()` using concurrency control |
| CRITICAL | Implement missing HO Review, Committee Approval, and Final Approval API endpoints |
| HIGH | Encrypt BVN for all `LoanApplicationParty` records |
| HIGH | Remove Draft bypass in `SubmitForBranchReview()` |
| HIGH | Implement file storage integration for document uploads |
| HIGH | Add interest rate and amount validation against product constraints |
| MEDIUM | Add unique DB index on `ApplicationNumber` with proper error handling |
| MEDIUM | Make `BranchId` mandatory for corporate loans |
| MEDIUM | Implement `Closed` status transition |
| MEDIUM | Add `LoanApplicationDetailsUpdatedEvent` for audit trail |
| LOW | Implement consent capture for director/signatory credit checks |
