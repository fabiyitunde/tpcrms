# Audit Report: WorkflowEngine Module

**Module ID:** 11
**Audit Date:** 2026-02-17
**Auditor:** Domain Expert Review
**Module Status (Documented):** 🟢 Completed
**Audit Verdict:** ⚠️ Security and Concurrency Gaps — Requires Fixes

---

## 1. Executive Summary

The WorkflowEngine is well-architectured with a clean state machine, SLA tracking, domain events for audit integration, and role-based queue management. However, there are several significant issues: role validation for transitions is not enforced at the domain level, the workflow seeding endpoint is unsafe in production, there is no concurrent transition protection, and the `WorkflowAction` enum conflates internal states with user-invocable actions.

---

## 2. Security Issues

### 2.1 No Role Validation in Domain Transition Method (HIGH)

The `WorkflowInstance.Transition()` method takes a `performedByUserId` and performs the transition, but **does not validate** that the performing user actually holds the `RequiredRole` for the transition. This check must happen at the Application layer (handler), and it's unclear whether it does.

**Scenario:**
- A `LoanOfficer` could call `POST /api/Workflow/{id}/transition` to approve the Branch Review stage
- If the Application layer handler doesn't check roles, the transition succeeds
- The API endpoint documentation shows `Auth: By Role` but the implementation detail is unspecified

**Recommendation:**
- In `ExecuteWorkflowTransitionHandler`, load the `WorkflowTransition` definition and verify the calling user's role matches `WorkflowTransition.RequiredRole`
- Also verify the user is in `WorkflowInstance.AssignedRole` or `AssignedToUserId`
- Add a unit test that verifies role enforcement

### 2.2 Workflow Seeding Endpoint Is Production-Unsafe (HIGH)

```
POST /api/Workflow/seed-corporate-workflow
```

This endpoint seeds the workflow definition. If accessible in production:
- A malicious admin or compromised account could reseed the workflow, changing approval stages and SLAs mid-operation
- Existing `WorkflowInstance` records would reference the old definition but new transitions would use the new seeded definition

**Recommendation:**
- Restrict this endpoint to `[Authorize(Roles = "SystemAdmin")]` AND `[AllowedEnvironments("Development", "Staging")]`
- In production, workflow definitions should be managed through a proper admin UI with maker-checker controls, not a seeding endpoint
- Add a check that refuses to reseed if active workflow instances exist

### 2.3 `Assign` and `Unassign` Endpoints Have No Role Restriction

The API documentation shows `Auth: Any` for both:
```
POST /api/Workflow/{id}/assign
POST /api/Workflow/{id}/unassign
```

This means any authenticated user can assign or unassign any workflow item, regardless of role. A loan officer could self-assign a branch approval workflow item.

**Recommendation:** Restrict assignment to managers and system admins, or at minimum to the role that owns the current stage.

---

## 3. Concurrency Issues

### 3.1 No Concurrent Transition Protection (HIGH)

If two users simultaneously submit an action on the same `WorkflowInstance` (e.g., two branch approvers both clicking "Approve"), both transitions may succeed, leading to duplicate `WorkflowTransitionLog` entries and an inconsistent state.

**Recommendation:**
- Add a `RowVersion` / `Timestamp` concurrency token to `WorkflowInstance` in EF Core configuration
- This causes EF Core to throw `DbUpdateConcurrencyException` if the row was modified since it was read
- The Application handler should catch this and return an appropriate error (409 Conflict)

---

## 4. Design Issues

### 4.1 `WorkflowAction.Create` Exposes Internal State (MEDIUM)

The `WorkflowAction` enum includes `Create` and `Complete` which are internal system actions. These are stored in `WorkflowTransitionLog` but also appear to be part of the same enum used for user-invocable transition actions. This creates confusion and risk:
- The API could potentially accept `action: "Create"` as a valid transition action from an external user
- The seeded `WorkflowDefinition.Transitions` should not reference `Create` or `Complete` as user-facing actions

**Recommendation:** Separate internal system actions from user-facing workflow actions. Consider an `InternalWorkflowAction` enum for `Create`, `Complete`, `SystemTransition`.

### 4.2 Workflow Definition Version Not Checked on Transition (MEDIUM)

`WorkflowInstance.WorkflowDefinitionId` references the definition used at creation time. If the definition is reseeded (new version), existing workflow instances still reference the old definition ID. However, the transition handler may load the *current* (new) definition and apply it to old instances.

**Impact:** An instance created under workflow version 1 may have transitions executed against workflow version 2 rules, causing incorrect role assignments or SLA values.

**Recommendation:**
- Store `WorkflowDefinitionVersion` in `WorkflowInstance`
- Always load the definition by `WorkflowDefinitionId` (the one stored at creation), not by type

### 4.3 SLA Breach Detection Is Passive (MEDIUM)

`WorkflowInstance.IsSLABreached` is set when `MarkSLABreached()` is called. However, this method must be called by an external process (background service or query). If the background service is down or not running, SLA breaches will not be detected or reported.

**Recommendation:**
- Ensure a background SLA monitoring service runs on a regular schedule (e.g., every 15 minutes)
- Also compute `IsSLADue()` at read time as a real-time check for the overdue queue
- Add a startup health check that verifies the SLA monitoring service is running

---

## 5. Missing Features

### 5.1 No Workflow Exists for Retail Loans

Only the corporate workflow is seeded. When Phase 2 retail loans are implemented, a separate workflow definition (or a mechanism to share stages) must be designed. The current seeding pattern should be extensible.

### 5.2 HO Review Auto-Advance After Credit Analysis Not Implemented

After all credit checks complete (`AllCreditChecksCompletedEvent`), the loan should automatically transition from `CreditAnalysis` to `HOReview`. The domain event is published by `LoanApplication`, but it's not confirmed that a `WorkflowEngine` handler exists to execute this transition automatically.

**Recommendation:** Implement a `AllCreditChecksCompletedEventHandler` in the WorkflowEngine that calls `WorkflowService.TransitionAsync()` to move to `HOReview`.

---

## 6. Recommendations Summary

| Priority | Item |
|----------|------|
| HIGH | Enforce role validation in `ExecuteWorkflowTransitionHandler` |
| HIGH | Restrict `seed-corporate-workflow` endpoint to dev/staging only |
| HIGH | Add `RowVersion` concurrency token to prevent concurrent transitions |
| HIGH | Add role restriction to assign/unassign endpoints |
| MEDIUM | Separate internal `WorkflowAction` values from user-facing actions |
| MEDIUM | Store and enforce workflow definition version in instances |
| MEDIUM | Ensure SLA breach background service is robust with health checks |
| MEDIUM | Implement `AllCreditChecksCompletedEventHandler` for auto-advance to HOReview |
| LOW | Design retail workflow definition for Phase 2 |
