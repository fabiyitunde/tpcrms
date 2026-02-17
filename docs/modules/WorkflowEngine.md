# WorkflowEngine Module

## Overview

The WorkflowEngine module manages the state machine for loan application approvals. It provides centralized workflow definition, role-based queue management, SLA tracking, and full audit trail of all transitions.

## Key Features

- **Configurable Workflow Definitions**: Define valid states, transitions, and SLAs per loan type
- **Role-Based Queues**: Automatically route work to appropriate roles
- **SLA Tracking**: Monitor overdue items with escalation capability
- **User Assignment**: Assign specific items to individual users
- **Full Audit Trail**: Track all transitions with timestamps and comments

## Domain Model

### Aggregates

```
WorkflowDefinition (Aggregate Root)
├── WorkflowStage[] - States in the workflow
└── WorkflowTransition[] - Valid transitions between states

WorkflowInstance (Aggregate Root)
├── Current state and assignment
├── SLA tracking
└── WorkflowTransitionLog[] - Audit trail
```

### WorkflowDefinition

Defines the workflow configuration for a loan type:

| Property | Description |
|----------|-------------|
| Name | Workflow name |
| ApplicationType | Corporate or Retail |
| Stages | List of workflow stages |
| Transitions | Valid transitions between stages |
| IsActive | Whether this definition is active |
| Version | Version number for tracking changes |

### WorkflowStage

Represents a stage in the workflow:

| Property | Description |
|----------|-------------|
| Status | Maps to LoanApplicationStatus |
| DisplayName | User-friendly name |
| AssignedRole | Role responsible for this stage |
| SLAHours | SLA deadline in hours |
| RequiresComment | Whether comment is mandatory |
| IsTerminal | Whether this is a final state |

### WorkflowTransition

Defines a valid transition:

| Property | Description |
|----------|-------------|
| FromStatus | Source status |
| ToStatus | Target status |
| Action | Workflow action (Approve, Reject, etc.) |
| RequiredRole | Role authorized for this transition |
| RequiresComment | Whether comment is mandatory |

### WorkflowInstance

Tracks workflow state for a loan application:

| Property | Description |
|----------|-------------|
| LoanApplicationId | Associated loan application |
| CurrentStatus | Current workflow state |
| AssignedRole | Role responsible for current stage |
| AssignedToUserId | Specific user assigned (optional) |
| SLADueAt | When SLA expires |
| IsSLABreached | Whether SLA has been breached |
| EscalationLevel | Current escalation level |
| TransitionHistory | Audit trail of all transitions |

## Corporate Loan Workflow

The default corporate loan workflow:

```
                    ┌──────────────────────────────────────────────────────────────────────────┐
                    │                                                                          │
                    ▼                                                                          │
┌───────┐    ┌───────────┐    ┌────────────────┐    ┌──────────────┐                          │
│ Draft │───►│ Submitted │───►│ Data Gathering │───►│ Branch Review│                          │
└───────┘    └───────────┘    └────────────────┘    └──────────────┘                          │
                                                           │                                   │
                         ┌─────────────────────────────────┼─────────────────────┐             │
                         │                                 │                     │             │
                         ▼                                 ▼                     ▼             │
               ┌─────────────────┐              ┌─────────────────┐    ┌──────────────┐       │
               │ Branch Returned │◄─────────────│ Branch Approved │    │Branch Rejected│       │
               └─────────────────┘              └─────────────────┘    └──────────────┘       │
                         │                                 │                                   │
                         └─────────────────────────────────┼───────────────────────────────────┘
                                                           │
                                                           ▼
                                               ┌─────────────────────┐
                                               │  Credit Analysis    │
                                               │ (Auto credit checks)│
                                               └─────────────────────┘
                                                           │
                                                           ▼
                                               ┌─────────────────────┐
                                               │     HO Review       │
                                               └─────────────────────┘
                                                           │
                         ┌─────────────────────────────────┼─────────────────────┐
                         │                                 │                     │
                         ▼                                 ▼                     ▼
               ┌─────────────────┐              ┌─────────────────────┐    ┌──────────┐
               │  Branch Review  │◄─────────────│Committee Circulation│    │ Rejected │
               │    (Return)     │              └─────────────────────┘    └──────────┘
               └─────────────────┘                         │
                                                           │
                                    ┌──────────────────────┼──────────────────────┐
                                    │                                             │
                                    ▼                                             ▼
                         ┌─────────────────────┐                      ┌────────────────────┐
                         │ Committee Approved  │                      │ Committee Rejected │
                         └─────────────────────┘                      └────────────────────┘
                                    │
                                    ▼
                         ┌─────────────────────┐
                         │   Final Approved    │
                         └─────────────────────┘
                                    │
                                    ▼
                         ┌─────────────────────┐
                         │   Offer Generated   │
                         └─────────────────────┘
                                    │
                                    ▼
                         ┌─────────────────────┐
                         │   Offer Accepted    │
                         └─────────────────────┘
                                    │
                                    ▼
                         ┌─────────────────────┐
                         │     Disbursed       │
                         └─────────────────────┘
```

## Workflow Actions

| Action | Description |
|--------|-------------|
| Submit | Submit for next stage review |
| Approve | Approve and move forward |
| Reject | Reject the application |
| Return | Return for corrections |
| Assign | Assign to specific user |
| Unassign | Remove user assignment |
| Escalate | Escalate to higher level |
| MoveToNextStage | System transition |
| Complete | Mark workflow complete |

