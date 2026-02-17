# CRMS - Implementation Tracker

**Version:** 2.0  
**Last Updated:** 2026-02-17  
**Status:** Implementation Phase (15/18 modules complete - 83%)

---

## 1. Architecture Overview

### 1.1 Design Pattern: Domain-Driven Design (DDD)

This system follows **Domain-Driven Design** principles with **Clean Architecture** layering.

#### Core DDD Concepts Applied

| Concept | Application in CRMS |
|---------|---------------------|
| **Ubiquitous Language** | All code, documentation, and communication use consistent domain terms (see Section 2) |
| **Bounded Contexts** | Lending, CreditAssessment, Workflow, CoreBanking, Notifications |
| **Aggregates** | LoanApplication, CorporateProfile, DecisionResult, WorkflowCase |
| **Entities** | Director, Signatory, BureauReport, FinancialStatement |
| **Value Objects** | Money, InterestRate, BVN, AccountNumber, Score |
| **Domain Events** | LoanApplicationCreated, WorkflowTransitioned, CommitteeVoteCast, CommitteeDecisionRecorded, etc. |
| **Event-Driven Communication** | Critical cross-context flows use domain events (Workflow→Audit, Committee→Audit, Config→Audit) |
| **Domain Services** | CreditAssessmentService, DecisionService, DisbursementService |
| **Repositories** | ILoanApplicationRepository, ICorporateProfileRepository, etc. |

#### Layer Responsibilities

```
┌─────────────────────────────────────────────────────────────┐
│                    PRESENTATION LAYER                        │
│  (Blazor Web, API Controllers)                              │
│  - UI components, view models, API endpoints                │
│  - Input validation, response formatting                    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    APPLICATION LAYER                         │
│  (Use Cases, Application Services, DTOs)                    │
│  - **BUSINESS LOGIC ORCHESTRATION**                         │
│  - Use case implementation                                  │
│  - Transaction management                                   │
│  - DTO mapping                                              │
│  - Cross-cutting concerns (logging, validation)            │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      DOMAIN LAYER                            │
│  (Entities, Value Objects, Domain Services, Interfaces)     │
│  - **CORE BUSINESS RULES**                                  │
│  - Entity behavior and invariants                           │
│  - Domain events                                            │
│  - Repository interfaces (not implementations)              │
│  - No dependencies on other layers                          │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  INFRASTRUCTURE LAYER                        │
│  (Persistence, External Services, Framework Concerns)       │
│  - EF Core DbContext and configurations                     │
│  - Repository implementations                               │
│  - External API clients (Fineract, Bureaus, LLM)           │
│  - Message queue implementations                            │
│  - File storage implementations                             │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 Solution Structure

```
CRMS/
├── src/
│   ├── CRMS.Domain/                 # Domain Layer
│   │   ├── Aggregates/
│   │   │   ├── LoanApplication/
│   │   │   ├── CorporateProfile/
│   │   │   ├── Decision/
│   │   │   └── Workflow/
│   │   ├── Entities/
│   │   ├── ValueObjects/
│   │   ├── Events/
│   │   ├── Services/
│   │   ├── Interfaces/
│   │   └── Exceptions/
│   │
│   ├── CRMS.Application/            # Application Layer
│   │   ├── UseCases/
│   │   │   ├── CorporateLoan/
│   │   │   ├── RetailLoan/
│   │   │   ├── CreditAssessment/
│   │   │   ├── Workflow/
│   │   │   └── Reporting/
│   │   ├── DTOs/
│   │   ├── Interfaces/
│   │   ├── Validators/
│   │   └── Mappings/
│   │
│   ├── CRMS.Infrastructure/         # Infrastructure Layer
│   │   ├── Persistence/
│   │   │   ├── DbContext/
│   │   │   ├── Configurations/
│   │   │   ├── Repositories/
│   │   │   └── Migrations/
│   │   ├── ExternalServices/
│   │   │   ├── Fineract/
│   │   │   ├── CreditBureau/
│   │   │   ├── StatementParsing/
│   │   │   ├── AIServices/
│   │   │   └── Notifications/
│   │   ├── FileStorage/
│   │   └── Messaging/
│   │
│   ├── CRMS.API/                    # API Layer (required, between UI and Application)
│   │   └── Controllers/
│   │
│   ├── CRMS.Web.Intranet/           # Intranet Portal (Corporate Loans - Staff)
│   │   ├── Components/
│   │   └── wwwroot/
│   │
│   └── CRMS.Web.Portal/             # Internet Portal (Retail Loans - Customers)
│       ├── Components/
│       └── wwwroot/
│
├── tests/
│   ├── CRMS.Domain.Tests/
│   ├── CRMS.Application.Tests/
│   ├── CRMS.Infrastructure.Tests/
│   └── CRMS.Integration.Tests/
│
└── docs/
    ├── FullDesign.md
    ├── ImplementationTracker.md
    └── modules/
        └── [ModuleName].md
