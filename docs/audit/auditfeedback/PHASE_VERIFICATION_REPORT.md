# Audit Feedback: Phase A–E Fix Verification Report

**Verification Date:** 2026-02-18
**Verifier:** Domain Expert Review
**Source Claims:** `docs/audit/AUDIT_SUMMARY.md` (updated by dev team)
**Method:** Direct source code inspection — each claimed fix verified against actual implementation

---

## Verification Summary

| Phase | Claimed Status | Verified Status | Notes |
|-------|---------------|-----------------|-------|
| A — Code Bug Fixes | ✅ COMPLETE | ✅ CONFIRMED | All 5 items verified |
| B — Domain Logic Gaps | ✅ COMPLETE | ⚠️ PARTIAL | C-02 partially fixed |
| C — Infrastructure | ✅ COMPLETE | ⚠️ PARTIAL | C-08 still incomplete |
| D — Security Hardening | ✅ COMPLETE | ⚠️ PARTIAL | H-19, H-20 incomplete |
| E — Performance & Polish | ✅ COMPLETE | ✅ CONFIRMED | All verified |

**New issues found during verification:** 2 (see Section 5)

---

## Section 1: Phase A — Code Bug Fixes ✅ FULLY VERIFIED

All five items in Phase A are confirmed fixed by direct code inspection.

### C-04: `Reject()` stores rejector in `ApprovedByUserId` (Collateral)
**Status: FIXED ✅**

`Collateral.cs` now has dedicated `RejectedByUserId` and `RejectedAt` fields (separate from `ApprovedByUserId`/`ApprovedAt`). The `Reject()` method correctly populates:
```csharp
Status = CollateralStatus.Rejected;
RejectedByUserId = rejectedByUserId;
RejectedAt = DateTime.UtcNow;
RejectionReason = reason;
```
`ApprovedByUserId` is no longer misused on rejection paths.

### C-05: `public new DateTime CreatedAt` hiding base class (Collateral)
**Status: FIXED ✅**

The `new` keyword property shadow is gone. `Collateral.Create()` assigns directly to the inherited base class property:
```csharp
CreatedAt = DateTime.UtcNow
```
`Entity.CreatedAt` is declared with `protected set`, allowing this assignment from the derived class factory method. EF Core will map a single `CreatedAt` correctly.

### C-06: Same bugs in Guarantor
**Status: FIXED ✅**

`Guarantor.cs` mirrors the same fix — dedicated `RejectedByUserId` and `RejectedAt` fields, no `new` property hiding. `Reject()` is semantically correct.

### C-07: DSCR overestimated when CashFlowStatement absent
**Status: FIXED ✅**

`FinancialRatios.Calculate()` now uses a conservative estimate when `cf == null`:
```csharp
// Conservative estimate: assume 5-year amortization of total debt
principalPayments = bs.TotalDebt / 5;
ratios.IsDSCREstimated = true;
```
The `IsDSCREstimated` flag allows downstream consumers (advisory engine, reporting) to treat the value with appropriate caution. This is a reasonable and auditable approach.

> **One remaining note:** The advisory engine and loan pack generator should be verified to surface this flag to the credit analyst. If a DSCR estimated value triggers a "Good" rating without flagging the estimation, the risk remains hidden.

### H-21: Role naming — `ComplianceOfficer` vs `Auditor`
**Status: FIXED ✅**

`Roles.cs` uses `Auditor`. `AuditController` now declares:
```csharp
[Authorize(Roles = "Auditor,RiskManager,SystemAdmin")]
```
Authorization will now correctly match the `Auditor` role defined in the system.

---

## Section 2: Phase B — Domain Logic Gaps ⚠️ PARTIALLY VERIFIED

Two of four items are confirmed fixed. One is partially fixed with a critical gap remaining.

### C-02: No consent verification before bureau credit checks
**Status: PARTIALLY FIXED ⚠️ — Critical gap remains**

The `ConsentRecord` domain entity is fully implemented with `IsValid()`, `Revoke()`, and expiry tracking. The `RequestBureauReportCommand` now **requires** `ConsentRecordId` as a mandatory parameter (not nullable), which is a step forward.

**However — the handler does NOT validate the consent before proceeding:**

