# CommitteeWorkflow Module

## Overview

The CommitteeWorkflow module manages the multi-user committee approval process for corporate loans. It enables committee members to review loan applications, cast votes, add comments, attach documents, and record final decisions with full audit trail.

## Key Features

- **Multi-Committee Support**: Branch, Regional, HO, Management, and Board credit committees
- **Member Management**: Add/remove committee members with role assignments
- **Voting System**: Approve, Reject, Abstain votes with quorum tracking
- **Comment Threads**: Committee members can discuss applications
- **Document Attachments**: Supporting documents with visibility controls
- **Decision Recording**: Final decision with approved terms and conditions
- **Deadline Tracking**: Configurable deadlines with overdue monitoring

## Domain Model

### CommitteeReview (Aggregate Root)

```
CommitteeReview
├── CommitteeMember[] - Assigned committee members
├── CommitteeComment[] - Discussion comments
└── CommitteeDocument[] - Attached documents
```

| Property | Description |
|----------|-------------|
| LoanApplicationId | Associated loan application |
| CommitteeType | Type of committee (Branch, HO, Management, etc.) |
| Status | Pending, InProgress, VotingComplete, Decided, Closed |
| RequiredVotes | Minimum members needed for quorum |
| MinimumApprovalVotes | Votes needed for approval |
| DeadlineAt | Deadline for committee decision |
| FinalDecision | Approved, ApprovedWithConditions, Rejected, Deferred, Escalated |

### CommitteeMember

| Property | Description |
|----------|-------------|
| UserId | Committee member user |
| UserName | Display name |
| Role | Member's role/title |
| IsChairperson | Whether this member can record final decision |
| Vote | Approve, Reject, Abstain |
| VotedAt | When vote was cast |
| VoteComment | Comment with vote |

### Committee Types

| Type | Description | Typical Limit |
|------|-------------|---------------|
| BranchCredit | Branch-level committee | Up to ₦50M |
| RegionalCredit | Regional committee | ₦50M - ₦200M |
| HeadOfficeCredit | HO credit committee | ₦200M - ₦500M |
| ManagementCredit | Management committee | ₦500M - ₦2B |
| BoardCredit | Board credit committee | Above ₦2B |

### Review Status Flow

```
┌─────────┐    ┌────────────┐    ┌─────────────────┐    ┌─────────┐    ┌────────┐
│ Pending │───►│ InProgress │───►│ VotingComplete  │───►│ Decided │───►│ Closed │
└─────────┘    └────────────┘    └─────────────────┘    └─────────┘    └────────┘
     │               │                                        │
     │               │                                        ▼
     │               └────────────────────────────────► [Chairperson
     │                                                   can decide
     └── Members assigned                                early if needed]
```

## Voting Rules

### Quorum
- Review cannot proceed without minimum required votes
- `RequiredVotes` defines quorum threshold
- `HasQuorum = VotesCast >= RequiredVotes`

### Majority
- Approval requires `MinimumApprovalVotes` approvals
- `HasMajorityApproval = ApprovalVotes >= MinimumApprovalVotes`

### Vote Types
- **Approve**: Support the application
- **Reject**: Do not support the application  
- **Abstain**: Recuse from voting (still counts toward quorum)

## Committee Decisions

| Decision | Description |
|----------|-------------|
| Approved | Full approval with approved terms |
| ApprovedWithConditions | Approval subject to conditions |
| Rejected | Application rejected |
| Deferred | Requires more information |
| Escalated | Escalate to higher committee |

### Approved Terms (when Approved)
- `ApprovedAmount` - Final approved amount
- `ApprovedTenorMonths` - Final approved tenor
- `ApprovedInterestRate` - Final approved rate
- `ApprovalConditions` - Conditions for disbursement

## API Endpoints

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | /api/Committee/{id} | Get review by ID | Any |
| GET | /api/Committee/by-loan-application/{id} | Get by loan app | Any |
| GET | /api/Committee/my-pending-votes | User's pending votes | Any |
| GET | /api/Committee/my-reviews | User's reviews | Any |
| GET | /api/Committee/by-status/{status} | Reviews by status | CreditOfficer+ |
| GET | /api/Committee/overdue | Overdue reviews | CreditOfficer+ |
| GET | /api/Committee/{id}/voting-summary | Voting statistics | Any |
| POST | /api/Committee | Create review | CreditOfficer+ |
| POST | /api/Committee/{id}/members | Add member | CreditOfficer+ |
| POST | /api/Committee/{id}/start-voting | Start voting | CreditOfficer+ |
| POST | /api/Committee/{id}/vote | Cast vote | CommitteeMember |
| POST | /api/Committee/{id}/comments | Add comment | Any |
| POST | /api/Committee/{id}/decision | Record decision | Chairperson |
| POST | /api/Committee/{id}/close | Close review | CreditOfficer+ |

