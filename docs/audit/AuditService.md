# Audit Report: AuditService Module

**Module ID:** 15
**Audit Date:** 2026-02-17
**Auditor:** Domain Expert Review
**Module Status (Documented):** 🟢 Completed
**Audit Verdict:** ⚠️ Compliance Gaps — True Immutability Not Enforced

---

## 1. Executive Summary

The AuditService is thoughtfully designed with a comprehensive set of audit actions, categories, and sensitive data types. The event-driven integration pattern provides loose coupling while ensuring critical actions are logged. However, there are fundamental compliance issues: the audit logs are not truly immutable (soft delete applies), IP address capture is not implemented, sensitive data (BVN) may appear in plain text in JSON audit fields, and several direct-call audit points may be missed (not covered by domain events). The `DataAccessLog` for bureau data is also flagged as a future item but is a current compliance requirement.

---

## 2. Compliance-Critical Issues

### 2.1 Soft Delete on Audit Logs Breaks Immutability (CRITICAL)

The base entity class includes an `IsDeleted` soft delete flag. If `AuditLog` inherits from this base entity, it can be soft-deleted via database operations or if any shared `DeleteEntity()` infrastructure is applied. True audit logs for regulatory compliance must be immutable — deletable only through an archival process (never via application-level delete).

**Recommendation:**
- `AuditLog` and `DataAccessLog` must NOT inherit from the common `Entity`/`AggregateRoot` base class that includes `IsDeleted`
- Create a separate `ImmutableRecord` base class with only `Id`, `CreatedAt`, and `CreatedBy` — no `IsDeleted`, no `ModifiedAt`, no `ModifiedBy`
- Apply `OnDelete(DeleteBehavior.Restrict)` in EF Core to prevent cascade deletes
- Add a DB-level trigger or application guard rejecting any UPDATE or DELETE on `AuditLogs` table

### 2.2 No Hash Chain for Tamper Detection (HIGH)

A determined DBA or compromised infrastructure could modify audit log records without detection. For banking regulation compliance, audit logs should be tamper-evident.

**Recommendation:**
- Implement a hash chain: each `AuditLog` entry stores a hash of `(previous_entry_hash + current_entry_data)`
- A periodic integrity check can walk the chain and detect any break
- Alternative (simpler): Replicate audit logs to a separate write-once storage (e.g., Azure Immutable Blob Storage, S3 Object Lock with WORM compliance)

### 2.3 `DataAccessLog` for BVN and Bureau Access Listed as "Future" (HIGH)

The `AuditService.md` notes this as part of the implementation, but the `CreditBureauIntegration` module explicitly does not call `AuditService.LogDataAccessAsync()` when bureau reports are accessed. This is a present-day NDPA compliance gap, not a future enhancement.

**Recommendation:** Immediately implement `DataAccessLog` calls when:
- A `BureauReport`'s raw data is read
- A BVN value is displayed or exported
- Any sensitive field is accessed outside the normal flow

### 2.4 IP Address Not Captured (HIGH)

`AuditLog` has an `IpAddress` field but the documentation does not confirm it is populated. If `AuditService.LogXxx()` methods accept `ipAddress` as a parameter but callers don't pass it (or it's not extracted from `IHttpContextAccessor`), all audit logs will have a null IP.

**Recommendation:**
- Inject `IHttpContextAccessor` into the audit service or use a middleware that propagates `ClientIp` via a scoped service
- Ensure IP is captured for all user-initiated actions
- For event-driven audit (domain event handlers), store the originating IP from the command context

---

## 3. Coverage Gaps

### 3.1 Direct Service Audit Points May Be Missed

The domain event pipeline covers critical workflow/committee/config events. However, several actions rely on explicit `AuditService.LogXxx()` calls that may not be implemented:

| Action | Status |
|--------|--------|
| Document upload | Requires explicit call in `UploadDocumentHandler` |
| Document verification | Requires explicit call in `VerifyDocumentHandler` |
| Collateral approval/rejection | Requires explicit call in `ApproveCollateralHandler` |
| Guarantor credit check | Requires explicit call in handler |
| Financial statement verification | Requires explicit call in `VerifyFinancialStatementHandler` |
| Advisory generation | Requires explicit call in `GenerateCreditAdvisoryHandler` |
| Login/Logout | Requires explicit call in `AuthService` |

**Recommendation:** Audit every handler for missing `AuditService` calls. Consider using a decorator pattern or MediatR pipeline behavior to automatically log all command executions.

---

## 4. Data Quality Issues

### 4.1 BVN May Appear in Plain Text in `OldValues`/`NewValues` JSON (HIGH)

`AuditLog` stores old and new values as JSON strings. If any handler passes a DTO or entity containing BVN in these JSON fields, the BVN will be stored in plain text in the audit log — even if it's encrypted elsewhere.

**Recommendation:**
- Define an exclusion list of sensitive field names that must be masked in `OldValues`/`NewValues`
- Before serializing audit context, apply masking: `BVN: "22234****90"`, `AccountNumber: "****7890"`

### 4.2 No Pagination for Audit Log Export

The search endpoint returns paginated results, but there is no export endpoint. For compliance reporting, auditors need to export entire audit trails for specific loan applications or date ranges. Downloading 50 records at a time for a 10,000 entry audit trail is impractical.

**Recommendation:** Add `GET /api/Audit/export?loanApplicationId=...&format=csv` endpoint with streaming response.

---

## 5. API Authorization Issues

### 5.1 Role Naming: `ComplianceOfficer` vs `Auditor`

The AuditService restricts access to `ComplianceOfficer`, `RiskManager`, and `SystemAdministrator`. However, as noted in the IdentityService audit, the predefined role name appears to be `Auditor`, not `ComplianceOfficer`. This naming inconsistency means the authorization attribute will fail to match and the correct role will not have access.

**Recommendation:** Standardize on `Auditor` (or `ComplianceOfficer`) across `Roles.cs`, `AuditController`, and `ReportingController`.

---

## 6. Recommendations Summary

| Priority | Item |
|----------|------|
| CRITICAL | Prevent soft delete of `AuditLog` and `DataAccessLog` — implement separate immutable base class |
| CRITICAL | Implement `DataAccessLog` calls immediately for bureau report and BVN access |
| HIGH | Implement IP address capture in all audit log entries |
| HIGH | Mask sensitive fields (BVN, AccountNumber) in `OldValues`/`NewValues` JSON |
| HIGH | Audit all handlers for missing explicit `AuditService.LogXxx()` calls |
| HIGH | Reconcile `ComplianceOfficer` vs `Auditor` role naming |
| MEDIUM | Implement hash chain or WORM storage for tamper-evident audit logs |
| MEDIUM | Add audit log export endpoint (CSV/Excel) for compliance reporting |
| LOW | Implement retention policy / archival for audit logs |