## SLA Configuration

| Stage | Assigned Role | SLA (Hours) |
|-------|---------------|-------------|
| Draft | LoanOfficer | No SLA |
| Submitted | LoanOfficer | 4 |
| Data Gathering | LoanOfficer | 24 |
| Branch Review | BranchApprover | 8 |
| Credit Analysis | System | 48 |
| HO Review | CreditOfficer | 24 |
| Committee Circulation | CommitteeMember | 72 |
| Committee Approved | FinalApprover | 8 |
| Approved | Operations | 4 |
| Offer Generated | Operations | 24 |
| Offer Accepted | Operations | 48 |

## API Endpoints

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | /api/Workflow/{id} | Get workflow by ID | Any |
| GET | /api/Workflow/by-loan-application/{id} | Get workflow by loan app | Any |
| GET | /api/Workflow/{id}/available-actions | Get available actions | Any |
| POST | /api/Workflow/{id}/transition | Execute transition | By Role |
| POST | /api/Workflow/{id}/assign | Assign to user | Any |
| POST | /api/Workflow/{id}/unassign | Remove assignment | Any |
| POST | /api/Workflow/{id}/escalate | Escalate workflow | Any |
| GET | /api/Workflow/queue/{role} | Get queue by role | Any |
| GET | /api/Workflow/my-queue | Get user's queue | Any |
| GET | /api/Workflow/overdue | Get overdue items | Manager |
| GET | /api/Workflow/queue-summary | Get queue summary | Manager |
| GET | /api/Workflow/definition/{type} | Get workflow definition | Any |
| POST | /api/Workflow/seed-corporate-workflow | Seed default workflow | Admin |

## Queue Management

### Role-Based Queues

Each role sees workflows assigned to them:

```json
GET /api/Workflow/queue/BranchApprover
[
  {
    "id": "...",
    "applicationNumber": "LA20260217ABC123",
    "customerName": "Acme Corp",
    "currentStatus": "BranchReview",
    "assignedRole": "BranchApprover",
    "slaDeue": "2026-02-17T18:00:00Z",
    "isSLABreached": false,
    "isOverdue": false
  }
]
```

### User Assignment

Workflows can be assigned to specific users within a role:

```json
POST /api/Workflow/{id}/assign
{
  "assignToUserId": "..."
}
```

### Queue Summary (Manager View)

```json
GET /api/Workflow/queue-summary
[
  {
    "role": "BranchApprover",
    "totalCount": 15,
    "overdueCount": 2,
    "assignedCount": 10,
    "unassignedCount": 5
  }
]
```

## Files

### Domain
- `Aggregates/Workflow/WorkflowDefinition.cs` - Workflow configuration
- `Aggregates/Workflow/WorkflowStage.cs` - Stage definition
- `Aggregates/Workflow/WorkflowTransition.cs` - Transition definition
- `Aggregates/Workflow/WorkflowInstance.cs` - Runtime workflow state
- `Aggregates/Workflow/WorkflowTransitionLog.cs` - Audit record
- `Enums/WorkflowEnums.cs` - WorkflowAction, EscalationLevel
- `Interfaces/IWorkflowRepository.cs` - Repository interfaces
- `Services/WorkflowService.cs` - Domain service

### Application
- `Workflow/Commands/WorkflowCommands.cs` - All commands
- `Workflow/Queries/WorkflowQueries.cs` - All queries
- `Workflow/DTOs/WorkflowDtos.cs` - DTOs

### Infrastructure
- `Persistence/Configurations/Workflow/WorkflowConfigurations.cs` - EF mappings
- `Persistence/Repositories/WorkflowRepositories.cs` - Repository implementations

### API
- `Controllers/WorkflowController.cs` - REST endpoints

## Usage Example

### Initialize Workflow (on loan creation)

```csharp
var result = await _workflowService.InitializeWorkflowAsync(
    loanApplicationId,
    LoanApplicationType.Corporate,
    LoanApplicationStatus.Draft,
    initiatedByUserId);
```

### Execute Transition

```csharp
var result = await _workflowService.TransitionAsync(
    workflowInstanceId,
    LoanApplicationStatus.BranchApproved,
    WorkflowAction.Approve,
    userId,
    "BranchApprover",
    "Approved - all documents verified");
```

### Get Available Actions

```csharp
var actions = await _workflowService.GetAvailableActionsAsync(
    workflowInstanceId,
    "BranchApprover");
// Returns: [Approve, Return, Reject]
```

## Integration Points

- **LoanApplication**: Workflow status mirrors loan status
- **CreditBureauIntegration**: Credit checks trigger auto-transition from BranchApproved to CreditAnalysis
- **AIAdvisoryEngine**: Advisory generation may trigger workflow progression
- **NotificationService**: Workflow transitions trigger notifications
- **AuditService**: All transitions logged for compliance

## Future Enhancements

1. **Parallel Approvals**: Support multiple approvers at same stage
2. **Conditional Routing**: Route based on loan amount or risk level
3. **Auto-Escalation**: Automatic escalation on SLA breach
4. **Delegation**: Allow users to delegate work during absence
5. **Workflow Versioning**: Track changes to workflow definitions
6. **Business Rules Engine**: Dynamic transition rules