## Usage Flow

### 1. Create Review

```json
POST /api/Committee
{
  "loanApplicationId": "...",
  "committeeType": "HeadOfficeCredit",
  "requiredVotes": 5,
  "minimumApprovalVotes": 3,
  "deadlineHours": 72
}
```

### 2. Add Members

```json
POST /api/Committee/{id}/members
{
  "userId": "...",
  "userName": "John Smith",
  "role": "Credit Committee Chair",
  "isChairperson": true
}
```

### 3. Start Voting

```json
POST /api/Committee/{id}/start-voting
```

### 4. Cast Votes

```json
POST /api/Committee/{id}/vote
{
  "vote": "Approve",
  "comment": "Strong financials, good collateral coverage"
}
```

### 5. Record Decision

```json
POST /api/Committee/{id}/decision
{
  "decision": "Approved",
  "rationale": "Committee unanimously approved based on strong DSCR and collateral",
  "approvedAmount": 500000000,
  "approvedTenorMonths": 48,
  "approvedInterestRate": 18.5,
  "conditions": "Quarterly financial reporting required"
}
```

### 6. Close Review

```json
POST /api/Committee/{id}/close
```

## Visibility Controls

### Comment Visibility
- **Committee**: Only committee members can see
- **Internal**: All bank staff can see
- **Applicant**: Include in customer communications (rare)

### Document Visibility
- **Committee**: Committee members only
- **Internal**: All bank staff
- **Public**: Include in loan pack PDF

## Domain Events

| Event | Trigger | Use |
|-------|---------|-----|
| CommitteeReviewCreatedEvent | Review created | Notifications |
| CommitteeMemberAddedEvent | Member added | Notify member |
| CommitteeVotingStartedEvent | Voting started | Notify all members |
| CommitteeVoteCastEvent | Vote cast | Track progress |
| CommitteeVotingCompletedEvent | All votes in | Alert chairperson |
| CommitteeCommentAddedEvent | Comment added | Activity tracking |
| CommitteeDecisionRecordedEvent | Decision made | Workflow transition |
| CommitteeReviewClosedEvent | Review closed | Audit trail |

## Integration Points

- **WorkflowEngine**: Committee decision triggers workflow transition
- **LoanApplication**: Updates loan status based on committee decision
- **NotificationService**: Sends notifications for all events
- **AuditService**: All actions logged for compliance
- **LoanPackGenerator**: Committee comments included in loan pack

## Files

### Domain
- `Aggregates/Committee/CommitteeReview.cs` - Aggregate root
- `Aggregates/Committee/CommitteeMember.cs` - Member entity
- `Aggregates/Committee/CommitteeComment.cs` - Comment entity
- `Aggregates/Committee/CommitteeDocument.cs` - Document entity
- `Enums/CommitteeEnums.cs` - All committee enums
- `Interfaces/ICommitteeRepository.cs` - Repository interface

### Application
- `Committee/Commands/CommitteeCommands.cs` - All commands
- `Committee/Queries/CommitteeQueries.cs` - All queries
- `Committee/DTOs/CommitteeDtos.cs` - DTOs

### Infrastructure
- `Persistence/Configurations/Committee/CommitteeConfigurations.cs` - EF mappings
- `Persistence/Repositories/CommitteeReviewRepository.cs` - Repository

### API
- `Controllers/CommitteeController.cs` - REST endpoints

## Future Enhancements

1. **Digital Signatures**: E-signature for committee decisions
2. **Proxy Voting**: Allow delegation during absence
3. **Parallel Committees**: Multiple committees reviewing simultaneously
4. **Voting Reminders**: Automated reminders for pending votes
5. **Anonymous Voting**: Optional anonymous voting mode
6. **Committee Templates**: Predefined committee compositions