```csharp
// RequestBureauReportHandler.Handle()
var reportResult = BureauReport.Create(
    request.Provider,
    SubjectType.Individual,
    request.SubjectName,
    request.BVN,
    request.RequestedByUserId,
    request.ConsentRecordId,   // ← passed in, but NEVER looked up or validated
    request.LoanApplicationId
);
// Immediately calls bureauProvider.SearchByBVNAsync() without consent check
```

The handler stores the `ConsentRecordId` but **never looks up the consent record** to verify:
- The consent record actually exists
- The consent status is `Active`
- The consent has not expired (`IsValid()`)
- The consent type matches `CreditBureauCheck`

A caller can pass any GUID (including a fabricated or revoked consent ID) and the bureau check will proceed. This means the NDPA compliance gap is NOT fully closed — the consent check is cosmetic, not enforced.

**Required fix:**
```csharp
var consent = await _consentRepository.GetByIdAsync(request.ConsentRecordId, ct);
if (consent == null || !consent.IsValid())
    return ApplicationResult<BureauReportDto>.Failure("Valid consent record is required for bureau access");
```

### H-13: Chairperson can decide without quorum
**Status: FIXED ✅**

`CommitteeReview.RecordDecision()` now enforces:
```csharp
if (!HasQuorum)
    return Result.Failure($"Quorum not reached. Required: {RequiredVotes}, Voted: ...");
// ...
if (member != null && member.IsChairperson && !member.HasVoted)
    return Result.Failure("Chairperson must cast their vote before recording a decision");
```
Both the quorum requirement and chairperson vote requirement are now enforced.

### H-14: `CommitteeDecisionRecordedEvent` has no handler
**Status: FIXED ✅**

`CommitteeDecisionWorkflowHandler` in `WorkflowIntegrationHandlers.cs` now handles the event and:
- Updates `LoanApplication` status via `ApproveCommittee()` or `RejectCommittee()`
- Transitions the `WorkflowInstance` to the correct status
- Handles `CommitteeDecision.Deferred` by returning the loan to `HOReview`

> **One observation:** For the `Deferred` case, the loan application entity itself is not updated (only the workflow is transitioned). `loanApplication.RejectCommittee()` or a `DeferCommittee()` method is not called for deferrals. The loan application's internal status field may remain stale. Recommend verifying that the `LoanApplication` aggregate has a matching `Defer()` or `ReturnToReview()` method and that it's called in the deferred branch.

### H-17: `MarkAsRead` does not verify ownership
**Status: FIXED ✅**

`NotificationController.MarkAsRead()` now performs ownership verification:
```csharp
if (notification.RecipientUserId.HasValue && notification.RecipientUserId.Value != currentUserId)
    return Forbid("You can only mark your own notifications as read");
```

> **Minor edge case:** If `RecipientUserId` is `null` (system notifications or bulk notifications), the check is bypassed and any authenticated user can mark it as read. This is acceptable for system notifications but should be documented.

---

## Section 3: Phase C — Infrastructure ⚠️ PARTIALLY VERIFIED

### C-08: File storage not implemented
**Status: PARTIALLY FIXED ⚠️ — Integration incomplete**

The storage infrastructure has been created:
- `IFileStorageService` interface defined
- `LocalFileStorageService` fully implemented (upload, download, delete, exists)
- `S3FileStorageService` implemented as well

**However, the actual integration points remain as TODO:**

In `GenerateLoanPackCommand.cs` (line 151):
```csharp
// TODO: Save PDF bytes to file storage (S3/Azure Blob)
```
The PDF bytes are generated and the metadata (`StoragePath`, `ContentHash`) are saved to the database, but **the bytes are never written to storage**. The `StoragePath` stored in the database points to a file that does not exist.

In `LoanPackController.cs` (lines 105–107):
```csharp
// TODO: Retrieve actual PDF from file storage using pack.StoragePath
// For now, return a placeholder response
return NotFound("PDF file storage not implemented - file would be at: " + pack.StoragePath);
```
The download endpoint is still non-functional. Committee members still cannot retrieve the loan pack.

**The dev team has built the plumbing but not connected the pipes.** The `IFileStorageService` is injected nowhere in `GenerateLoanPackHandler`. This issue remains CRITICAL.

### H-12: No optimistic concurrency on `WorkflowInstance`
**Status: FIXED ✅**

`WorkflowInstance` now has:
```csharp
public byte[] RowVersion { get; private set; } = [];
```
Provided the EF Core configuration maps this as `[Timestamp]` or `.IsRowVersion()`, concurrent transitions will now throw `DbUpdateConcurrencyException` rather than silently overwriting each other.

