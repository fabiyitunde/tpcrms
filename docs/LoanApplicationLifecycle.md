# Loan Application Lifecycle — Complete Flow

> **Last updated:** 2026-04-02
> **Scope:** Nigerian corporate loan origination platform (TPCRMS)
> **Architecture:** Blazor Server · Clean Architecture (Domain → Application → Infrastructure → Web.Intranet)
> **Pattern:** Domain-Driven Design · Domain Events · Dual-Status Workflow Engine

---

## Table of Contents

1. [System Overview](#system-overview)
2. [Stage 0 — Creation (Draft)](#stage-0--creation-draft)
3. [Stage 1 — Branch Review (BranchReview)](#stage-1--branch-review)
4. [Stage 2 — Branch Approved → Credit Analysis Start](#stage-2--branch-approved--credit-analysis-start)
5. [Stage 3 — Credit Analysis (CreditAnalysis)](#stage-3--credit-analysis)
6. [Stage 4 — HO Review (HOReview)](#stage-4--ho-review)
7. [Stage 5 — Committee Review (CommitteeCirculation)](#stage-5--committee-review)
8. [Stage 6 — Final Approval (CommitteeApproved)](#stage-6--final-approval)
9. [Stage 7 — Offer Letter & Disbursement (Approved → Disbursed)](#stage-7--offer-letter--disbursement)
10. [Complete Status Transition Table](#complete-status-transition-table)
11. [Dual-Status Workflow Engine](#dual-status-workflow-engine)
12. [Domain Events & Handlers](#domain-events--handlers)
13. [Background Services](#background-services)
14. [Roles & Permissions Summary](#roles--permissions-summary)
15. [Known Issues & Gaps](#known-issues--gaps)

---

## System Overview

### Core Status Enum (`LoanApplicationStatus`)

```
Draft → Submitted → BranchReview → BranchApproved → CreditAnalysis
  → HOReview → CommitteeCirculation → CommitteeApproved → Approved → Disbursed

Terminal statuses: BranchRejected, BranchReturned, CommitteeRejected, Rejected, Disbursed, Closed, Cancelled

Defined but unused: DataGathering, FinalApproval, OfferGenerated, OfferAccepted
```

### Workflow Actions (`WorkflowAction`)
`Create · Submit · Approve · Reject · Return · Assign · Unassign · Escalate · Complete · Cancel · Reopen · MoveToNextStage · RequestInfo · ProvideInfo · Override`

### Committee Decisions (`CommitteeDecision`)
`Approved · ApprovedWithConditions · Rejected · Deferred · Escalated`

### Dual-Status Pattern
Every loan application has **two parallel status fields**:
- `LoanApplication.Status` — the business state read by the UI and all queries
- `WorkflowInstance.CurrentStatus` — the operational workflow state (SLA, assignment, escalation)

Both must be kept in sync. Transitions that update one without the other are the primary source of bugs.

---

## Stage 0 — Creation (Draft)

**Status:** `Draft`
**Who acts:** LoanOfficer

### Entry Point
User navigates to New Loan Application and enters a CBS account number.

### Step-by-Step Process

| # | Step | Source |
|---|------|--------|
| 1 | Fetch customer by account number from Core Banking Service (CBS) | CBS mock: account `1234567890` |
| 2 | Validate customer type = Corporate (rejects individual accounts) | `InitiateCorporateLoanCommand` |
| 3 | Fetch RC number + incorporation date from CBS or use user overrides | `InitiateCorporateLoanCommand` |
| 4 | Create `LoanApplication` aggregate — status = `Draft` | `LoanApplication.CreateCorporate()` |
| 5 | Generate application number: `LA{yyyyMMdd}{6-char hex}` | Domain factory method |
| 6 | Add **Directors** from SmartComply CAC lookup (UI-sourced, BVN user-entered) | `application.AddParty(Director, ...)` |
| 7 | Add **Signatories** from CBS data (BVN editable only if missing from CBS) | `application.AddParty(Signatory, ...)` |
| 8 | Auto-fetch 6-month internal bank statement from CBS | `InitiateCorporateLoanCommand` |
| 9 | Persist all (application + parties + statement) in one `SaveChangesAsync` | `IUnitOfWork` |
| 10 | Domain event fired: `LoanApplicationCreatedEvent` | No handler registered |

### What LoanOfficer Can Edit in Draft
- Loan amount, tenor, interest rate type and value
- Party information: fill missing BVNs, enter shareholding % for directors
- Documents: upload/remove per product document requirements
- Collateral: add property, vehicle, or other collateral
- Guarantors: add guarantors with their BVN
- Additional bank statements (external banks)

### Pre-Submission Validation (checked when Submit button clicked)
- At least 1 bank statement must exist
- All required party fields must be complete (BVN, shareholding for directors)
- Mandatory documents uploaded per loan product requirements

### Exit
LoanOfficer clicks **"Submit for Review"** → Stage 1

---

## Stage 1 — Branch Review

**Status:** `BranchReview`
**Who acts:** BranchApprover
**Workflow SLA:** 48 hours
**Comments required:** No (optional)

### Entry — SubmitLoanApplicationCommand

```
1. Validate bank statements exist
2. application.Submit(userId)
     → Status = Submitted
     → Fires LoanApplicationSubmittedEvent (no handler)
3. application.SubmitForBranchReview(userId)
     → Status = BranchReview
4. WorkflowService.InitializeWorkflowAsync()
     → Creates WorkflowInstance
     → CurrentStatus = BranchReview
     → AssignedRole = "BranchApprover"
     → SLADueAt = Now + 48h
5. SaveChangesAsync
```

### Actions Available

| Button | Role | Result | Notes |
|--------|------|--------|-------|
| **Approve** | BranchApprover | → `BranchApproved` | Triggers credit check queue |
| **Return** | BranchApprover | → `BranchReturned` | Comment required; LoanOfficer can edit and resubmit |
| **Reject** | BranchApprover | → `BranchRejected` ■ | Terminal; comment required |

### Domain Events Raised
- `LoanApplicationSubmittedEvent` — **no handler registered**

---

## Stage 2 — Branch Approved → Credit Analysis Start

**Status:** `BranchApproved` (briefly) → auto-transitions to `CreditAnalysis`
**Who acts:** System (fully automated)
**Workflow SLA:** 24 hours (BranchApproved stage, CreditOfficer assigned)

### Entry — ApproveBranchCommand

```
1. application.ApproveBranch(userId, comment)
     → Status = BranchApproved
     → BranchApprovedAt = now
     → Fires LoanApplicationBranchApprovedEvent
2. SaveChangesAsync
     → DomainEventPublishingInterceptor dispatches event
3. BranchApprovedCreditCheckQueueHandler.HandleAsync()
     → Calls ICreditCheckQueue.QueueCreditCheckAsync(applicationId, userId)
     → Puts CreditCheckRequest into Channel<> (in-memory async queue)
     → Returns immediately — does NOT block
```

### Background Service: CreditCheckBackgroundService

Runs continuously as a hosted service. Reads `CreditCheckRequest` items from the channel one at a time.

```
For each request:
  1. Create DI scope
  2. Resolve ProcessLoanCreditChecksHandler
  3. Execute ProcessLoanCreditChecksCommand(loanApplicationId, systemUserId)
  4. Log result
  5. On exception: log error and continue to next item (no retry)
```

### Inside ProcessLoanCreditChecksCommand

**Entry validation:**
- Status must be `BranchApproved` or `CreditAnalysis`
- If `AllCreditChecksCompleted == true` already → returns cached results (idempotent)

**Count calculation:**
```
partiesWithBVN   = directors + signatories with non-empty BVN, deduplicated by BVN
                   (⚠️ before fix: duplicates counted, causing count mismatch — see Known Issues G1)
guarantorsWithBVN = active guarantors with BVN
hasBusinessCheck  = loan has an RC number

totalChecks = partiesWithBVN.Count + guarantorsWithBVN.Count + (hasBusinessCheck ? 1 : 0)
```

**Transition to CreditAnalysis (when status = BranchApproved):**
```
application.StartCreditAnalysis(totalChecks, systemUserId)
  → Status = CreditAnalysis
  → TotalCreditChecksRequired = totalChecks
  → CreditChecksCompleted = 0
  → CreditCheckStartedAt = now
  → Fires CreditAnalysisStartedEvent (no handler)
```

**Individual credit checks (per unique BVN party or guarantor):**
```
For each party/guarantor:
  1. Check existingBvns set (idempotency — includes Completed, NotFound, Failed)
     → If already processed: skip, do NOT call RecordCreditCheckCompleted again
  2. Get consent: GetValidConsentAsync(bvn, CreditBureauCheck)
     → No consent found:
        · Create BureauReport with Status=Failed, ErrorMessage = "No valid consent..."
        · Add to DB (NDPA audit trail)
        · Do NOT call RecordCreditCheckCompleted (consent failure does not count)
     → Consent found:
        · Call SmartComply CRC Full report
        · Extract: credit score, grade, delinquencies, fraud risk score, fraud recommendation
        · Create BureauReport with Status=Completed
        · Call application.RecordCreditCheckCompleted(systemUserId)
          → CreditChecksCompleted++
          → If CreditChecksCompleted >= TotalCreditChecksRequired:
              · CreditCheckCompletedAt = now
              · Fires AllCreditChecksCompletedEvent
```

**Business credit check (if RC number exists):**
```
  1. Check hasExistingBusinessReport (includes Completed, NotFound, Failed)
     → If exists: skip, do NOT call RecordCreditCheckCompleted
  2. Get consent: GetValidConsentAsync(rcNumber, CreditBureauCheck)
     (RC number stored in ConsentRecord.NIN field)
  3. Call SmartComply CRC Business History
  4. Business reports have NO credit score (shows N/A in UI — this is correct/expected)
  5. Call application.RecordCreditCheckCompleted(systemUserId)
```

**Save:** `SaveChangesAsync` saves all bureau reports + updated loan application.
The `AllCreditChecksCompletedEvent` is dispatched by the interceptor after save.

### AllCreditChecksCompletedWorkflowHandler

```
1. Load loanApplication and workflowInstance fresh from DB
2. Verify workflowInstance.CurrentStatus == CreditAnalysis (skip if not)
3. WorkflowService.TransitionAsync(workflowInstance, HOReview, MoveToNextStage)
     → Updates WorkflowInstance.CurrentStatus = HOReview
     → Updates AssignedRole = "HOReviewer", SLADueAt = now + 48h
4. loanApplication.MoveToHOReview(Guid.Empty)
     → Validates: Status == CreditAnalysis AND AllCreditChecksCompleted == true
     → Status = HOReview
5. SaveChangesAsync
```

> ⚠️ **Current bug (G3):** The return value of `MoveToHOReview()` is not checked. If it fails silently (e.g., because `AllCreditChecksCompleted` is false due to the duplicate BVN issue G1), the workflow transitions to HOReview but `LoanApplication.Status` remains `CreditAnalysis`.

---

## Stage 3 — Credit Analysis

**Status:** `CreditAnalysis`
**Who acts:** CreditOfficer (view only — no action buttons)
**Workflow SLA:** 72 hours
**Transition:** Fully automatic via `AllCreditChecksCompletedEvent`

### What CreditOfficer Sees

- **Bureau Tab** — one card per checked party:
  - Individual cards: credit score, score grade (A+/A/B/C/D/E), active loans, delinquencies, overdue amount, fraud risk score, fraud recommendation
  - Business card: total loans, active loans, delinquent facilities, total outstanding exposure — **no credit score (N/A is expected and correct)**
  - Badge variants:
    - 🟡 **Consent Required** — no NDPA consent found for this BVN
    - 🔴 **Check Failed** — SmartComply API returned an error
    - ⬜ **Not Found** — BVN/RC not in bureau database
- All other tabs (parties, documents, collateral, financials, bank statements) in read-only mode

### How Credit Analysis Ends
When `CreditChecksCompleted >= TotalCreditChecksRequired`:
- `AllCreditChecksCompletedEvent` fires automatically
- Handler auto-transitions both `LoanApplication.Status` and `WorkflowInstance.CurrentStatus` to `HOReview`
- No user action needed

---

## Stage 4 — HO Review

**Status:** `HOReview`
**Who acts:** CreditOfficer *(⚠️ role mismatch — workflow definition assigns HOReviewer, but UI checks for CreditOfficer — see Known Issues G2)*
**Workflow SLA:** 48 hours
**Comments required:** Yes (for all actions)

### Actions Available

| Button | Role (UI) | Result | Notes |
|--------|-----------|--------|-------|
| **Approve** | CreditOfficer | → `CommitteeCirculation` | Opens committee setup modal |
| **Return** | CreditOfficer | → prior stage | Comment required |
| **Reject** | CreditOfficer | → `Rejected` ■ | Terminal; comment required |

### On Approve
- Status → `CommitteeCirculation`
- Committee Setup Modal opens immediately

---

## Stage 5 — Committee Review

**Status:** `CommitteeCirculation`
**Who acts:** CreditOfficer (setup + record decision), CommitteeMember (voting)
**Workflow SLA:** Configurable per committee type

### Committee Structure (`CommitteeReview` aggregate)

```
CommitteeReview {
  CommitteeType: BranchCredit | RegionalCredit | HeadOfficeCredit | ...
  Status: Pending → InProgress → VotingComplete → Decided → Closed
  RequiredVotes: int
  MinimumApprovalVotes: int
  DeadlineAt: DateTime (Now + DeadlineHours)

  Members[] { UserId, UserName, Role, IsChairperson, Vote?, VotedAt?, VoteComment? }
  Comments[] { UserId, Content, Visibility: Public|Private, CreatedAt }

  // Computed
  ApprovalVotes, RejectionVotes, AbstainVotes, PendingVotes: int
  HasQuorum: bool
  HasMajorityApproval: bool
  IsOverdue: bool

  // Set on decision
  FinalDecision: CommitteeDecision?
  ApprovedAmount, ApprovedTenorMonths, ApprovedInterestRate, ApprovalConditions
  DecisionAt, DecisionRationale, DecisionByUserId
}
```

### Step-by-Step Committee Process

**Step 1 — Setup (CreditOfficer)**
- Choose committee type
- Set required votes and minimum approval votes
- Set deadline (default 72 hours)
- Creates `CommitteeReview` with status = `Pending`

**Step 2 — Add Members (CreditOfficer)**
- Add users by ID with their role label
- Mark one member as Chairperson
- Members can add comments at any time

**Step 3 — Start Voting (CreditOfficer)**
- `CommitteeReview.Status` = `InProgress`
- Vote buttons become visible to assigned CommitteeMembers

**Step 4 — Cast Votes (CommitteeMember)**
- Each member votes: **Approve / Reject / Abstain** + optional comment
- Only assigned members can vote
- Vote button visible when: status = CommitteeCirculation AND user is assigned member
- Counters updated: ApprovalVotes++, RejectionVotes++, AbstainVotes++, PendingVotes--

> ⚠️ **Gap (G5):** `HasQuorum` and `HasMajorityApproval` are tracked but not enforced. Decision can be recorded with zero votes.

**Step 5 — Record Decision (CreditOfficer/Chairperson)**

| Decision | Required Input | LoanApplication.Status |
|----------|---------------|----------------------|
| Approved | ApprovedAmount, ApprovedTenorMonths, ApprovedInterestRate | `CommitteeApproved` |
| ApprovedWithConditions | Same + Conditions text | `CommitteeApproved` |
| Rejected | Rationale | `CommitteeRejected` ■ |
| Deferred | Rationale | Back to `HOReview` |
| Escalated | Rationale | (handler not fully implemented) |

**Validations on decision input:**
- ApprovedAmount > 0
- ApprovedTenorMonths between 1 and 360
- ApprovedInterestRate between 0% and 100%

**Domain event: `CommitteeDecisionRecordedEvent`**
Handled by `CommitteeDecisionWorkflowHandler`:
```
If Approved:
  → application.ApproveCommittee(Guid.Empty, approvedMoney, tenor, rate)
     · Sets ApprovedAmount, ApprovedTenorMonths, ApprovedInterestRate
     · Status = CommitteeApproved
  → WorkflowService.TransitionAsync(→ CommitteeApproved)

If Rejected:
  → application.RejectCommittee(Guid.Empty, "Committee decision: Rejected")
     · Status = CommitteeRejected (terminal)
  → WorkflowService.TransitionAsync(→ CommitteeRejected)

If Deferred:
  → Status = HOReview (loops back for more information)
  → WorkflowService.TransitionAsync(→ HOReview)
  → (no explicit "Deferred" status — application returns to HOReview silently)
```

---

## Stage 6 — Final Approval

**Status:** `CommitteeApproved`
**Who acts:** FinalApprover
**Workflow SLA:** 24 hours
**Comments required:** Optional

At this point the loan has locked approved terms:
- `ApprovedAmount` (in NGN)
- `ApprovedTenorMonths`
- `ApprovedInterestRate`
- `ApprovalConditions` (if any)

**Loan Pack** PDF is available from this stage onward (summary of all loan details and approved terms).

### Actions Available

| Button | Role | Result |
|--------|------|--------|
| **Approve** | FinalApprover | → `Approved`; fires `LoanApplicationApprovedEvent` |
| **Reject** | FinalApprover | → `Rejected` ■ (terminal) |

**`LoanApplicationApprovedEvent`** — **no handler registered**
(Should trigger: offer letter generation initiation, customer notification)

---

## Stage 7 — Offer Letter & Disbursement

**Statuses:** `Approved` → `Disbursed` ■
**Who acts:** Operations

### Offer Letter Generation
- Available when status is `Approved` or `Disbursed`
- `GenerateOfferLetterCommand` creates a PDF and stores it to file storage
- Creates `OfferLetter` record with version tracking
- Each re-generation creates a new version; old versions marked `Superseded`
- Current version marked `Generated`
- Download available for all versions

### Disbursement
```
1. Operations clicks "Record Disbursement"
2. Enters CoreBankingLoanId (from CBS after loan is booked)
3. application.RecordDisbursement(coreBankingLoanId, userId)
     → Validates: Status == Approved OR OfferAccepted
     → Status = Disbursed ■
     → DisbursedAt = now
     → CoreBankingLoanId stored
     → Fires LoanApplicationDisbursedEvent (no handler registered)
4. SaveChangesAsync
```

**Terminal status — loan lifecycle complete.**

---

## Complete Status Transition Table

```
Draft
  ──[LoanOfficer: Submit]──────────────────────► BranchReview

BranchReview
  ──[BranchApprover: Approve]─────────────────► BranchApproved
  ──[BranchApprover: Return]──────────────────► BranchReturned  (LoanOfficer edits & resubmits)
  ──[BranchApprover: Reject]──────────────────► BranchRejected ■

BranchApproved
  ──[System: auto via background service]─────► CreditAnalysis

CreditAnalysis
  ──[System: auto when all checks done]───────► HOReview

HOReview
  ──[CreditOfficer*: Approve]─────────────────► CommitteeCirculation
  ──[CreditOfficer*: Return]──────────────────► (prior stage)
  ──[CreditOfficer*: Reject]──────────────────► Rejected ■
  (* role mismatch: should be HOReviewer — see Known Issues G2)

CommitteeCirculation
  ──[decision: Approved]──────────────────────► CommitteeApproved
  ──[decision: ApprovedWithConditions]────────► CommitteeApproved
  ──[decision: Rejected]──────────────────────► CommitteeRejected ■
  ──[decision: Deferred]──────────────────────► HOReview  (loops back)

CommitteeApproved
  ──[FinalApprover: Approve]──────────────────► Approved
  ──[FinalApprover: Reject]───────────────────► Rejected ■

Approved
  ──[Operations: Generate Offer Letter]───────► (OfferGenerated — milestone, not a status change)
  ──[Operations: Record Disbursement]─────────► Disbursed ■

■ = Terminal status (no further transitions)
```

---

## Dual-Status Workflow Engine

### Two Parallel Status Fields

| Field | Table | Set by | Read by |
|-------|-------|--------|---------|
| `LoanApplication.Status` | LoanApplications | Domain methods (ApproveBranch, MoveToHOReview, etc.) | UI, all queries, button visibility |
| `WorkflowInstance.CurrentStatus` | WorkflowInstances | `WorkflowService.TransitionAsync()` | SLA tracking, role assignment, escalation |

### Workflow Initialization
Called in `SubmitLoanApplicationCommand` after status = BranchReview:
```
WorkflowService.InitializeWorkflowAsync(applicationId, loanType, BranchReview, userId)
  → Finds active WorkflowDefinition for the loan type
  → Creates WorkflowInstance:
      CurrentStatus = BranchReview
      AssignedRole = "BranchApprover"
      SLADueAt = now + 48h
      Adds transition log entry
```

### Transition Logic (WorkflowService.TransitionAsync)
```
1. Load WorkflowInstance + WorkflowDefinition
2. Find transition: definition.GetTransition(currentStatus, targetStatus, action)
3. Validate: transition exists
4. Validate: actor role has permission
5. Check: comment required if stage requires it
6. instance.Transition(targetStatus, action, ...)
     → Logs to transition history
     → Updates: CurrentStatus, AssignedRole, SLADueAt, EnteredCurrentStageAt
     → If isTerminal: IsCompleted = true
     → Fires: WorkflowTransitionedEvent or WorkflowInstanceCompletedEvent
7. Persist
```

### Sync Pattern (event handlers)
Every auto-transition handler follows this pattern:
```csharp
// Step 1: Update WorkflowInstance
await _workflowService.TransitionAsync(workflowInstance.Id, targetStatus, action, ...);

// Step 2: Update LoanApplication.Status to match
loanApplication.MoveToHOReview(Guid.Empty); // or ApproveCommittee, RejectCommittee, etc.
_loanApplicationRepository.Update(loanApplication);
await _unitOfWork.SaveChangesAsync();
```

---

## Domain Events & Handlers

| Event | Raised By | Handler | Effect |
|-------|-----------|---------|--------|
| `LoanApplicationCreatedEvent` | `CreateCorporate()` | ❌ None | — |
| `LoanApplicationSubmittedEvent` | `Submit()` | ❌ None | — |
| `LoanApplicationBranchApprovedEvent` | `ApproveBranch()` | ✅ `BranchApprovedCreditCheckQueueHandler` | Queues credit checks to background service |
| `CreditAnalysisStartedEvent` | `StartCreditAnalysis()` | ❌ None | — |
| `AllCreditChecksCompletedEvent` | `RecordCreditCheckCompleted()` | ✅ `AllCreditChecksCompletedWorkflowHandler` | Transitions CreditAnalysis → HOReview (both status fields) |
| `CommitteeDecisionRecordedEvent` | `CommitteeReview.RecordDecision()` | ✅ `CommitteeDecisionWorkflowHandler` | Transitions to CommitteeApproved / CommitteeRejected / HOReview based on decision |
| `LoanApplicationApprovedEvent` | `FinalApprove()` | ❌ None | — |
| `LoanApplicationDisbursedEvent` | `RecordDisbursement()` | ❌ None | — |
| `WorkflowTransitionedEvent` | `WorkflowInstance.Transition()` | ✅ `WorkflowTransitionAuditHandler` | Writes audit log entry |
| `WorkflowSLABreachedEvent` | `WorkflowInstance.MarkSLABreached()` | (not confirmed) | Should trigger escalation notification |
| `CommitteeVoteCastEvent` | `CommitteeReview.CastVote()` | ✅ `CommitteeVoteAuditHandler` | Writes audit log entry |

### Event Publishing Mechanism
- `DomainEventPublishingInterceptor` intercepts every `SaveChangesAsync()`
- Collects all domain events from tracked aggregate roots
- Dispatches each to registered `IDomainEventHandler<TEvent>` implementations
- Handlers run synchronously within the same request/scope

---

## Background Services

### CreditCheckBackgroundService

| Property | Value |
|----------|-------|
| Type | `IHostedService` (started with app, runs continuously) |
| Queue | `Channel<CreditCheckRequest>` (in-memory, unbounded) |
| Processing | Sequential — one request at a time |
| Retry | ❌ None — exceptions logged, processing continues |
| Persistence | ❌ None — queue is lost on app restart |
| Invoked via | `ICreditCheckQueue.QueueCreditCheckAsync()` |
| Triggered by | `BranchApprovedCreditCheckQueueHandler` |

**Processing loop:**
```
await foreach (var request in channel.Reader.ReadAllAsync(stoppingToken))
{
    using var scope = CreateServiceScope();
    var handler = scope.GetRequiredService<ProcessLoanCreditChecksHandler>();
    await handler.Handle(new ProcessLoanCreditChecksCommand(
        request.LoanApplicationId,
        request.SystemUserId));
}
```

---

## Roles & Permissions Summary

### User Roles

| Role | Stage(s) | Key Capabilities |
|------|----------|-----------------|
| **LoanOfficer** | Draft | Create application, add parties, upload documents, submit |
| **BranchApprover** | BranchReview | Approve / Return (with reason) / Reject |
| **CreditOfficer** | CreditAnalysis, HOReview, CommitteeCirculation | View credit results; HOReview approve/return/reject; Setup committee; Record committee decision |
| **HOReviewer** | HOReview | *Intended role but UI currently routes to CreditOfficer (role mismatch bug)* |
| **CommitteeMember** | CommitteeCirculation | Cast vote (Approve/Reject/Abstain), add comments |
| **FinalApprover** | CommitteeApproved | Final approve or reject |
| **Operations** | Approved | Generate offer letter, record disbursement |
| **RiskManager** | Any | Risk oversight and review |
| **Auditor** | Any | Read-only audit trail access |
| **SystemAdmin** | Any | System configuration |

### Role-Action Matrix

| Status | BranchApprover | CreditOfficer | CommitteeMember | FinalApprover | Operations |
|--------|---------------|---------------|-----------------|---------------|------------|
| BranchReview | ✅ Approve/Return/Reject | — | — | — | — |
| BranchApproved | — | 👁 View | — | — | — |
| CreditAnalysis | — | 👁 View | — | — | — |
| HOReview | — | ✅ Approve/Return/Reject | — | — | — |
| CommitteeCirculation | — | ✅ Setup, Record decision | ✅ Vote, Comment | — | — |
| CommitteeApproved | — | — | — | ✅ Approve/Reject | — |
| Approved | — | — | — | — | ✅ Generate offer, Disburse |

---

## Known Issues & Gaps

### 🔴 Blocking Issues

| # | Issue | Root Cause | Impact |
|---|-------|-----------|--------|
| **G1** | **Duplicate BVN in `TotalCreditChecksRequired`** | Same person (e.g., John Adebayo) added as both Director and Signatory from CBS data. `totalChecks` counts them twice; processing loop checks each BVN once. `CreditChecksCompleted` never reaches `TotalCreditChecksRequired`. `AllCreditChecksCompletedEvent` never fires. | Loan stuck permanently at CreditAnalysis |

**Fix:** Deduplicate `partiesWithBVN` by BVN before calculating `totalChecks`:
```csharp
var partiesWithBVN = loanApp.Parties
    .Where(p => !string.IsNullOrEmpty(p.BVN))
    .GroupBy(p => p.BVN!, StringComparer.OrdinalIgnoreCase)
    .Select(g => g.First())
    .ToList();
```

### 🟠 High Severity

| # | Issue | Root Cause | Impact |
|---|-------|-----------|--------|
| **G2** | **Role mismatch at HOReview** | Workflow definition assigns role `HOReviewer`; UI button visibility checks `CreditOfficer`. HOReviewer role user sees no action buttons. | HOReviewer cannot act; CreditOfficer has unintended elevated access |
| **G3** | **`MoveToHOReview()` return value ignored** | `AllCreditChecksCompletedWorkflowHandler` calls `loanApplication.MoveToHOReview()` without checking the `Result` return value. If it fails (e.g., due to G1), the workflow transitions but the loan status stays `CreditAnalysis`. | Workflow and loan status become out of sync; UI shows wrong stage |

**Fix for G3:**
```csharp
var moveResult = loanApplication.MoveToHOReview(Guid.Empty);
if (moveResult.IsFailure)
{
    _logger.LogError("MoveToHOReview failed for loan {LoanId}: {Error}", ...);
    return; // do not save
}
```

### 🟡 Medium Severity

| # | Issue | Root Cause | Impact |
|---|-------|-----------|--------|
| **G4** | **No consent enforcement** | Consent failures mark bureau check as `NoConsent` and are not counted. If all parties lack consent, `AllCreditChecksCompleted` is never true. No UI indicator or workflow block. | Loan stuck in CreditAnalysis if consent not pre-seeded |
| **G5** | **Committee quorum not enforced** | `HasQuorum` and `HasMajorityApproval` are tracked but not validated before recording a decision. | Chairperson can record a decision with 0 votes |
| **G6** | **In-memory credit check queue** | `Channel<CreditCheckRequest>` is not persisted. App restart loses all pending items. | Credit checks for any loan approved before restart are permanently lost |

### 🟢 Low Severity / Design Gaps

| # | Issue | Impact |
|---|-------|--------|
| **G7** | `FinalApproval` and `OfferAccepted` statuses defined in enum but never set by any code path | Enum bloat; dead code |
| **G8** | `LoanApplicationCreatedEvent`, `LoanApplicationSubmittedEvent`, `LoanApplicationApprovedEvent`, `LoanApplicationDisbursedEvent` have no registered handlers | No downstream automation (notifications, audit, CBS booking) on these key events |
| **G9** | `Guid.Empty` used as system user ID in all event handlers (`MoveToHOReview(Guid.Empty)`, etc.) | Audit trail shows empty GUID for all system-driven transitions |
| **G10** | Deferred committee decision returns to HOReview silently — no "Deferred" status or indication in UI | Reviewers don't know they're handling a deferred case |
| **G11** | No retry logic in `CreditCheckBackgroundService` | Transient SmartComply API failures permanently fail the check; manual re-trigger required |

---

## Mock Data Reference (Development / Test)

### CBS Mock — Account `1234567890` (Corporate)
| Role | Name | BVN | Notes |
|------|------|-----|-------|
| Director | John Adebayo | `22234567890` | Also a Signatory — causes G1 duplicate BVN bug |
| Director | Amina Ibrahim | `22234567891` | — |
| Director | Chukwuma Okonkwo | `22234567892` | — |
| Signatory | John Adebayo | `22234567890` | Same as Director above |
| Signatory | Fatima Bello | `22234567893` | Unique to signatories |
| Business | Acme Industries Ltd | RC: `RC123456` | IncorporationNumber field |

### SmartComply Mock — Known BVNs
| BVN | Name | Score | Delinquencies |
|-----|------|-------|--------------|
| `22234567890` | JOHN ADEBAYO | ~760 (A) | 0 |
| `22234567891` | AMINA IBRAHIM | ~570 (D) | 2 |
| `22234567892` | CHUKWUMA OKONKWO | ~390 (E) | 4 |
| `22234567893` | FATIMA BELLO | ~720 (A) | 0 |
| `22212345678` | OLUWASEUN BAKARE | ~790 (A+) | 0 |

### SmartComply Mock — Known RC Numbers
| RC Number | Business Name |
|-----------|--------------|
| `RC123456` | CAPITALFIELD ASSET MGT LTD |
| `RC654321` | ACME INDUSTRIES LIMITED |

### Seeded Consent Records (global, not application-specific)
- `22234567890`, `22234567891`, `22234567892`, `22234567893`, `22212345678` (individual BVNs)
- `RC123456`, `RC654321` stored in `ConsentRecord.NIN` (business RC numbers)

---

*End of document.*