```

### 1.3 Technology Stack

| Layer | Technology |
|-------|------------|
| Language | C# (.NET 9) |
| UI Framework | Blazor Server |
| ORM | Entity Framework Core |
| Database | MySQL (Pomelo.EntityFrameworkCore.MySql) |
| Caching | Redis (optional) |
| Message Queue | RabbitMQ or Azure Service Bus |
| File Storage | S3-compatible (MinIO for dev, AWS S3/Azure Blob for prod) |
| PDF Generation | QuestPDF or iText7 |
| AI/LLM | OpenAI API / Azure OpenAI |
| Authentication | ASP.NET Core Identity + JWT |
| Logging | Serilog with structured logging |
| Testing | xUnit, Moq, FluentAssertions |

---

## 2. Ubiquitous Language Glossary

This glossary defines the **official terms** used throughout the codebase, documentation, and team communication. All developers must use these terms consistently.

### Loan & Application Terms

| Term | Definition |
|------|------------|
| **LoanApplication** | A request for credit submitted by a customer (retail) or initiated by a loan officer (corporate) |
| **LoanProduct** | A configured loan offering with defined terms (amount range, tenor, pricing, eligibility rules) |
| **Facility** | The approved credit amount and terms granted to a borrower |
| **Tenor** | The duration of the loan in months |
| **Principal** | The original loan amount before interest |
| **Disbursement** | The act of releasing approved funds to the borrower's account |
| **Booking** | Recording the loan in the core banking system |

### Corporate Lending Terms

| Term | Definition |
|------|------------|
| **CorporateProfile** | The business entity applying for credit, including registration details |
| **Director** | An individual listed as a director of the corporate entity |
| **Signatory** | An individual authorized to sign on the corporate bank account |
| **AuditedFinancials** | Verified financial statements (Balance Sheet, Income Statement, Cash Flow) for past years |
| **LoanPack** | The complete PDF document containing all loan analysis, bureau reports, and approval history |

### Credit Assessment Terms

| Term | Definition |
|------|------------|
| **BureauCheck** | A credit history inquiry against a credit bureau |
| **BureauReport** | The response from a credit bureau containing credit history and scores |
| **BVN** | Bank Verification Number - Nigeria's biometric identification for banking |
| **ScoreMatrix** | The AI-generated assessment combining multiple risk factors |
| **DSCR** | Debt Service Coverage Ratio - ability to service debt from income/cashflow |
| **DTI** | Debt-to-Income ratio |
| **Delinquency** | A past-due payment or default on a credit obligation |

### Decision Terms

| Term | Definition |
|------|------------|
| **Decision** | The outcome of credit assessment: Approve, Decline, or Refer |
| **Approve** | Loan meets all criteria and is granted |
| **Decline** | Loan does not meet criteria and is rejected |
| **Refer** | Loan requires manual review before decision |
| **Override** | A manual change to the system's recommended decision (requires justification) |
| **ReasonCode** | A standardized code explaining a decision factor |

### Workflow Terms

| Term | Definition |
|------|------------|
| **WorkflowDefinition** | Configuration defining valid stages, transitions, and SLAs for a loan type |
| **WorkflowStage** | A state in the workflow with assigned role and SLA |
| **WorkflowTransition** | A valid movement between stages with required role and action |
| **WorkflowInstance** | Runtime tracking of a loan application's workflow state |
| **WorkflowAction** | An action that triggers transition (Submit, Approve, Reject, Return, Escalate) |
| **BranchApproval** | First-level approval by the branch loan approving officer |
| **HOReview** | Head Office review stage for corporate loans |
| **CommitteeCirculation** | Stage where committee members review and comment |
| **FinalApproval** | The ultimate approval authority's decision |
| **SLA** | Service Level Agreement - maximum time allowed in a workflow stage |
| **Escalation** | Raising a workflow item to higher authority due to SLA breach or complexity |

### System Actors

| Term | Definition |
|------|------------|
| **Applicant** | A customer applying for a retail loan |
| **LoanOfficer** | Bank staff who initiates and manages corporate loan applications |
| **CreditOfficer** | Staff who reviews referred applications and makes credit decisions |
| **RiskManager** | Senior staff with override authority |
| **BranchApprover** | Branch-level authority for corporate loan progression |
| **CommitteeMember** | Head office staff participating in committee review |
| **FinalApprover** | Designated authority for final loan approval |

---

## 3. Module Implementation Status

### Status Legend
- 🔴 Not Started
- 🟡 In Progress
- 🟢 Completed
- ⏸️ On Hold

### Module Tracker

| # | Module | Status | Priority | Documentation | Dependencies |
|---|--------|--------|----------|---------------|--------------|
| 1 | **ProductCatalog** | 🟢 | P1 | [ProductCatalog.md](modules/ProductCatalog.md) | None |
| 2 | **IdentityService** | 🟢 | P1 | [IdentityService.md](modules/IdentityService.md) | None |
| 3 | **CoreBankingAdapter** | 🟢 | P1 | [CoreBankingAdapter.md](modules/CoreBankingAdapter.md) | None |
| 4 | **CorporateLoanInitiation** | 🟢 | P1 | [CorporateLoanInitiation.md](modules/CorporateLoanInitiation.md) | ProductCatalog, CoreBankingAdapter |
| 5 | **CreditBureauIntegration** | 🟢 | P1 | [CreditBureauIntegration.md](modules/CreditBureauIntegration.md) | None |
| 6 | **StatementAnalyzer** | 🟢 | P1 | [StatementAnalyzer.md](modules/StatementAnalyzer.md) | None |
| 7 | **CollateralManagement** | 🟢 | P1 | [CollateralManagement.md](modules/CollateralManagement.md) | None |
| 8 | **GuarantorManagement** | 🟢 | P1 | [GuarantorManagement.md](modules/GuarantorManagement.md) | CreditBureauIntegration |
| 9 | **FinancialDocumentAnalyzer** | 🟢 | P1 | [FinancialDocumentAnalyzer.md](modules/FinancialDocumentAnalyzer.md) | None |
| 10 | **AIAdvisoryEngine** | 🟢 | P1 | [AIAdvisoryEngine.md](modules/AIAdvisoryEngine.md) | CreditBureauIntegration, StatementAnalyzer, FinancialDocumentAnalyzer |
| 11 | **WorkflowEngine** | 🟢 | P1 | [WorkflowEngine.md](modules/WorkflowEngine.md) | None |
| 12 | **CommitteeWorkflow** | 🟢 | P2 | [CommitteeWorkflow.md](modules/CommitteeWorkflow.md) | WorkflowEngine |
| 13 | **LoanPackGenerator** | 🟢 | P2 | [LoanPackGenerator.md](modules/LoanPackGenerator.md) | AIAdvisoryEngine, WorkflowEngine |
| 14 | **NotificationService** | 🟢 | P2 | [NotificationService.md](modules/NotificationService.md) | None |
| 15 | **AuditService** | 🟢 | P1 | [AuditService.md](modules/AuditService.md) | None |
| 16 | **ReportingService** | 🔴 | P3 | [ReportingService.md](modules/ReportingService.md) | All modules |
| 17 | **CustomerPortal** | 🔴 | P3 | [CustomerPortal.md](modules/CustomerPortal.md) | ProductCatalog, CreditBureauIntegration, StatementAnalyzer |
| 18 | **DecisionEngine** | 🔴 | P3 | [DecisionEngine.md](modules/DecisionEngine.md) | CreditBureauIntegration, StatementAnalyzer |

---

## 4. Module Summaries

### 4.1 ProductCatalog
**Purpose:** Manage loan products, eligibility rules, and pricing configurations.

**Key Responsibilities:**
- CRUD operations for loan products
- Eligibility rule configuration
- Interest rate and fee structures
- Required document definitions per product

**Domain Entities:** LoanProduct, EligibilityRule, PricingTier, DocumentRequirement

**Bounded Context:** Lending

---

### 4.2 IdentityService
**Purpose:** Handle authentication, authorization, and user management.

**Key Responsibilities:**
- User registration and authentication
- Role-based access control (RBAC)
- JWT token management
- Multi-factor authentication (optional)

**Domain Entities:** User, Role, Permission

**Bounded Context:** Identity

---

### 4.3 CoreBankingAdapter
**Purpose:** Integration layer for Fineract core banking system.

**Key Responsibilities:**
- Customer lookup and creation
- Account information retrieval
- Loan booking and disbursement
- Repayment schedule synchronization
- Statement retrieval

**Integration Pattern:** Anti-corruption layer with adapter pattern

**External API:** Fineract REST API

**Bounded Context:** CoreBanking

---

### 4.4 CorporateLoanInitiation
**Purpose:** Officer-driven corporate loan application intake and management.

**Key Responsibilities:**
- Loan application creation with corporate account details
- Automatic data pull from core banking
- Director and signatory information capture
- Document upload management (statements, audited accounts)
- Submission for branch approval
- **Automatic credit check triggering** after branch approval (via background service)
- Credit check progress tracking

**Domain Entities:** LoanApplication, CorporateProfile, Director, Signatory

**Bounded Context:** Lending

**Integration:** CreditBureauIntegration (automatic trigger on BranchApproved)

---

### 4.5 CreditBureauIntegration
**Purpose:** Orchestrate credit checks against Nigerian credit bureaus.

**Key Responsibilities:**
- Consent verification before checks
- Multi-bureau support (FirstCentral, CreditRegistry, Mono)
- Normalized report generation
- Raw response encryption and storage
- Retry and fallback handling

**Integration Pattern:** Pluggable providers via ICreditBureauProvider interface

**Bounded Context:** CreditAssessment

---

### 4.6 StatementAnalyzer
**Purpose:** Parse bank statements and extract cashflow analytics.

**Key Responsibilities:**
- PDF/CSV statement parsing
- Transaction categorization
- Cashflow feature extraction (inflows, outflows, volatility)
- Salary detection
- Recurring obligation identification

**Domain Entities:** Statement, Transaction, CashflowSummary

**Bounded Context:** CreditAssessment

---

### 4.7 FinancialDocumentAnalyzer
**Purpose:** Parse audited financial statements and calculate financial ratios.

**Key Responsibilities:**
- OCR and table extraction from financial documents
- Balance sheet parsing
- Income statement parsing
- Ratio calculation (Current Ratio, Debt-Equity, Margins, EBITDA)

**Domain Entities:** FinancialStatement, BalanceSheet, IncomeStatement, FinancialRatios

**Bounded Context:** CreditAssessment

---

### 4.8 AIAdvisoryEngine
**Purpose:** Generate AI-powered decision support for corporate loans.

**Key Responsibilities:**
- Aggregate all assessment data (bureau, cashflow, financials, collateral, guarantors)
- Generate score matrix with 8 risk categories
- Produce recommendations (amount, tenor, interest tier)
- Generate risk commentary and red flags
- Create exportable decision support report
- **UI-manageable scoring configuration** with maker-checker workflow

**Scoring Configuration:**
- All scoring parameters stored in database (not config files)
- System Administrator role only can modify
- Maker-checker approval required for changes
- Full audit history for compliance

**AI Integration:** OpenAI/Azure OpenAI for analysis and commentary (MockAIAdvisoryService for development)

**Bounded Context:** CreditAssessment

---

### 4.9 WorkflowEngine
**Purpose:** Manage loan application state transitions and approval routing.

**Key Responsibilities:**
- **Configurable workflow definitions** per loan type (Corporate/Retail)
- **State machine** with 17 stages from Draft to Disbursed
- **Role-based queue management** (LoanOfficer, BranchApprover, CreditOfficer, CommitteeMember, FinalApprover, Operations)
- **SLA tracking** with configurable hours per stage and breach detection
- **User assignment** within role queues
- **Escalation support** with multi-level escalation
- **Full audit trail** of all transitions with timestamps and comments

**Domain Entities:** WorkflowDefinition, WorkflowStage, WorkflowTransition, WorkflowInstance, WorkflowTransitionLog

**Key Features:**
- Workflow definitions seeded via API (`POST /api/Workflow/seed-corporate-workflow`)
- Available actions dynamically determined by current state and user role
- Queue views by role and by user assignment
- Overdue workflow monitoring for managers
- Integrates with LoanApplication status

**Bounded Context:** Workflow

---

### 4.10 CommitteeWorkflow
**Purpose:** Multi-user committee approval process for corporate loans.

**Key Responsibilities:**
- **Multi-committee support** (Branch, Regional, HO, Management, Board)
- **Member management** with role assignments and chairperson designation
- **Voting system** with Approve/Reject/Abstain and quorum tracking
- **Comment threads** with visibility controls (Committee/Internal/Applicant)
- **Document attachments** with visibility controls
- **Decision recording** with approved terms and conditions
- **Deadline tracking** with overdue monitoring

**Domain Entities:** CommitteeReview, CommitteeMember, CommitteeComment, CommitteeDocument

**Key Features:**
- Configurable required votes and minimum approval threshold
- Chairperson can record final decision
- Voting summary with quorum and majority tracking
- Integration with WorkflowEngine for status transitions

**Bounded Context:** Workflow

---

### 4.11 LoanPackGenerator
**Purpose:** Generate comprehensive PDF loan packs for corporate loan committee review.

**Key Responsibilities:**
- **PDF Generation** using QuestPDF (free community license)
- **11 sections**: Executive Summary, Customer Profile, Directors/Signatories, Bureau Reports, Financial Analysis, Cashflow, Collateral, Guarantors, AI Advisory, Workflow History, Committee Comments
- **Version control** - auto-increment versions, archive old packs
- **Content hashing** - SHA256 for document integrity
- **Section tracking** - flags indicating which sections are included

**Domain Entities:** LoanPack (with versioning and content summary)

**Key Features:**
- Parallel data loading from all repositories for performance
- Professional layout with colored risk indicators
- Red flags and mitigating factors highlighted
- Key metrics summary boxes
- Configurable section inclusion based on available data

**Technology:** QuestPDF

**Bounded Context:** Reporting

---

### 4.12 NotificationService
**Purpose:** Send notifications via multiple channels.

**Key Responsibilities:**
- Email notifications (SMTP/SendGrid)
- SMS notifications (Africa's Talking/Termii)
- WhatsApp Business API (optional)
- Notification templates management
- Delivery tracking

**Bounded Context:** Notifications

---

### 4.13 AuditService
**Purpose:** Maintain immutable audit logs for compliance with Nigerian banking regulations.

**Key Responsibilities:**
- **Immutable audit logs** for all significant actions
- **Sensitive data access tracking** (BVN, credit reports, financials)
- **Comprehensive search** by category, action, user, entity, date range
- **Loan application timeline** - complete audit trail per application
- **Failed action monitoring** for investigation
- **Compliance-ready** design for regulatory requirements

**Domain Entities:** AuditLog, DataAccessLog

**Key Features:**
- 30+ audit actions across 17 categories
- 11 sensitive data types tracked
- JSON storage for old/new values and additional context
- AuditService domain service with helper methods for common logging scenarios
- Restricted API access (ComplianceOfficer, RiskManager, SystemAdministrator)

**Bounded Context:** Audit

---

### 4.14 ReportingService
**Purpose:** Dashboards and analytics for business insights.

**Key Responsibilities:**
- Funnel metrics (submitted → approved → disbursed)
- Portfolio analytics
- Decision distribution reports
- Performance dashboards

**Bounded Context:** Reporting

---

### 4.15 CustomerPortal
**Purpose:** Self-service web portal for retail loan applications.

**Key Responsibilities:**
- Multi-step application form
- Consent capture
- Statement upload/connection
- Application status tracking

**Bounded Context:** Lending

---

### 4.16 DecisionEngine
**Purpose:** Automated decisioning for retail loans.

**Key Responsibilities:**
- Hard rules evaluation (fail-fast)
- Points-based scorecard
- ML model inference (Phase 2)
- Decision with reason codes
- Threshold-based routing (Approve/Decline/Refer)

**Bounded Context:** CreditAssessment

---

## 5. Cross-Cutting Concerns

### 5.1 Error Handling
- Use Result pattern for domain operations
- Global exception handling in API/Web layer
- Structured error responses with correlation IDs

### 5.2 Logging
- Serilog with structured logging
- Correlation ID propagation
- Sensitive data masking (BVN, account numbers)

### 5.3 Validation
- FluentValidation for DTO validation
- Domain invariant enforcement in entities

### 5.4 Caching
- Redis for frequently accessed data (products, configurations)
- Cache invalidation strategies

### 5.5 Resilience
- Polly for retry policies
- Circuit breaker for external APIs
- Timeout handling

### 5.6 Domain Events (Cross-Context Communication)

The system uses **domain events** for critical cross-bounded-context communication, providing loose coupling while maintaining audit compliance.

#### Infrastructure Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `IDomainEventDispatcher` | Domain | Interface for event dispatch |
| `IDomainEventHandler<T>` | Domain | Interface for event handlers |
| `DomainEventDispatcher` | Infrastructure | Reflection-based handler resolution |
| `DomainEventPublishingInterceptor` | Infrastructure | EF Core interceptor, dispatches after SaveChanges |

#### Event Flow

```
1. Command handler modifies aggregate
2. Aggregate raises event: AddDomainEvent(new XyzEvent(...))
3. UnitOfWork.SaveChangesAsync() called
4. EF Core persists changes to database
5. Interceptor fires AFTER successful commit
6. Interceptor collects all events from tracked aggregates
7. Events dispatched to registered handlers (new scope)
8. Handlers perform cross-context actions (e.g., create audit log)
```

#### Critical Flow Event Handlers

| Event | Handler | Action |
|-------|---------|--------|
| `WorkflowTransitionedEvent` | `WorkflowTransitionAuditHandler` | Creates audit log for workflow state changes |
| `CommitteeVoteCastEvent` | `CommitteeVoteAuditHandler` | Creates audit log for committee votes |
| `CommitteeDecisionRecordedEvent` | `CommitteeDecisionAuditHandler` | Creates audit log for committee decisions |
| `ScoringParameterChangeApprovedEvent` | `ScoringParameterChangeAuditHandler` | Creates audit log for config changes |
| `LoanApplicationCreatedEvent` | `LoanApplicationCreatedAuditHandler` | Creates audit log for new applications |
| `LoanApplicationApprovedEvent` | `LoanApplicationApprovedAuditHandler` | Creates audit log for approvals |

#### Design Decisions

1. **Eventual Consistency**: Events dispatched after commit, not within transaction
2. **Fault Tolerance**: Handler errors logged but don't rollback the original operation
3. **Scoped Resolution**: New DI scope per dispatch avoids DbContext conflicts
4. **Critical Flows Only**: Only important cross-context flows use events; other flows use direct service calls for simplicity

#### Adding New Event Handlers

1. Create event record in Domain aggregate (e.g., `public record MyEvent(...) : DomainEvent;`)
2. Aggregate raises event: `AddDomainEvent(new MyEvent(...));`
3. Create handler in Infrastructure: `public class MyEventHandler : IDomainEventHandler<MyEvent>`
4. Register in `DependencyInjection.cs`: `services.AddScoped<IDomainEventHandler<MyEvent>, MyEventHandler>();`

---

## 6. Database Schema Conventions

### Naming Conventions
- Tables: PascalCase plural (e.g., `LoanApplications`, `Directors`)
- Columns: PascalCase (e.g., `CreatedAt`, `ModifiedBy`)
- Foreign Keys: `[Entity]Id` (e.g., `LoanApplicationId`)
- Indexes: `IX_[Table]_[Column(s)]`

### Audit Columns (all tables)
- `Id` (GUID, primary key)
- `CreatedAt` (DateTime, UTC)
- `CreatedBy` (string, user ID)
- `ModifiedAt` (DateTime, UTC, nullable)
- `ModifiedBy` (string, user ID, nullable)
- `IsDeleted` (bool, soft delete)

### Encryption
- Sensitive fields encrypted at application level before storage
- Fields: BVN, RawBureauResponse, AccountNumber

---

## 7. API Design Conventions

### RESTful Endpoints
- Use plural nouns: `/api/loan-applications`, `/api/directors`
- HTTP verbs: GET, POST, PUT, PATCH, DELETE
- Versioning: `/api/v1/...`

### Response Format
```json
{
  "success": true,
  "data": { },
  "errors": [],
  "correlationId": "guid"
}
```

### Pagination
```json
{
  "items": [],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 100,
  "totalPages": 5
}
```

---

## 8. Testing Strategy

### Unit Tests
- Domain logic (entities, value objects, domain services)
- Application services with mocked dependencies
- Target: 80%+ coverage on Domain and Application layers

### Integration Tests
- Repository implementations with in-memory database
- External service adapters with mocked HTTP responses
- Workflow state transitions

### End-to-End Tests
- Critical user journeys
- API contract tests

---

## 9. Deployment Considerations

### Environment Configuration
- Use appsettings.{Environment}.json
- Sensitive values in environment variables or secrets manager

### Database Migrations
- EF Core migrations in Infrastructure project
- Migration scripts version controlled

### Health Checks
- Database connectivity
- External API availability
- Message queue connectivity

---

## 10. Session Recovery Guide

When starting a new Factory AI session for this project:

1. **Read this document first** - It contains architecture, patterns, and conventions
2. **Check module status** - See which modules are completed/in-progress
3. **Read relevant module docs** - For the specific module you're working on
4. **Follow the ubiquitous language** - Use consistent terminology
5. **Maintain documentation** - Update this tracker and module docs as you progress

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-16 | Factory AI | Initial implementation tracker |
| 1.3 | 2026-02-17 | Factory AI | Added modules 7-10 (Collateral, Guarantor, FinancialAnalyzer, AIAdvisory) |
| 1.4 | 2026-02-17 | Factory AI | Added database-driven scoring configuration with maker-checker workflow |
| 1.5 | 2026-02-17 | Factory AI | Added WorkflowEngine module (11) with state machine, SLA tracking, queues |
| 1.6 | 2026-02-17 | Factory AI | Added CommitteeWorkflow module (12) with multi-user voting and decision recording |
| 1.7 | 2026-02-17 | Factory AI | Added AuditService module (15) with immutable logging and sensitive data tracking |
| 1.8 | 2026-02-17 | Factory AI | Added domain event infrastructure for critical cross-context flows (Option 3) |
| 1.9 | 2026-02-17 | Factory AI | Added LoanPackGenerator module (13) with QuestPDF for comprehensive loan pack PDFs |