> **Verification recommended:** Confirm that `WorkflowInstanceConfiguration.cs` maps `RowVersion` with `.IsRowVersion()` or `[Timestamp]` in EF Core Fluent API.

---

## Section 4: Phase D — Security Hardening ⚠️ PARTIALLY VERIFIED

### H-16: No role authorization on LoanPack generate endpoint
**Status: FIXED ✅**

`LoanPackController` now restricts generation:
```csharp
[Authorize(Roles = "CreditOfficer,HOReviewer,RiskManager,SystemAdmin")]
```
Unauthorized users will receive HTTP 403.

### H-19: IP address not captured in audit logs
**Status: PARTIALLY FIXED ⚠️**

The `AuditService.LogAsync()` method signature accepts `ipAddress` as a parameter. The `LogLoginAsync()` method similarly accepts it.

**The gap:** `AuditService` does **not** inject `IHttpContextAccessor`. IP capture is entirely dependent on callers passing the value. In the domain event-driven audit path (the majority of audit entries), event handlers do not have access to the HTTP context. For example, `CommitteeDecisionWorkflowHandler` and `AuditEventHandlers` are infrastructure services with no HTTP context dependency — they cannot pass the originating request IP.

Only API controller-level direct calls (e.g., login) have access to the request IP. The bulk of operational audit logs — workflow transitions, committee votes, collateral approvals — will continue to record `null` for `IpAddress`.

**Proper fix requires either:**
1. Injecting `IHttpContextAccessor` into `AuditService` and reading IP internally, or
2. Propagating the client IP via a scoped `IAuditContext` service populated by middleware

### H-20: BVN masking in `OldValues`/`NewValues` JSON
**Status: NOT FIXED ✗**

`AuditService.LogAsync()` still serializes objects directly:
```csharp
oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
newValues != null ? JsonSerializer.Serialize(newValues) : null,
```
There is no sensitive field masking, no exclusion list, and no JSON transformer applied before serialization. If any caller passes a DTO containing `BVN`, `NIN`, `AccountNumber`, or similar fields in `oldValues`/`newValues`, they will be stored in plain text in the audit log.

**Required fix:** A masking serializer or pre-serialization transformer that replaces known sensitive property names with masked equivalents (e.g., `"BVN": "2234****90"`).

---

## Section 5: Phase E — Performance and Polish ✅ FULLY VERIFIED

### H-18: Retry logic has no backoff delay
**Status: FIXED ✅**

`Notification.MarkAsFailed()` now implements exponential backoff:
```csharp
// Exponential backoff: 1min, 5min, 25min (base 5^retryCount minutes)
var backoffMinutes = Math.Pow(5, RetryCount);
NextRetryAt = DateTime.UtcNow.AddMinutes(backoffMinutes);
```
`ProcessRetriesAsync()` filters by `GetForRetryAsync(DateTime.UtcNow, ...)` ensuring retries are not processed until `NextRetryAt` has passed.

### H-22: No caching for reporting dashboard
**Status: FIXED ✅**

`ReportingService` now uses `IMemoryCache`:
- Dashboard summary: 5-minute TTL
- Portfolio summary: 15-minute TTL
- Cache key per report type

> **Note:** This is in-process memory caching. In a multi-instance deployment, each instance will have its own cache — different users hitting different instances could see metrics up to 5 minutes stale from different points in time. For a single-instance deployment this is acceptable. For scale-out, Redis distributed cache should replace `IMemoryCache`.

### M-01: Seed data for loan products
**Status: FIXED ✅ (with a new issue — see Section 6)**

`SeedData.cs` now includes `SeedLoanProductsAsync()` which creates Corporate Term Loan and Working Capital Finance products. The system no longer starts with an empty product catalog.

---

## Section 6: NEW Issues Found During Verification

These issues were not present in the original audit and have been introduced or revealed by the Phase A–E changes.

### NEW-01: Seeded Roles in `SeedData.cs` Do Not Match `Roles.cs` Constants (HIGH)

`SeedData.cs` seeds these roles:
```
SystemAdmin, BranchManager, CreditOfficer, CreditAnalyst,
HOReviewer, RiskManager, CommitteeMember, CommitteeChair,
Auditor, CustomerService
```

