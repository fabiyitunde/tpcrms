# CRMS - Implementation Tracker

**Version:** 1.1  
**Last Updated:** 2026-02-16  
**Status:** Implementation Phase

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
| **Domain Events** | LoanApplicationSubmitted, BureauCheckCompleted, DecisionMade, LoanDisbursed |
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
| **WorkflowCase** | The stateful progression of a loan application through approval stages |
| **BranchApproval** | First-level approval by the branch loan approving officer |
| **HOReview** | Head Office review stage for corporate loans |
| **CommitteeCirculation** | Stage where committee members review and comment |
| **FinalApproval** | The ultimate approval authority's decision |

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
| 3 | **CoreBankingAdapter** | 🔴 | P1 | [CoreBankingAdapter.md](modules/CoreBankingAdapter.md) | None |
| 4 | **CorporateLoanInitiation** | 🔴 | P1 | [CorporateLoanInitiation.md](modules/CorporateLoanInitiation.md) | ProductCatalog, CoreBankingAdapter |
| 5 | **CreditBureauIntegration** | 🔴 | P1 | [CreditBureauIntegration.md](modules/CreditBureauIntegration.md) | None |
| 6 | **StatementAnalyzer** | 🔴 | P1 | [StatementAnalyzer.md](modules/StatementAnalyzer.md) | None |
| 7 | **FinancialDocumentAnalyzer** | 🔴 | P2 | [FinancialDocumentAnalyzer.md](modules/FinancialDocumentAnalyzer.md) | None |
| 8 | **AIAdvisoryEngine** | 🔴 | P1 | [AIAdvisoryEngine.md](modules/AIAdvisoryEngine.md) | CreditBureauIntegration, StatementAnalyzer, FinancialDocumentAnalyzer |
| 9 | **WorkflowEngine** | 🔴 | P1 | [WorkflowEngine.md](modules/WorkflowEngine.md) | None |
| 10 | **CommitteeWorkflow** | 🔴 | P2 | [CommitteeWorkflow.md](modules/CommitteeWorkflow.md) | WorkflowEngine |
| 11 | **LoanPackGenerator** | 🔴 | P2 | [LoanPackGenerator.md](modules/LoanPackGenerator.md) | AIAdvisoryEngine, WorkflowEngine |
| 12 | **NotificationService** | 🔴 | P2 | [NotificationService.md](modules/NotificationService.md) | None |
| 13 | **AuditService** | 🔴 | P1 | [AuditService.md](modules/AuditService.md) | None |
| 14 | **ReportingService** | 🔴 | P3 | [ReportingService.md](modules/ReportingService.md) | All modules |
| 15 | **CustomerPortal** | 🔴 | P3 | [CustomerPortal.md](modules/CustomerPortal.md) | ProductCatalog, CreditBureauIntegration, StatementAnalyzer |
| 16 | **DecisionEngine** | 🔴 | P3 | [DecisionEngine.md](modules/DecisionEngine.md) | CreditBureauIntegration, StatementAnalyzer |

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

**Domain Entities:** LoanApplication, CorporateProfile, Director, Signatory

**Bounded Context:** Lending

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
- Aggregate all assessment data (bureau, cashflow, financials)
- Generate score matrix
- Produce recommendations (amount, tenor, interest tier)
- Generate risk commentary and red flags
- Create exportable decision support report

**AI Integration:** OpenAI/Azure OpenAI for analysis and commentary

**Bounded Context:** CreditAssessment

---

### 4.9 WorkflowEngine
**Purpose:** Manage loan application state transitions and approval routing.

**Key Responsibilities:**
- State machine implementation
- Role-based queue management
- SLA tracking and escalations
- State transition validation
- Audit trail for all transitions

**Domain Entities:** WorkflowCase, WorkflowState, StateTransition

**Bounded Context:** Workflow

---

### 4.10 CommitteeWorkflow
**Purpose:** Multi-user committee approval process for corporate loans.

**Key Responsibilities:**
- Committee member assignment
- Comment and voting management
- Document attachment to cases
- Circulation tracking
- Final decision aggregation

**Domain Entities:** CommitteeReview, CommitteeComment, Vote

**Bounded Context:** Workflow

---

### 4.11 LoanPackGenerator
**Purpose:** Generate comprehensive PDF loan packs for corporate loans.

**Key Responsibilities:**
- Aggregate all loan information
- Generate formatted PDF with sections
- Include bureau reports, AI analysis, committee comments
- Version control for generated packs

**Technology:** QuestPDF or iText7

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
**Purpose:** Maintain immutable audit logs for compliance.

**Key Responsibilities:**
- Log all data access events
- Log all decisions and overrides
- Log integration calls
- Immutable storage
- Audit report generation

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
