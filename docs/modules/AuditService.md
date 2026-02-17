# AuditService Module

## Overview

The AuditService module provides immutable audit logging for compliance with Nigerian banking regulations. It tracks all significant actions in the system, including data access to sensitive information like BVN and credit reports.

## Key Features

- **Immutable Audit Logs**: All actions recorded permanently
- **Sensitive Data Access Tracking**: Separate log for BVN, credit reports, etc.
- **Comprehensive Search**: Filter by category, action, user, entity, date range
- **Loan Application Timeline**: Complete audit trail per application
- **Failed Action Tracking**: Monitor and investigate failures
- **Compliance Ready**: Designed for Nigerian banking audit requirements
- **Event-Driven Integration**: Automatically receives domain events from other modules

## Domain Event Integration

The AuditService is a **consumer of domain events** from other bounded contexts. This provides loose coupling while ensuring all critical actions are audited.

### Event Handlers

| Event | Source Module | Audit Action Created |
|-------|---------------|---------------------|
| `WorkflowTransitionedEvent` | WorkflowEngine | StatusChange in Workflow category |
| `CommitteeVoteCastEvent` | CommitteeWorkflow | Vote in Committee category |
| `CommitteeDecisionRecordedEvent` | CommitteeWorkflow | Decision in Committee category |
| `ScoringParameterChangeApprovedEvent` | Configuration | ConfigApprove in Configuration category |
| `LoanApplicationCreatedEvent` | CorporateLoanInitiation | Create in LoanApplication category |
| `LoanApplicationApprovedEvent` | CorporateLoanInitiation | Approve in LoanApplication category |

### How It Works

```
1. Other module's aggregate raises domain event
2. SaveChangesAsync() commits the transaction
3. DomainEventPublishingInterceptor dispatches events
4. AuditEventHandler receives event
5. Handler calls AuditService.LogAsync() to create audit record
```

This ensures audit logs are created automatically without tight coupling between modules.

## Domain Model

### AuditLog

Records all significant actions in the system:

| Property | Description |
|----------|-------------|
| Action | Type of action (Create, Update, Approve, etc.) |
| Category | Category (LoanApplication, CreditBureau, etc.) |
| Description | Human-readable description |
| UserId/UserName/UserRole | Who performed the action |
| IpAddress | Client IP address |
| EntityType/EntityId | What was affected |
| LoanApplicationId | Associated loan (if applicable) |
| OldValues/NewValues | JSON of before/after values |
| AdditionalData | Extra context as JSON |
| IsSuccess | Whether action succeeded |
| ErrorMessage | Error details if failed |
| Timestamp | When action occurred |

### DataAccessLog

Tracks access to sensitive data:

| Property | Description |
|----------|-------------|
| UserId/UserName/UserRole | Who accessed |
| DataType | Type of sensitive data (BVN, CreditReport, etc.) |
| EntityType/EntityId | What was accessed |
| AccessType | View, Download, Export, Print, Share |
| AccessReason | Why they accessed (optional) |
| LoanApplicationId | Associated loan |
| AccessedAt | When accessed |

## Audit Actions

### CRUD Operations
- Create, Read, Update, Delete

### Loan Application
- Submit, Approve, Reject, Return, Escalate, StatusChange

### Workflow
- Assign, Unassign

### Committee
- Vote, Comment, Decision

### Credit Bureau
- CreditCheck, BureauRequest, BureauResponse

### Financial Analysis
- StatementUpload, StatementAnalysis, RatioCalculation

### Advisory
- AdvisoryGenerated, ScoreCalculated

### Configuration (Maker-Checker)
- ConfigChange, ConfigApprove, ConfigReject

### Security
- Login, Logout, LoginFailed, PasswordChange, PasswordReset, RoleChange

### Documents
- DocumentUpload, DocumentDownload, DocumentDelete, DocumentVerify

### Integration
- ExternalApiCall, ExternalApiResponse

### Other
- Override, Export, Print

## Audit Categories

| Category | Description |
|----------|-------------|
| Authentication | Login/logout events |
| Authorization | Permission changes |
| LoanApplication | Loan lifecycle events |
| CreditBureau | Bureau checks |
| FinancialAnalysis | Financial statement analysis |
| StatementAnalysis | Bank statement analysis |
| Collateral | Collateral management |
| Guarantor | Guarantor management |
| Workflow | Workflow transitions |
| Committee | Committee actions |
| Advisory | AI advisory events |
| Configuration | System configuration changes |
| Document | Document operations |
| Integration | External API calls |
| Security | Security events |
| DataAccess | Sensitive data access |
| System | System events |

## Sensitive Data Types

| Type | Examples |
|------|----------|
| BVN | Bank Verification Number |
| NIN | National Identity Number |
| CreditReport | Bureau reports |
| BankStatement | Account statements |
| FinancialStatement | Audited financials |
| PersonalInformation | Name, DOB, address |
| ContactInformation | Phone, email |
| EmploymentInformation | Employer, salary |
| IncomeInformation | Income details |
| CollateralDetails | Asset information |
| GuarantorDetails | Guarantor information |