`Roles.cs` defines these constants:
```
SystemAdmin, LoanOfficer, CreditOfficer, RiskManager,
BranchApprover, HOReviewer, CommitteeMember, FinalApprover,
Operations, Auditor, Customer
```

**Mismatches:**
| SeedData.cs (seeded) | Roles.cs (constant) | Impact |
|----------------------|---------------------|--------|
| `BranchManager` | `BranchApprover` | Any `[Authorize(Roles = "BranchApprover")]` attribute will fail — the role does not exist in DB |
| `CreditAnalyst` | *(not in Roles.cs)* | This role exists in DB but no code references it |
| `CommitteeChair` | *(not in Roles.cs)* | Same — seeded but unreferenced |
| `CustomerService` | `Customer` | Customer self-service features will fail authorization |
| *(not seeded)* | `LoanOfficer` | Loan officers cannot be assigned this role on first boot |
| *(not seeded)* | `FinalApprover` | Final approval step will have no authorized users |
| *(not seeded)* | `Operations` | Disbursement operations will have no authorized users |

This breaks the authorization model at startup. **Both files must be synchronized.**

### NEW-02: `AddComment()` Authorization Logic in CommitteeReview Still Not Fixed (MEDIUM)

The original audit flagged this (M-18) as potentially inverted logic. The code in `CommitteeReview.AddComment()` is unchanged:
```csharp
var member = _members.FirstOrDefault(m => m.UserId == userId);
if (member == null && visibility != CommentVisibility.Internal)
    return Result.Failure("Only committee members can add committee-visible comments");
```

This means **non-committee members CAN add `Internal` visibility comments** to any committee review. The Phase B fixes did not address this. The policy intent (whether non-members should be able to comment at all) remains undocumented and the logic remains unchanged.

---

## Section 7: Status of Deferred Issues

The dev team acknowledged the following items as deferred. Verification confirms they remain open:

| Issue | Deferred Reason | Verification |
|-------|----------------|--------------|
| C-01: Race condition in `CreditChecksCompleted++` | Needs atomic increment | ✅ Still open — non-atomic increment unchanged |
| C-03: Raw bureau response stored unencrypted | Requires key management | ✅ Still open — `RawResponse` field stored in plain text |
| C-09: Duplicate notifications in multi-instance | Needs Redis | ✅ Still open — no distributed lock in `NotificationProcessingService` |
| C-10: AuditLog immutability | Architectural change | ✅ Confirmed non-issue — neither `Entity` nor `AggregateRoot` has `IsDeleted` |
| C-11: `DataAccessLog` calls missing from handlers | Handler updates needed | ✅ Still open — `RequestBureauReportHandler` does not call `LogDataAccessAsync` |
| C-12: Application number collision | Needs uniqueness check | ✅ Still open — 6-char GUID substring, no uniqueness guard |

> **Note on C-10:** Upon direct inspection of `Entity.cs` and `AggregateRoot.cs`, **neither class has an `IsDeleted` field**. The original audit concern was hypothetical ("if soft delete is applied to the base entity"). Since the base classes are clean, the immutability concern is limited to application-level delete operations — which should be restricted by repository access patterns and authorization, not by a flag on the entity itself. This deferred item should be reclassified as a process control concern rather than a code defect.

---

## Section 8: Recommended Actions Before Next UAT Cycle

### Must Fix Before UAT
1. **NEW-01** — Synchronize role names between `SeedData.cs` and `Roles.cs`. Authorization model is broken at startup.
2. **C-02** — Add consent record lookup and `IsValid()` check in `RequestBureauReportHandler`. The compliance gap is not actually closed.
3. **C-08** — Complete file storage integration in `GenerateLoanPackHandler` — inject `IFileStorageService`, call `UploadAsync()`, and implement the download endpoint in `LoanPackController`.

### Fix in Next Sprint
4. **H-19** — Inject `IHttpContextAccessor` or propagate client IP via a scoped audit context service populated by request middleware.
5. **H-20** — Implement sensitive field masking before JSON serialization in `AuditService.LogAsync()`.
6. **NEW-02** — Clarify and correct the `AddComment()` authorization logic in `CommitteeReview`.

### Verify Configuration
7. Confirm EF Core maps `WorkflowInstance.RowVersion` as `[Timestamp]` / `.IsRowVersion()`.
8. Confirm `IFileStorageService` is registered in DI and the correct adapter (Local vs S3) is selected by environment config.
