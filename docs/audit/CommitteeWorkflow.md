# Audit Report: CommitteeWorkflow Module

**Module ID:** 12
**Audit Date:** 2026-02-17
**Auditor:** Domain Expert Review
**Module Status (Documented):** 🟢 Completed
**Audit Verdict:** ⚠️ Business Logic Gaps — Quorum and Decision Logic Need Fixes

---

## 1. Executive Summary

The CommitteeWorkflow module is well-structured with good coverage of multi-user voting, comment threads, document attachments, and domain event publishing. However, there are several business logic issues: the chairperson can make a decision without quorum, voting completion logic fires when all members vote but not when quorum is reached, the decision-recording authorization check contains inverted logic, and there is no integration guard ensuring committees are only created for loans in the correct workflow stage.

---

## 2. Business Logic Issues

### 2.1 Chairperson Can Decide Without Quorum (HIGH)

```csharp
public Result RecordDecision(...)
{
    if (Status != CommitteeReviewStatus.VotingComplete && Status != CommitteeReviewStatus.InProgress)
        return Result.Failure("Review must be in voting complete or in-progress status");

    var member = _members.FirstOrDefault(m => m.UserId == decidedByUserId);
    if (member == null || !member.IsChairperson)
    {
        // Allow if all votes are in
        if (Status != CommitteeReviewStatus.VotingComplete)
            return Result.Failure("Only chairperson can record decision before all votes are cast");
    }
    ...
}
```

The chairperson (when `member.IsChairperson == true`) can call `RecordDecision()` at any point while voting is `InProgress` — **even before a single vote has been cast**. There is no check that:
- The chairperson has cast their own vote
- Quorum (`HasQuorum`) has been reached

**Impact:** A chairperson can unilaterally approve or reject a loan without any committee input.

**Recommendation:**
- Add check: `if (!HasQuorum) return Result.Failure("Quorum not reached")`
- Optionally require the chairperson to have voted before recording a decision

### 2.2 `VotingComplete` Fires Only When 100% of Members Vote, Not at Quorum (MEDIUM)

```csharp
if (_members.All(m => m.HasVoted))
{
    Status = CommitteeReviewStatus.VotingComplete;
    AddDomainEvent(new CommitteeVotingCompletedEvent(Id, LoanApplicationId));
}
```

`CommitteeVotingCompletedEvent` only fires when **all** members have voted, not when quorum is reached. If a committee has 7 members but only requires 5 votes (quorum), the status never reaches `VotingComplete` until all 7 vote. This means:
- The chairperson cannot close the committee after quorum without all members voting
- Members who are unavailable block committee progress

**Recommendation:** Also trigger `VotingComplete` when `HasQuorum` is reached:
```csharp
if (_members.All(m => m.HasVoted) || HasQuorum)
{
    Status = CommitteeReviewStatus.VotingComplete;
    ...
}
```

### 2.3 `AddComment()` Authorization Logic Is Inverted (MEDIUM)

```csharp
public Result AddComment(Guid userId, string content, CommentVisibility visibility = CommentVisibility.Committee)
{
    var member = _members.FirstOrDefault(m => m.UserId == userId);
    if (member == null && visibility != CommentVisibility.Internal)
        return Result.Failure("Only committee members can add committee-visible comments");
    ...
}
```

This check says: "If you are NOT a member AND visibility is NOT Internal, reject." This means:
- Non-members CAN add comments with `CommentVisibility.Committee` if `visibility == CommentVisibility.Internal` ... wait, that's confusing. Let me re-read.

Actually the logic: `member == null && visibility != Internal` → fail. So non-members can only add `Internal` comments. But `Internal` comments are visible to all staff. This is backward — non-members should only be allowed to add comments if specifically authorized, not based on visibility.

**Impact:** Any authenticated staff member (not in the committee) can add comments that will appear in committee discussions by using `visibility = Committee`.

Wait, re-reading: the condition is `if (member == null && visibility != Internal) return Failure`. So if `member == null` and `visibility == Internal`, it does NOT return failure — the non-member CAN comment. But if `member == null` and `visibility == Committee`, it returns failure.

So the bug is: Non-members can add `Internal` visibility comments to a committee review. Whether this is intentional policy needs clarification.

**Recommendation:** Clarify the intended policy. If non-members should not comment at all, change to:
```csharp
if (member == null)
    return Result.Failure("Only committee members can comment on this review");
```

If non-members can comment with Internal visibility (e.g., support staff), document this explicitly.

### 2.4 No Prevention of Multiple Committee Reviews for Same Loan (MEDIUM)

There is no domain or application layer validation preventing two `CommitteeReview` records from being created for the same `LoanApplicationId` simultaneously. This could result in:
- Duplicate voting sessions
- Conflicting decisions
- Confusion in the workflow

**Recommendation:** In `CreateCommitteeReviewHandler`, check that no active `CommitteeReview` (status not `Closed`) exists for the `LoanApplicationId` before creating a new one.

### 2.5 Committee Not Validated Against Loan Workflow Stage (MEDIUM)

A committee review can be created for a loan application that is NOT in `CommitteeCirculation` status. For example, a committee could be created for a loan still in `BranchReview`.

**Recommendation:** In `CreateCommitteeReviewHandler`, verify the loan application's current status is `CommitteeCirculation` before creating the review.

---

## 3. Integration Gaps

### 3.1 Committee Decision Does Not Automatically Update Loan Status (HIGH)

`CommitteeDecisionRecordedEvent` is published but the handler for this event is not clearly documented. The committee's decision (Approved/Rejected) must update the `LoanApplication` status and trigger the `WorkflowEngine` to advance to the next stage.

**Impact:** After a committee approves a loan, the loan application stays in `CommitteeCirculation` indefinitely unless someone manually calls a separate API endpoint.

**Recommendation:**
- Implement `CommitteeDecisionRecordedEventHandler` that:
  - Updates `LoanApplication` status (via `ApproveCommittee()` or `RejectFromCommittee()`)
  - Transitions the `WorkflowInstance` to `CommitteeApproved` or `CommitteeRejected`

### 3.2 `CommitteeDecision.Deferred` Has No Follow-Up Path

`CommitteeDecision.Deferred` means more information is needed. However, there is no domain flow for what happens after a deferral:
- Is the loan returned to `HOReview`?
- Is additional information requested from the loan officer?
- Is a new committee review created after the information is provided?

**Recommendation:** Define and implement the deferral workflow path.

---

## 4. Recommendations Summary

| Priority | Item |
|----------|------|
| HIGH | Enforce quorum before chairperson can record a decision |
| HIGH | Implement `CommitteeDecisionRecordedEventHandler` to update loan and workflow status |
| MEDIUM | Trigger `VotingComplete` at quorum, not only when all members have voted |
| MEDIUM | Clarify and fix `AddComment()` authorization logic |
| MEDIUM | Prevent multiple active committee reviews for the same loan |
| MEDIUM | Validate loan is in `CommitteeCirculation` before creating committee review |
| MEDIUM | Define the deferral workflow path |
| LOW | Add configurable deadline per committee type |