## API Endpoints

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | /api/Audit/{id} | Get log by ID | Compliance+ |
| GET | /api/Audit/by-loan-application/{id} | Logs for loan app | Compliance+ |
| GET | /api/Audit/by-entity/{type}/{id} | Logs for entity | Compliance+ |
| GET | /api/Audit/by-user/{userId} | Logs for user | Compliance+ |
| GET | /api/Audit/recent | Recent logs | Compliance+ |
| GET | /api/Audit/failed | Failed actions | Compliance+ |
| GET | /api/Audit/search | Search with filters | Compliance+ |
| GET | /api/Audit/data-access/by-user/{id} | Data access by user | Compliance+ |
| GET | /api/Audit/data-access/by-loan-application/{id} | Data access by loan | Compliance+ |

**Authorization**: ComplianceOfficer, RiskManager, SystemAdministrator

## Usage

### AuditService Domain Service

```csharp
// Log status change
await _auditService.LogStatusChangeAsync(
    loanApplicationId,
    applicationNumber,
    "BranchReview",
    "BranchApproved",
    userId,
    userName,
    userRole,
    "Approved - all documents verified");

// Log credit bureau request
await _auditService.LogCreditBureauRequestAsync(
    bureauReportId,
    "FirstCentral",
    "Director",
    "John Smith",
    loanApplicationId,
    applicationNumber,
    userId,
    userName);

// Log committee vote
await _auditService.LogCommitteeVoteAsync(
    committeeReviewId,
    loanApplicationId,
    applicationNumber,
    "Approve",
    userId,
    userName,
    userRole,
    "Strong financials, recommend approval");

// Log sensitive data access
await _auditService.LogDataAccessAsync(
    userId,
    userName,
    userRole,
    SensitiveDataType.CreditReport,
    "BureauReport",
    bureauReportId,
    DataAccessType.View,
    subjectBVN,
    loanApplicationId,
    applicationNumber,
    "Reviewing credit history for loan assessment");

// Log login
await _auditService.LogLoginAsync(
    userId,
    userName,
    email,
    isSuccess: true,
    ipAddress: clientIp,
    userAgent: userAgent);
```

### Search API

```
GET /api/Audit/search?category=LoanApplication&from=2026-01-01&to=2026-02-17&pageNumber=1&pageSize=50
```

Response:
```json
{
  "items": [
    {
      "id": "...",
      "action": "StatusChange",
      "category": "LoanApplication",
      "description": "Loan application status changed from BranchReview to BranchApproved",
      "userName": "John Smith",
      "entityType": "LoanApplication",
      "entityReference": "LA20260217ABC123",
      "isSuccess": true,
      "timestamp": "2026-02-17T10:30:00Z"
    }
  ],
  "totalCount": 150,
  "pageNumber": 1,
  "pageSize": 50,
  "totalPages": 3
}
```

## Compliance Features

### Nigerian Banking Requirements

1. **Data Protection**: All access to personal data logged
2. **Audit Trail**: Complete history of all loan decisions
3. **Non-Repudiation**: User, IP, timestamp for every action
4. **Immutability**: Audit logs cannot be modified or deleted
5. **Retention**: Designed for long-term storage
6. **Access Control**: Only compliance/admin can view audit logs

### Integration with Other Modules

The AuditService should be called from other modules:

- **LoanApplication**: Status changes, document uploads
- **CreditBureau**: Bureau requests and responses
- **Committee**: Votes, comments, decisions
- **Workflow**: Transitions, assignments
- **Configuration**: Parameter changes (maker-checker)
- **Identity**: Login/logout, password changes

## Files

### Domain
- `Aggregates/Audit/AuditLog.cs` - Main audit log entity
- `Aggregates/Audit/DataAccessLog.cs` - Sensitive data access log
- `Enums/AuditEnums.cs` - AuditAction, AuditCategory, SensitiveDataType, DataAccessType
- `Interfaces/IAuditRepository.cs` - Repository interfaces
- `Services/AuditService.cs` - Domain service with logging helpers

### Application
- `Audit/Queries/AuditQueries.cs` - All query handlers
- `Audit/DTOs/AuditDtos.cs` - DTOs

### Infrastructure
- `Persistence/Configurations/Audit/AuditConfigurations.cs` - EF mappings
- `Persistence/Repositories/AuditRepositories.cs` - Repository implementations

### API
- `Controllers/AuditController.cs` - REST endpoints

## Database Schema

### AuditLogs Table
- Indexed on: Timestamp, Category, Action, UserId, LoanApplicationId, (EntityType, EntityId)
- OldValues, NewValues, AdditionalData stored as JSON

### DataAccessLogs Table
- Indexed on: UserId, DataType, AccessedAt, LoanApplicationId, (EntityType, EntityId)

## Future Enhancements

1. **Audit Report Generation**: PDF/Excel export of audit trails
2. **Real-time Alerts**: Notify on suspicious activity patterns
3. **Retention Policies**: Automatic archival of old logs
4. **Anonymization**: Mask PII in exported reports
5. **Compliance Reports**: Pre-built reports for regulators
6. **Audit Log Integrity**: Hash chain for tamper detection
