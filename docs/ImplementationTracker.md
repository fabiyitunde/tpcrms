# CRMS - Implementation Tracker

**Version:** 6.9
**Last Updated:** 2026-04-09
**Status:** Phase 1 COMPLETE (Backend + UI) | Audit Fixes Complete (A-E) | UI Enhancements COMPLETE | SmartComply Integration COMPLETE (Backend + UI) | Bank Statement UI COMPLETE | AI Advisory Data Quality COMPLETE | Scoring Config Editor COMPLETE | Core Banking API COMPLETE | Location Hierarchy COMPLETE | Location Bug Fixes COMPLETE | Location Admin UI COMPLETE | UI Wiring Audit + Committee Setup COMPLETE | Standing Committee + Auto-Routing COMPLETE | P3 UI Gaps RESOLVED | Hybrid AI Advisory COMPLETE | Save Transactions Bug Fixed | Add Transactions UX Bugs Fixed | Submit for Review Bug Fixed | Workflow Save Bugs Fixed (Approve/Return/AutoTransition) | Lifecycle Gaps G4/G6/G7/G9/G10/G11 Fixed | CreditAnalysis Stage Fully Wired | HOReview→CommitteeCirculation Desync Fixed | Disbursement Checklist COMPLETE (CP/CS state machine + PDF + CS monitoring) | Phase 2 Pending (Retail)

---

## Implementation Phases

### Phase 1: Corporate Loan System - COMPLETE ✅

#### Backend Modules (16/16)

| Step | Module | Status |
|------|--------|--------|
| 1. Loan Officer Initiation | CorporateLoanInitiation | ✅ |
| 2. Core Banking Data Pull | CoreBankingAdapter | ✅ (Real + Mock) |
| 3. Document Uploads | LoanApplication | ✅ |
| 4. Credit Bureau Checks | CreditBureauIntegration | ✅ (Auto-triggered) |
| 5. Financial Analysis | FinancialDocumentAnalyzer | ✅ |
| 6. AI Advisory | AIAdvisoryEngine | ✅ |
| 7. Branch/HO Review | WorkflowEngine | ✅ |
| 8. Committee Approval | CommitteeWorkflow | ✅ |
| 9. PDF Loan Pack | LoanPackGenerator | ✅ |
| 10. Notifications | NotificationService | ✅ |
| 11. Audit Trail | AuditService | ✅ |
| 12. Reporting | ReportingService | ✅ |
| 13. Identity/Auth | IdentityService | ✅ |
| 14. Product Catalog | ProductCatalog | ✅ |
| 15. Collateral | CollateralManagement | ✅ |
| 16. Guarantor | GuarantorManagement | ✅ |

#### Intranet UI (12 Pages) - COMPLETE ✅

All pages call Application layer **directly** via `ApplicationService` (no HTTP/API calls):

| Page | Route | ApplicationService Methods |
|------|-------|---------------------------|
| Dashboard | `/` | `GetDashboardSummaryAsync`, `GetMyPendingTasksAsync` |
| My Queue | `/queues/my` | `GetMyPendingTasksAsync` |
| All Queues | `/queues/all` | `GetQueueSummaryAsync`, `GetQueueByRoleAsync` |
| Applications | `/applications` | `GetApplicationsByStatusAsync` |
| Application Detail | `/applications/{id}` | `GetApplicationDetailAsync`, `GenerateLoanPackAsync`, `GenerateAdvisoryAsync`, `CastVoteAsync`, `TransitionWorkflowAsync`, `VerifyDocumentAsync`, `RejectDocumentAsync`, `SetCollateralValuationAsync`, `ApproveCollateralAsync`, `ApproveGuarantorAsync`, `RejectGuarantorAsync` |
| New Application | `/applications/new` | `GetLoanProductsAsync`, `CreateApplicationAsync`, `SubmitApplicationAsync` |
| Committee Reviews | `/committee/reviews` | `GetCommitteeReviewsByStatusAsync` |
| My Votes | `/committee/my-votes` | `GetMyPendingVotesAsync`, `CastVoteAsync` |
| Reports | `/reports` | `GetReportingMetricsAsync` |
| Audit Trail | `/reports/audit` | `GetAuditLogsAsync` |
| Users | `/admin/users` | `GetUsersAsync` |
| Products | `/admin/products` | `GetAllLoanProductsAsync` |

**Auth:** `AuthService` calls `IAuthService` directly (no HTTP)

**Disbursement:** After final approval, branch manually books the loan in core banking (Fineract). Automated disbursement API is available but not exposed - intentional for audit/compliance reasons.

### Audit Fix Phases - COMPLETE ✅

Following a comprehensive code audit (78 issues found), systematic fixes were applied across 5 phases:

| Phase | Focus | Status | Key Fixes |
|-------|-------|--------|-----------|
| **A** | Code Bug Fixes | ✅ | Rejection tracking fields, DSCR calculation, role naming |
| **B** | Domain Logic Gaps | ✅ | Consent verification, quorum enforcement, workflow handlers |
| **C** | Infrastructure | ✅ | File storage abstraction (Local/S3), concurrency tokens |
| **D** | Security Hardening | ✅ | IP capture, sensitive data masking, rate limiting, authorization |
| **E** | Performance & Polish | ✅ | Exponential retry backoff, reporting cache, seed data |

**Migrations Created:**
- `AddRejectionTrackingFields` (Phase A)
- `PhaseBDomainLogicFixes` (Phase B - ConsentRecords table)
- `PhaseCInfrastructure` (Phase C - RowVersion columns)
- `PhaseEPerformance` (Phase E - NextRetryAt column)

**New Components Added:**
- `ConsentRecord` aggregate (NDPA compliance)
- `IFileStorageService` with Local and S3 implementations
- `IHttpContextService` for IP address capture
- `SensitiveDataMasker` utility
- `EnvironmentRestrictedAttribute` filter
- `SeedData` class for roles and products
- Workflow integration handlers (Committee→Workflow, CreditChecks→Workflow)

### Phase 2: Retail Loan Backend - PENDING 🔴

| Module | Purpose | Status |
|--------|---------|--------|
| CustomerPortal | Self-service application backend | 🔴 Not Started |
| DecisionEngine | Automated Approve/Decline/Refer | 🔴 Not Started |

**Pending for Phase 2:**
- Customer Portal UI (Blazor pages for customer self-service)

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
| 16 | **ReportingService** | 🟢 | P3 | [ReportingService.md](modules/ReportingService.md) | All modules |
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
**Purpose:** Integration layer for the bank's core banking system (CBS).

**Key Responsibilities:**
- Customer/corporate account lookup by NUBAN (`/core/account/fulldetailsbynuban/{nuban}`)
- Director and signatory retrieval (from the same endpoint)
- Account transaction/statement retrieval (`/core/transactions/{nuban}?startDate=&endDate=`)

**Authentication:** OAuth 2.0 Client Credentials (bearer token via `CoreBankingAuthHandler`)

**Integration Pattern:** Anti-corruption layer with adapter pattern. Single CBS response cached per-request to avoid duplicate calls.

**Real API Endpoints (2):**
1. `GET /core/account/fulldetailsbynuban/{nuban}` — client details + directors[] + signatories[]
2. `GET /core/transactions/{nuban}?startDate=DD-MM-YYYY&endDate=DD-MM-YYYY` — transaction list

**Not Available from CBS:** Loan booking/disbursement (done manually), balance queries, customer creation, repayment schedules. These are stubbed with explicit failure messages for Phase 2.

**Configuration:** `appsettings.json` → `CoreBanking` section (`BaseUrl`, `ClientId`, `ClientSecret`, `TokenEndpoint`, `UseMock`)

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
**Purpose:** Send notifications via multiple channels with event-driven triggering.

**Key Responsibilities:**
- Multi-channel delivery: Email, SMS, WhatsApp, InApp, Push
- Template-based messaging with {{variable}} substitution
- Delivery tracking (Pending → Sending → Sent → Delivered → Read)
- Retry logic (max 3 retries for failed notifications)
- Scheduled notifications support
- Event-driven: Consumes WorkflowAssigned, WorkflowSLABreached, CommitteeVotingStarted events
- Background processing via NotificationProcessingService (30-second intervals)

**Domain Entities:** Notification (with lifecycle management), NotificationTemplate
**Key Features:**
- Mock senders for development (Email, SMS, WhatsApp)
- User notification inbox with unread count
- Admin template CRUD operations
- Context linking to LoanApplication

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
- Dashboard summary with key metrics at-a-glance
- Loan funnel metrics (Submitted → In Review → Approved → Disbursed)
- Portfolio analytics (by product, type, risk rating, aging)
- Performance metrics (processing times, SLA compliance, user productivity)
- Decision distribution (approval rates, rejection reasons)
- Committee analytics (activity, voting patterns, member participation)
- SLA compliance tracking

**Domain Entities:** ReportDefinition (optional saved report configs)
**Key Features:**
- Date range filtering on all reports
- Role-based access control (sensitive reports restricted)
- Aggregates data from LoanApplications, Workflows, Committees
- Ready for export enhancement (Excel/PDF)

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
- **In-Memory Cache** for reporting dashboard (5-15 min TTL)
- Redis (optional) for distributed caching
- Cache invalidation strategies

### 5.5 Rate Limiting
- .NET 9 built-in rate limiting middleware
- **Global limit**: 100 requests/minute per IP
- **Auth endpoints**: 5 requests/minute (strict for brute-force protection)
- **Sensitive operations**: 20 requests/minute

### 5.6 File Storage
- **Abstraction**: `IFileStorageService` interface
- **Local storage**: `LocalFileStorageService` for dev/single-server
- **S3 storage**: `S3FileStorageService` for production (AWS S3, MinIO, DigitalOcean Spaces)
- Configuration-based provider selection

### 5.7 Concurrency Control
- **Optimistic locking** via `RowVersion` columns on critical aggregates
- Applied to: `WorkflowInstance`, `LoanApplication`
- EF Core throws `DbUpdateConcurrencyException` on conflicts

### 5.8 Resilience
- Polly for retry policies
- Circuit breaker for external APIs
- Timeout handling
- **Exponential backoff** for notification retries (5^n minutes)

### 5.9 Security
- **IP address capture** via `IHttpContextService` (supports X-Forwarded-For)
- **Sensitive data masking** via `SensitiveDataMasker` (BVN, account numbers, etc.)
- **Environment-restricted endpoints** via `EnvironmentRestrictedAttribute`
- **Role-based authorization** on all sensitive endpoints

### 5.10 Consent Management (NDPA Compliance)
- `ConsentRecord` aggregate tracks borrower consent
- Required before any credit bureau check
- Supports multiple consent types and capture methods
- Consent validation integrated into credit check workflows

### 5.11 Domain Events (Cross-Context Communication)

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
| 2.0 | 2026-02-17 | Factory AI | Added Intranet UI with all core pages and modern CSS design system |
| 2.1 | 2026-02-17 | Factory AI | Comprehensive audit review completed (78 issues documented) |
| 2.2 | 2026-02-18 | Factory AI | Phase A-B fixes: rejection tracking, DSCR calculation, consent verification, quorum enforcement |
| 2.3 | 2026-02-18 | Factory AI | Phase C-E fixes: file storage (S3), concurrency tokens, security hardening, caching, seed data |
| 2.4 | 2026-02-19 | Factory AI | Intranet UI enhancements: Data entry modals (Collateral, Guarantor, Document, Financial Statement), flexible financial statement validation by business age, document viewer, workflow fixes, delete/edit for Collateral and Guarantor |
| 2.5 | 2026-02-19 | Factory AI | Intranet UI: View modals for Collateral and Guarantor; error messages moved to modal footers; Add Guarantor silent-failure fix (GuaranteeType enum values) |
| 2.6 | 2026-02-20 | Factory AI | Intranet UI: Document verify/reject fully wired (RejectDocumentCommand added to Application layer); SetCollateralValuationModal created; CollateralTab valuation/approve buttons; approve collateral confirmation modal; directors/signatories confirmed as read-only (auto-fetched from core banking) |
| 2.7 | 2026-02-21 | Factory AI | Intranet UI: Guarantor approve/reject fully wired (ApproveGuarantorAsync + RejectGuarantorAsync in ApplicationService; GuarantorsTab updated with CanManageGuarantors param and contextual buttons; Detail.razor approve confirmation modal and reject modal with mandatory reason; DI registrations verified; build clean) |
| 2.8 | 2026-02-21 | Factory AI | Intranet UI: Collateral document management complete — ICollateralDocumentRepository interface + CollateralDocumentRepository; UploadCollateralDocumentCommand/Handler + DeleteCollateralDocumentCommand/Handler in CollateralCommands.cs; RemoveDocument() on Collateral aggregate; UploadCollateralDocumentModal.razor (NEW); ViewCollateralModal DOCUMENTS section with view/download/delete + confirmation; CollateralTab upload button; Detail.razor wired; /api/collateral-documents/{id}/view and /download endpoints; delete removes both DB record and storage file |
| 2.9 | 2026-03-01 | Factory AI | SmartComply Integration: Complete infrastructure for new credit bureau provider (SmartComply/Adhere API). Created ISmartComplyProvider interface with 18+ methods covering Individual Credit (10 endpoints), Business Credit (3 endpoints), Loan Fraud Check (2 endpoints), KYC/Identity (6 endpoints). Implemented SmartComplyProvider (real HTTP client) and MockSmartComplyProvider (test data). Added SmartComplySettings, SmartComplyEndpoints, SmartComplyDtos. Updated CreditBureauEnums with SmartComply types. Registered in DI with UseMock toggle. Configuration added to appsettings.json. |
| 3.0 | 2026-03-01 | Factory AI | SmartComply Wired to Application Layer: Complete rewrite of ProcessLoanCreditChecksCommand to use SmartComply. Individual credit checks use GetCRCFullAsync (directors/signatories/guarantors). Business credit check added using GetCRCBusinessHistoryAsync with RC number. Fraud checks added for both individuals (CheckIndividualLoanFraudAsync) and business (CheckBusinessLoanFraudAsync). Added RegistrationNumber field to LoanApplication aggregate (auto-fetched from Core Banking). Updated LoanApplicationDto and all mappers. New DTOs: BusinessCreditCheckResultDto with fraud risk scores. Credit check batch now includes business result alongside individual results. 11 Bug fixes applied: BUG-1 (handler DI registration), BUG-3 (fraud result persistence), BUG-4 (consent before API call), BUG-5 (check RecordCreditCheckCompleted result), BUG-6 (per-account delinquency), BUG-7 (mock income=0 handling), BUG-8 (idempotency), BUG-9 (logging + cancellation), BUG-10 (error message), BUG-11 (individual account number). Two migrations created: AddRegistrationNumberToLoanApplication, AddFraudCheckFieldsToBureauReport. |
| 3.1 | 2026-03-01 | Factory AI | SmartComply Code Review + Critical/High Bug Fixes: Comprehensive review of all SmartComply integration code (14 issues found). Fixed 4 critical/high issues: C-1 — workflow no longer advances when all credit checks blocked by NoConsent (RecordCreditCheckCompleted now guarded; idempotency excludes Failed reports to allow re-runs); H-1 — RecordBulkConsentHandler.CreateOrGetConsent returns ApplicationResult failure instead of throwing InvalidOperationException; H-2 — InternalError paths in both ProcessIndividualCreditCheck and ProcessBusinessCreditCheck now create failed BureauReport audit records before returning; H-3 — added processedInBatch dictionary to RecordBulkConsentHandler to prevent duplicate consent records when multiple parties share a BVN within the same batch. 10 medium/low issues documented in SESSION_HANDOFF for future sessions. |
| 3.2 | 2026-03-01 | Factory AI | Credit Bureau UI Complete: Updated BureauReportDto/BureauReportSummaryDto with new SmartComply fields (ActiveLoans, TotalOverdue, FraudRiskScore, FraudRecommendation, PartyId, PartyType). Added GetBureauReportsAsync() to ApplicationService. Completely redesigned BureauTab.razor with: business credit report section (highlighted styling), party grouping (Directors/Signatories/Guarantors in separate sections with icons), fraud risk badges (Low/Medium/High color-coded), new metrics display (TotalOverdue, MaxDelinquencyDays, Provider), failed check indicator. Updated Detail.razor to fetch real bureau reports. Registered GetBureauReportByIdHandler and GetBureauReportsByLoanApplicationHandler in DI. |
| 3.5 | 2026-03-09 | Factory AI | Code quality M-1/M-2 (NIN index on ConsentRecords, ConsentRecordId index on BureauReports). User CRUD: `UpdateUserCommand`+handler, `ToggleUserStatusCommand`+handler, `ApplicationUser.ClearRoles()` added; 3 handlers registered in DI; `CreateUserAsync`, `UpdateUserAsync`, `ToggleUserStatusAsync` added to `ApplicationService`; `Users.razor` SaveUser/ToggleUserStatus wired to real backend. Product CRUD: `SuspendLoanProductCommand`+handler, `LoanProductSuspendedEvent`; `ActivateLoanProductHandler`+`SuspendLoanProductHandler` registered; `CreateLoanProductAsync`, `UpdateLoanProductAsync`, `ToggleLoanProductAsync` added; `Products.razor` SaveProduct/ToggleProduct wired. Bug fix: `LoanProductSummaryDto` extended with `MinTenorMonths`/`MaxTenorMonths`/`BaseInterestRate`; `ToSummaryDto()` mapper updated; hardcoded fallback values removed from both `GetLoanProductsAsync` and `GetAllLoanProductsAsync`. Build: 0 errors. |
| 3.4 | 2026-03-09 | Factory AI | Bank Statement Transaction Drill-Down: `ViewStatementModal.razor` (new) — transaction list with All/Credits/Debits filter, live search, category badges (color-coded), recurring badge, negative balance highlight. `StatementTransactionInfo` model added to `ApplicationModels.cs`. `GetStatementTransactionsAsync` added to `ApplicationService.cs`. "View" button added to own-bank card and each external statement row in `StatementsTab.razor`. `Detail.razor` wired with state vars, show/close handlers, and modal block. No backend changes — `GetStatementTransactionsHandler` already registered. Build: 0 errors. |
| 3.3 | 2026-03-01 | Factory AI | Bank Statement Auto-Fetch + External Statements UI + Editable Fallback Fields: InitiateCorporateLoanCommand now persists 6-month CoreBanking statement on application create. LoanApplication.IncorporationDate added (with migration). LoanApplicationParty.UpdateBVN/UpdateShareholdingPercent domain methods. UpdatePartyInfoCommand/Handler (new). VerifyStatementCommand/RejectStatementCommand + handlers added to UploadStatementCommand.cs. BankStatementSummaryDto extended to 18 fields. GetStatementsByLoanApplicationHandler mapper updated. 8 statement handlers + UpdatePartyInfoHandler + domain services registered in DI. StatementsTab.razor (new): Own Bank + Other Banks sections, trust badges, cashflow metrics, verify/reject/analyze buttons. UploadExternalStatementModal.razor (new). FillPartyInfoModal.razor (new). PartiesTab: "Complete info" button for null BVN/shareholding (Draft only). New.razor: real FetchCorporateDataAsync() call + override fields for null RC number/IncorporationDate. ApplicationService: 7 new methods (GetBankStatements, UploadExternal, Verify, Reject, Analyze, FetchCorporateData, UpdatePartyInfo). Manual migration 20260301170000 + Designer.cs. Build: 0 errors. |
| 3.6 | 2026-03-13 | Factory AI | SmartComply CAC Advanced data structure complete: `CacAdvancedData`/`CacAdvancedDirectorData` DTOs (30+ fields per director), `ISmartComplyProvider` split into `VerifyCacAsync` (basic) and `VerifyCacAdvancedAsync` (advanced), `SmartComplyCacDirector` expanded from 3 to 24 fields, `MockSmartComplyProvider` updated with rich mock data. New application flow redesigned: RC number always editable; `FetchCacDirectorsAsync` (new) calls CAC Advanced to retrieve directors; `FetchCorporateDataAsync` now includes signatories from core banking; `ApplicationModels.cs` extended with `DirectorInput`/`SignatoryInput`/`CacLookupResult`/`CacDirectorEntry`; `CreateApplicationRequest` carries directors and signatories; `InitiateCorporateLoanCommand` accepts `DirectorInput[]`/`SignatoryInput[]` with core banking fallback; `New.razor` Step 1 redesigned (CAC lookup banner, director cards with BVN input, signatory BVN entry). Build: 0 errors. |
| 3.7 | 2026-03-13 | Factory AI | AI Advisory bureau data fix: `GenerateCreditAdvisoryHandler` now injects `IBureauReportRepository` and queries real `BureauReport` records by `LoanApplicationId`, indexed by `PartyId`; `MapBureauReport()` helper maps all domain fields (credit score, active loans, performing/non-performing, outstanding balance, overdue, delinquency days, legal actions, fraud score) and derives `WorstStatus`; corporate bureau report (`SubjectType.Business`) added as "Corporate" entry; parties without bureau data flagged via `IsPlaceholder`. `BureauDataInput` extended with `MaxDelinquencyDays`, `HasLegalActions`, `TotalOverdue`, `FraudRiskScore`, `FraudRecommendation`, `IsPlaceholder`. Scoring config alignment: 10 new fields added to `CreditHistoryConfig` (`LegalActionsPenalty`, `SevereDelinquencyDaysThreshold`/`Penalty`, `WatchListDaysThreshold`/`Penalty`, `HighFraudRiskScoreThreshold`/`Penalty`, `ElevatedFraudRiskScoreThreshold`/`Penalty`, `MissingBureauDataPenaltyPerParty`); all loaded from DB via `ScoringConfigurationService`; `MockAIAdvisoryService.CalculateCreditHistoryScore()` uses `cfg.` for all values. Build: 0 errors. |
| 3.9 | 2026-03-16 | Factory AI | Real Core Banking API integration: Created `CoreBankingService` (real HTTP client for CBS API using 2 endpoints: `fulldetailsbynuban` for account+directors+signatories, `transactions` for statements). `CoreBankingAuthHandler` implements OAuth 2.0 Client Credentials with token caching. `CoreBankingSettings` + `CoreBankingDtos` match actual CBS JSON shape. `DependencyInjection.cs` updated with conditional `UseMock` toggle + retry policy. `MockCoreBankingService` completely rewritten to reflect real API constraints (no shareholding/nationality on directors, no mandate type/designation on signatories, no industry/incorporation date on corporate info, dead operations return explicit failures). Director discrepancy indicator added to `New.razor`: collapsible CBS-vs-SmartComply comparison with match/discrepancy badge, names-only-in-one-source listing, and CBS director reference table. `FetchCorporateDataAsync` now also fetches CBS directors via `GetDirectorsAsync` for comparison. `CbsDirectorInfo` model added to `ApplicationModels.cs`. Build: 0 errors. |
| 4.0 | 2026-03-16 | Factory AI | AI Advisory data quality fixes (6 gaps): GAP-1 — ExistingExposure now derived from corporate bureau report `TotalOutstandingBalance`/`ActiveLoans` instead of hardcoded zero. GAP-2 — `PendingReview` financials now included with `IsUnverified` flag on `FinancialDataInput`. GAP-3 — `RecurringCreditsCount`/`RecurringDebitsCount` aggregated from `BankStatement.Transactions.IsRecurring`. GAP-5 — `IndustrySector` field added to `LoanApplication` domain, `CreateCorporate()` factory, DTOs, mappers, `InitiateCorporateLoanCommand`, `New.razor` Step 2 (17-option CBN sector dropdown), and AI request. Migration `20260316120000_AddIndustrySectorToLoanApplication`. GAP-6 — confirmed already implemented (YearType in FinancialDataInput). GAP-7 — `GuarantorDataInput` extended with `HasBureauReport` flag; each guarantor checked against `reportsByParty`. GAP-8 — `Valued` collateral now included alongside `Approved`; `CollateralDataInput` extended with `ApprovedCount`, `ValuedButNotApprovedCount`, `ValuedButNotApprovedMarketValue`. Build: 0 errors. |
| 4.1 | 2026-03-16 | Factory AI | Role-based workflow authorization alignment: Comprehensive review of 11 roles vs 17 workflow stages. Fixed 4 UI authorization issues in `Detail.razor`: (1) `HOReview` status now checks `CreditOfficer` instead of `HOReviewer`; (2) `ShowReturnButton` now has per-status role checks (`BranchApprover`/`CreditOfficer`); (3) `ShowRejectButton` now has per-status role checks; (4) Added `CommitteeCirculation`→`CommitteeMember` and changed `FinalApproval`→`CommitteeApproved` for `FinalApprover`. UI button visibility now matches backend workflow transitions exactly. |
| 4.2 | 2026-03-16 | Factory AI | Location hierarchy + role-based visibility filtering: `Location` aggregate with 4-level hierarchy (HeadOffice→Region→Zone→Branch), `LocationType`/`VisibilityScope` enums, `ILocationRepository` (13 methods), `LocationRepository` with hierarchy traversal, `VisibilityService` domain service. `ApplicationUser.LocationId` replaces `BranchId`. `Roles.RoleVisibilityScopes` maps 11 roles (Branch: LoanOfficer/BranchApprover; Global: CreditOfficer/HOReviewer/Committee/FinalApprover/Operations/Risk/Audit/Admin; Own: Customer). EF migration `AddLocationHierarchy` creates Locations table + renames Users.BranchId→LocationId. Seed data: 21 Nigeria locations (1 HO, 2 Regions, 6 Zones, 12 Branches). Query handlers `GetLoanApplicationsByStatusHandler`/`GetPendingBranchReviewHandler` now accept visibility params and filter through `VisibilityService`. `UserInfo.LocationId`/`PrimaryRole` added. `ApplicationService.GetApplicationsByStatusAsync` has visibility-aware overload. Applications Index page passes user context. Build: 0 errors, all tests pass. |
| 4.3 | 2026-03-16 | Factory AI | Location/visibility bug fixes (8 bugs + 2 gaps): BUG-1 AuthService.cs now maps LocationId/LocationName; BUG-2 CreateApplicationAsync passes userLocationId; BUG-3 UserDto.BranchId→LocationId renamed across auth chain (5 files); BUG-4 GetHierarchyTreeAsync builds parent-child tree in-memory; BUG-5 UpdateUserCommand has LocationId + calls SetLocation(); BUG-6 LoanApplicationsController extracts JWT claims for visibility; BUG-7 GetPendingBranchReviewHandler registered in Infrastructure DI; BUG-8 VisibilityService return value documented; GAP-2 SeedTestUsersAsync creates 6 test users with locations (pwd: Test@123); GAP-5 NE zone now has Maiduguri + Bauchi branches. Build: 0 errors, 5 tests pass. |
| 4.4 | 2026-03-16 | Factory AI | Location CRUD Admin UI + User location picker: New Application layer module `CRMS.Application.Location` with `LocationDtos.cs` (3 DTOs), `LocationCommands.cs` (4 commands: Create/Update/Activate/Deactivate + handlers), `LocationQueries.cs` (5 queries: GetById/GetAll/GetByType/GetTree/GetChildren + handlers). 9 handlers registered in Infrastructure DI. `ApplicationService` extended with 8 location methods. New `Locations.razor` admin page at `/admin/locations` with tree view (collapsible hierarchy with emoji icons), list view, search/filter by type/status, create/edit/activate/deactivate modals. `Users.razor` updated with dynamic location picker dropdown replacing hardcoded branch list. `ApplicationModels.cs` extended with `LocationInfo` and `LocationTreeNode` models. Build: 0 errors, 4 tests pass. |
| 4.5 | 2026-03-18 | Factory AI | Comprehensive UI wiring audit + critical fixes + committee setup. **Phase 1 — Tracker items resolved:** Performance/Committee report pages wired to ReportingService (7 new DTOs); M-3 RequestBureauReportCommand migrated from ICreditBureauProvider to ISmartComplyProvider.GetCRCFullAsync; M-5 NonPerformingAccounts→DelinquentFacilities rename (10 files + DB migration); M-4 in-process concurrency lock (ConcurrentDictionary+SemaphoreSlim); removed mock product fallback from New.razor. **Phase 2 — Detail page audit fixes:** 4 tabs wired to real backend (Workflow via GetWorkflowByLoanApplicationHandler, Advisory via GetLatestAdvisoryByLoanApplicationHandler, Committee via GetCommitteeReviewByLoanApplicationHandler, Comments via AddCommitteeCommentHandler); DownloadDocumentAsync now uses IFileStorageService; GetMyPendingTasksAsync fetches real Amount/ProductName; collateral ForcedSaleValue/LastValuationDate mapping fixed. **Phase 3 — Committee workflow UI:** Voting authorization guard on CommitteeTab (3 states: not member/already voted/can vote); `SetupCommitteeModal.razor` 2-step wizard (configure type+quorum+deadline, add members with roles+chairperson); `CanSetupCommitteeReview` gate (CreditOfficer at CommitteeCirculation, no existing review); 3 new ApplicationService methods (CreateCommitteeReviewAsync, AddCommitteeMemberAsync, GetCommitteeMemberUsersAsync). 6 new DI registrations. Build: 0 errors, all tests pass. |
| 4.6 | 2026-03-18 | Factory AI | Standing committee infrastructure + automatic routing. **Domain:** `StandingCommittee` aggregate with `StandingCommitteeMember` children; properties: Name, CommitteeType, RequiredVotes, MinimumApprovalVotes, DefaultDeadlineHours, MinAmountThreshold, MaxAmountThreshold, IsActive; domain methods: Create, Update, AddMember, RemoveMember, UpdateMember, Activate/Deactivate. `IStandingCommitteeRepository` with `GetForAmountAsync(amount)` for routing. **Infrastructure:** `StandingCommitteeConfiguration` + `StandingCommitteeMemberConfiguration` (unique index on CommitteeType; composite unique on StandingCommitteeId+UserId). `StandingCommitteeRepository` with amount-range query. Migration `20260318120000_AddStandingCommittees`. **Application:** 5 commands (Create/Update/Toggle/AddMember/RemoveMember) + 2 queries (GetAll/GetForAmount). **UI:** `Committees.razor` admin page at `/admin/committees` with card layout, CRUD modals, member management. `SetupCommitteeModal` rewritten: auto-routes to standing committee by `application.Loan.RequestedAmount`; pre-populates members from roster; falls back to ad-hoc if no match. **Seed:** 5 standing committees (Branch N0-50M, Regional N50-200M, HO N200-500M, Management N500M-2B, Board N2B+). 8 new DI registrations. Build: 0 errors, tests pass. |
| 4.7 | 2026-03-18 | Factory AI | P3 UI gaps resolved. **Template CRUD:** `NotificationTemplateCommands.cs` (new) with Create/Update/Toggle commands+handlers. `INotificationTemplateRepository.GetAllAsync()` added. `GetAllNotificationTemplatesQuery` updated with `IncludeInactive` param. `Templates.razor` complete rewrite: real backend, create/edit/toggle/preview, search/filter. 5 DI registrations. 4 new `ApplicationService` methods. **Bureau report detail modal:** `ViewBureauReportModal.razor` (new) showing score circle, key metrics, fraud risk assessment, alerts/red flags, credit accounts table. `BureauTab.razor` updated with `OnViewReport` callback + view buttons. `Detail.razor` wired. `GetBureauReportDetailAsync` added to `ApplicationService`. **Guarantor credit check trigger:** Confirmed N/A — already auto-triggered after branch approval via `ProcessLoanCreditChecksCommand`. Build: 0 errors, tests pass. |
| 4.8 | 2026-03-18 | Factory AI | Hybrid AI Advisory architecture. **New files:** `RuleBasedScoringEngine.cs` — extracted deterministic scoring logic (5 risk categories, configurable thresholds from DB); `LLMNarrativeGenerator.cs` — builds structured prompts with Nigerian banking context, calls OpenAI GPT-4o-mini for narrative text; `HybridAIAdvisoryService.cs` — orchestrates rule-based scoring + optional LLM narratives with graceful fallback; `AIAdvisorySettings.cs` — configuration (UseLLMNarrative toggle, timeout, fallback). **Key principle:** LLM enhances presentation but NEVER changes scores or recommendations. **DI:** Config-based toggle registers `LLMNarrativeGenerator` only when `UseLLMNarrative=true`. **Config:** `appsettings.json` (API + Web.Intranet) updated with `AIAdvisory` section + `OpenAI.ApiKey` placeholder. **Cost:** Rule-only ~50ms/$0; Hybrid ~3-5s/~$0.01 per advisory. Build: 0 errors, tests pass. |
| 6.9 | 2026-04-09 | Factory AI | Full disbursement checklist feature (Session 46). Domain: `DisbursementChecklistItem` state machine (`ChecklistItemStatus` with 9 values; `ConditionType` Precedent/Subsequent; `Satisfy`, `SubmitForLegalReview`, `RatifyByLegal`, `ReturnByLegal`, `ProposeWaiver`, `ApproveWaiver`, `RejectWaiver`, `RequestExtension`, `ApproveExtension`, `RejectExtension`, `MarkOverdue()` made public). Application: `ConfirmOfferAcceptanceCommand`+handler (CP gate + `loanApp.AcceptOffer()` + Disbursement Memo PDF generation); `GetDisbursementChecklistQuery`+handler; 8 item-action command/handler pairs; 3 admin checklist template command/handler pairs (`AddChecklistTemplateItemCommand`, `UpdateChecklistTemplateItemCommand`, `RemoveChecklistTemplateItemCommand` — SystemAdmin/RiskManager only); `LoanPackData` gains `List<string> ApprovalConditions`; `GenerateLoanPackCommand` parses `committeeReview.ApprovalConditions` (newline-split). Infrastructure: `DisbursementMemoPdfGenerator.cs` (QuestPDF: loan summary box, CP table, CS table, certification + signature lines); `CsMonitoringBackgroundService.cs` (daily cycle, LINQ join query to avoid navigation property, `MarkOverdue()`, tiered T-7/T-1/T+0/T+7/T+30/T+90 log warnings); 13 handler registrations + `IDisbursementMemoPdfGenerator` + hosted service added to `DependencyInjection.cs`; `LoanPackPdfGenerator.cs` — Section 12 "Conditions of Approval" added. UI: `OfferAcceptanceTab.razor` (new) — CP/CS tables, green/amber summary banner, single `activeModal`+`activeItem` state machine handles all 7 action types, role-filtered buttons per item; `Detail.razor` — hardcoded `cpChecklist`/`CpCheckItem`/`InitialiseCpChecklist()` removed, "Disbursement Checklist" tab added (visible at OfferGenerated/OfferAccepted/Disbursed), `ShowRecordAcceptanceButton` now Operations/SystemAdmin only, Disburse modal body replaced with green "CPs verified" banner, CP gate removed from Disburse button (domain now enforces via `loanApp.AcceptOffer()`); `ApplicationService.cs` — 8 new checklist methods, `IssueOfferLetterAsync` seeds checklist, `RecordOfferAcceptanceAsync` calls `ConfirmOfferAcceptanceHandler` first; `ApplicationModels.cs` — `DisbursementChecklistModel`, `ChecklistItemModel`, `ChecklistTemplateItemModel` added. Migration `20260409123746_AddDisbursementChecklist` created (PENDING). Build: 0 errors. |
| 6.8 | 2026-04-08 | Factory AI | Three bug clusters fixed (Session 45). BUG-1 (Seeder crash): `SeedWorkflowDefinitionAsync` upgrade blocks rewrote using pure `ExecuteSqlRawAsync` — no EF tracking, no `SaveChangesAsync` in upgrade path; `INSERT IGNORE` for stages (unique index idempotency), `INSERT ... WHERE NOT EXISTS` for transitions; `InsertTransitionIfMissingAsync` helper added. BUG-2 (File upload): `LocalFileStorageService.UploadAsync` now calls `Directory.CreateDirectory(Path.GetDirectoryName(filePath))` before `File.WriteAllBytesAsync` — callers pass `"AppNumber/FileName.pdf"` as fileName so a subdirectory was never created, causing `DirectoryNotFoundException` on every upload (broke offer letter generation and loan pack download). BUG-3 (Offer letter/loan pack version collision): `OfferLetter.Create()` and `LoanPack.Create()` hardcoded `Version = 1`; unique index on `(LoanApplicationId, Version)` caused `DbUpdateException` on retry after a prior Failed record; catch block's `SaveChangesAsync` was unprotected so constraint violation masked the real error. Fixes: added `version` param to both `Create()` methods; `GetVersionCountAsync` → `GetMaxVersionAsync` (uses `MAX(Version) ?? 0` instead of `COUNT`); catch blocks wrapped in inner try-catch (best-effort audit record); `GetOfferLettersByApplicationQuery` now filters out `Failed` records so UI only shows downloadable versions. Files: `OfferLetter.cs`, `LoanPack.cs`, both repository interfaces + implementations, both command handlers, `OfferLetterQueries.cs`, `LocalFileStorageService.cs`, `ComprehensiveDataSeeder.cs`. |
| 6.7 | 2026-04-07 | Factory AI | HOReview → CommitteeCirculation domain desync fixed (Session 44). `LoanApplication.Status` stayed at HOReview while `WorkflowInstance.CurrentStatus` advanced to CommitteeCirculation on HO Reviewer approval — because `ApproveApplicationAsync` never called `MoveToCommittee()` on the domain aggregate for the HOReview case. Fix: (1) `services.AddScoped<MoveToCommitteeHandler>()` added to `DependencyInjection.cs` (handler existed but was unregistered). (2) `else if (currentStatus == "HOReview")` block added to `ApplicationService.ApproveApplicationAsync` — resolves `MoveToCommitteeHandler` in a fresh scope, calls `MoveToCommitteeCommand`, fails fast before workflow transition if domain update fails. Two stuck applications fixed directly in DB. Build: 0 errors. |
| 6.6 | 2026-04-07 | Factory AI | CreditAnalysis stage fully wired (Session 43). Root cause identified: all Application layer handlers must be explicitly registered in `DependencyInjection.cs` — no MediatR assembly scanning. Four handlers were unregistered: `ApproveCreditAnalysisHandler` (cause of "Failed to approve application"), `ReturnFromCreditAnalysisHandler` (new), `ReturnFromHOReviewHandler`, `FinalApproveHandler`. Added all four to DI. CreditAnalysis Return capability added end-to-end: `LoanApplication.ReturnFromCreditAnalysis()` domain method; `ReturnFromCreditAnalysisCommand`+handler; `ApplicationService.ReturnApplicationAsync` domain call for CreditAnalysis case; `ShowReturnButton` in `Detail.razor` includes `CreditAnalysis→CreditOfficer`; `ComprehensiveDataSeeder` + `WorkflowCommands.SeedCorporateLoanWorkflowHandler` updated with `CreditAnalysis→BranchReview Return/CreditOfficer` transition and corrected `CreditAnalysis→HOReview` to `Approve/CreditOfficer`; DB INSERT for new transition row. Build: 0 errors, 0 warnings. |
| 6.5 | 2026-04-03 | Factory AI | G11: Credit check retry gaps — residual issues after G6 outbox rewrite (Session 42). (1) Crash recovery: `RecoverOrphanedEntriesAsync()` added to `CreditCheckBackgroundService.ExecuteAsync` startup path — queries all `Processing` entries and resets to `Pending` before first poll; prevents entries from being permanently stuck after app crash mid-processing. (2) `ProcessedAt` set on terminal failure (`isFinalAttempt`) in both result-failure and exception paths of `ProcessEntryAsync` (previously only set on success). (3) `Failed` reports retryable via UI: `IBureauReportRepository.Delete` + `BureauReportRepository.Delete` added (same pattern as other repos); `ProcessLoanCreditChecksCommand` — builds `alreadyCountedBvns`/`alreadyCountedBusiness` from existing `Failed` reports, deletes them before retry loop, removes `Failed` from `existingBvns`/`hasExistingBusinessReport` (only `Completed`+`NotFound` remain "done"), adds `!alreadyCountedBvns.Contains(bvn)` guard to all three `RecordCreditCheckCompleted` call sites to prevent double-incrementing `CreditChecksCompleted` on retry. (4) `CanRerunCreditChecks` in `Detail.razor` expanded: `ConsentRequired || Failed` (was `ConsentRequired` only) — button now appears when SmartComply API failures have exhausted all automatic retries. Build: 0 errors, 0 warnings. |
| 6.4 | 2026-04-03 | Factory AI | G10: Committee deferral dual-status desync + no UI indicator (Session 41). Root cause: `CommitteeDecisionWorkflowHandler` Deferred case had bare `break` — `WorkflowInstance.CurrentStatus` set to HOReview but `LoanApplication.Status` remained CommitteeCirculation; no `DeferFromCommittee()` domain method existed. Fix: (1) `LoanApplication.DeferFromCommittee(userId, rationale)` added — validates CommitteeCirculation status, sets Status=HOReview, writes status history entry with rationale. (2) `CommitteeDecisionRecordedEvent` gains `string? Rationale = null` field; `CommitteeReview.RecordDecision()` passes rationale into event. (3) `CommitteeDecisionWorkflowHandler` Deferred case calls `DeferFromCommittee()` with failure guard. (4) `Detail.razor` yellow deferral banner at HOReview when `Committee.Decision == "Deferred"` — shows date and rationale inline (data already in `CommitteeInfo`, no new queries). No migration. Build: 0 errors (Domain + Infrastructure + Web.Intranet). |
| 6.3 | 2026-04-03 | Factory AI | Lifecycle gap fixes G4/G6/G7/G9 (Session 40). G6: `CreditCheckOutbox` persistent DB table replaces in-memory `Channel<CreditCheckRequest>`; `CreditCheckOutboxEntry` entity; `ICreditCheckOutbox`/`CreditCheckOutboxWriter` (enqueues atomically within `ApproveBranchHandler` SaveChangesAsync); `CreditCheckBackgroundService` rewritten to poll DB, claim entries, retry 3×; migration `20260402140707_AddCreditCheckOutbox`; `DependencyInjection.cs` updated. G7: `MoveToFinalApproval(userId)` added to `LoanApplication` domain; `CommitteeDecisionWorkflowHandler` auto-transitions CommitteeApproved→FinalApproval (system-driven); workflow seeder adds `FinalApproval` stage + transitions; `FinalApproveHandler` called from `ApplicationService.ApproveApplicationAsync`; UI gates on `FinalApprover` role. G9: `SystemConstants.SystemUserId = "00000000-0000-0000-0000-000000000001"` created in `CRMS.Domain.Constants`; `WorkflowIntegrationHandlers.cs` — `Guid.Empty` replaced with `domainEvent.DecisionByUserId` for all chairman-initiated actions (ApproveCommittee, RejectCommittee, workflow transitions for Approved/Rejected/Deferred) and `SystemConstants.SystemUserId` for system-driven transitions (AllCreditChecksCompleted→HOReview, CommitteeApproved→FinalApproval auto-transition); `ApplicationService` resolves `SystemConstants.SystemUserId` → "System Process" in workflow history display. G4: `BureauTab.razor` — yellow consent-blocked banner when any report is `ConsentRequired` (lists party names, explains action needed); business report card footer updated with `ConsentRequired`/`Failed`/`NotFound` status badges (was missing, individual cards already had them). Build: 0 errors (Infrastructure + Web.Intranet verified). |
| 6.2 | 2026-03-30 | Factory AI | Workflow save bugs fixed (Session 39). BUG-1: `WorkflowInstanceRepository.Update()` — unconditional `DbSet.Update()` on tracked entity marked existing `WorkflowTransitionLog` rows as `Modified` → UPDATE instead of INSERT → SaveChanges failed → "Failed to approve application" UI error. Fixed: applied `AutoDetectChangesEnabled=false/true` + detached/tracked branching (same pattern as `LoanApplicationRepository`). BUG-2: `AllCreditChecksCompletedWorkflowHandler` — called `TransitionAsync` + `_instanceRepository.Update()` but never called `_unitOfWork.SaveChangesAsync()` in the domain event handler scope → workflow never advanced to HOReview. Fixed: injected `IUnitOfWork`, added `SaveChangesAsync` after successful transition. BUG-3: `CommitteeDecisionWorkflowHandler` — same missing `SaveChangesAsync`. Fixed same way. BUG-4: `ReturnApplicationAsync` — status mapping wrong (`BranchReview→Draft` should be `→BranchReturned`; `HOReview→CreditAnalysis` should be `→BranchReview` per workflow seeder) + missing domain command for BranchReview returns. Fixed: corrected mapping + added `ReturnFromBranchHandler` call in fresh scope for BranchReview case. Build: 0 compiler errors. |
| 6.1 | 2026-03-30 | Factory AI | Submit for Review bug fixes (Session 38). BUG-1: `LoanApplicationRepository.Update()` — EF Core `AutoDetectChangesEnabled=true` caused `_context.Entry(application)` to run `DetectChanges()`, which found the new `LoanApplicationStatusHistory` entry (added by `application.Submit()`) in the tracked collection and marked it `Modified` instead of `Added` → EF generated `UPDATE` instead of `INSERT` → ROW_COUNT()=0 → `DbUpdateConcurrencyException`. Fixed by wrapping entire `Update()` body with `AutoDetectChangesEnabled=false/true` in a try/finally. BUG-2: `Detail.razor` submit success path — `StateHasChanged()` missing after `await LoadApplication()`, so Blazor did not re-render the page after a successful submit; status remained showing "Draft". Fixed by adding `StateHasChanged()` after `LoadApplication()`. Build: 0 errors. |
| 6.0 | 2026-03-29 | Factory AI | Bank statement "Add Transactions" UX fixes (Session 37). Part 1 — "Add Txns" on existing statement now shows prior transactions: `ShowManageStatementTransactionsModal` made async; loads `GetStatementTransactionsAsync` when `TransactionCount > 0`; passes as `ExistingTransactions` to modal; modal renders existing rows as read-only section with `BaseBalance` (last saved RunningBalance) as starting point for new rows; `ComputedClosing`/`ComputeRunningBalance`/`Save()` all use `BaseBalance`. Part 2 — 4 additional modal bugs: (1) `@bind` on Description/Reference changed to `@bind:event="oninput"` to prevent Blazor re-render from clobbering user's typed text; (2) `OnSuccess` now always invoked on `result.Success` (partial-save path no longer skips parent refresh); (3) "Not Reconciled" warning condition tightened to require at least one row with an entered amount; (4) "Total Credits"/"Total Debits" labels renamed to "New Credits"/"New Debits" when existing transactions present. Files changed: `Detail.razor`, `ManageStatementTransactionsModal.razor`. Build: not verified (UI-only, no DI changes). |
| 5.9 | 2026-03-28 | Factory AI | `DbUpdateConcurrencyException` bug fix in `AddTransactionsHandler`. Root cause: EF Core 9 generates a separate UPDATE for the null optional `OwnsOne(CashflowSummary)` using `= NULL` WHERE conditions (MySQL evaluates as UNKNOWN → 0 rows matched → exception). Three fixes: (1) `BankStatementConfiguration` — `builder.Navigation(x => x.CashflowSummary).IsRequired(false)` prevents EF from generating owned-entity null-check WHERE clauses; (2) `AddTransactionsHandler` — collect `result.Value` (new `StatementTransaction` entities) and call `_repository.AttachNewTransactions()` instead of `_repository.Update(statement)` — `BankStatement` already tracked so EF auto-detects scalar changes; (3) `IBankStatementRepository` + `BankStatementRepository` — `AttachNewTransactions` method added to interface and implementation. Build: 0 errors. |
| 5.8 | 2026-03-26 | Factory AI | Submit for Review end-to-end bug fixes (4 bugs). BUG-1: `BankStatementRepository.Update()` — EF Core `DbSet.Update()` graph traversal marks new `StatementTransaction` entities (non-empty Guid keys) as `Modified` not `Added` → silent no-op / DB exception → "Failed to add transactions". Fixed by capturing Detached transactions before `Update()`, re-marking as `Added` after. BUG-2: Two wrong-collection checks for bank statement validation — `ValidateForSubmission()` checked `application.Documents` (LoanApplicationDocument aggregate) instead of `application.BankStatements`; `LoanApplication.Submit()` made the same check. Both fixed: UI checks `BankStatements.Any()`; domain check removed; cross-aggregate validation moved to `SubmitLoanApplicationHandler` (injects `IBankStatementRepository`). BUG-3: `SubmitForReview()` had no else branch — command failure silently swallowed; added `submitError` field + alert in modal. BUG-4: `LoanApplicationRepository.Update()` same EF Core tracking bug — new `LoanApplicationStatusHistory` added by `AddStatusHistory()` tracked as `Modified` → DB exception → "failed to submit application". Fixed with same pattern as BUG-1; also covers Comments and Documents. Build: 0 errors. |
| 5.7 | 2026-03-25 | Factory AI | Offer letter download + history. `IOfferLetterRepository.GetAllByLoanApplicationIdAsync` added (domain interface + infrastructure implementation, ordered by version desc). New `CRMS.Application/OfferLetter/Queries/OfferLetterQueries.cs`: `GetOfferLettersByApplicationQuery` + `GetOfferLettersByApplicationHandler` returning `List<OfferLetterSummaryDto>`. DI: `GetOfferLettersByApplicationHandler` registered. `OfferLetterInfo` model added to `ApplicationModels.cs`. `ApplicationService`: `GetOfferLettersByApplicationAsync(Guid)` + `DownloadOfferLetterAsync(Guid offerLetterId)` (loads StoragePath from DB, streams via `IFileStorageService`). `Detail.razor`: offer letters loaded in `LoadApplication()` for Approved/Disbursed; "Offer Letters" tab (only visible when `CanGenerateOfferLetter`) with count badge; tab content: empty-state or full table (version badge, filename, size, status badge, generated-by, timestamp, per-row download with spinner); after generate: list auto-refreshes before auto-download; `DownloadOfferLetter(Guid)` handler with error feedback; `FormatFileSize` helper added. Build: 0 errors. |
| 5.6 | 2026-03-25 | Factory AI | `ManageStatementTransactionsModal` save button hardening: `ToString("N2")` → `ToString("F2", CultureInfo.InvariantCulture)` on debit/credit number inputs (comma-formatted strings silently discarded by browser → preloaded amounts showed as empty → `CanSave = false` → button permanently disabled); `StateHasChanged()` after `isSaving = true` (spinner now shows immediately); `catch (Exception ex)` block (unhandled exceptions now surface as error message instead of crashing Blazor circuit); `min-height: 0` on modal body div (prevents flex overflow clipping footer); disabled-state hint text below Save button. Single file changed: `ManageStatementTransactionsModal.razor`. Build: 0 errors. |
| 5.5 | 2026-03-25 | Factory AI | External bank statement transaction pipeline: `StatementFileParserService.cs` (new) — CSV+Excel auto-parsing, auto column detection, 18 date formats, 40+ column name aliases, ClosedXML; `ManageStatementTransactionsModal.razor` (new) — full transaction entry grid with live balance reconciliation, preload from parsed file, parse message banner; `AddTransactionsCommand` fixed to use `GetByIdWithTransactionsAsync` + calls `ValidateDataIntegrity()` after save; `UploadExternalStatementAsync` rewired to read file into `byte[]` once (upload + parse), returns `ApiResponse<StatementUploadResult>`; `AddStatementTransactionsAsync` (new); `StatementTransactionRow`/`StatementUploadResult`/`StatementParseResult` models added; `Program.cs` registers `StatementFileParserService` Singleton; `UploadExternalStatementModal` updated with `InputFile` + format guide panel; `StatementsTab` updated with `OnEnterTransactions` param + "Enter Txns" button + disabled Verify/Analyze when 0 transactions; `Detail.razor` auto-opens transaction modal after upload when rows parsed; `Help/Index.razor` `RenderTabStatements()` rewritten with 5 sections. Build: 0 errors. |
| 5.4 | 2026-03-22 | Factory AI | M-series + L-series bug fixes (all 19 M + all 8 L). Key changes: `AppStatus.cs` constants class (new) replaces all magic status strings in `Detail.razor`; `BankSettings.cs`/`CollateralHaircutSettings.cs` (new) for configurable defaults via `IOptions<T>`; `UpdateCollateralHandler`+`UpdateGuarantorHandler` (new) + DI registration; all 6 admin pages protected with `[Authorize(Roles="SystemAdmin")]`; `IAuditLogRepository.SearchAsync` extended with `searchTerm` through all 5 layers; balance sheet save validation; vote amount/tenor/rate range checks; FSV>MV guard; committee min-approval guard; dashboard demo data removed; pagination (page size 15) on all 4 admin list pages; Help search filters 40 nav items in real-time; `CommentsTab` loading/error state; calendar month diff fix in upload statement modal; `FinalDecision` on `CommitteeReviewSummaryDto`; `LocationId` on `UserSummaryDto`. Build: 0 errors. |
| 5.3 | 2026-03-22 | Factory AI | Bug fixes (TabModalReviewReport C6/C7/C8 + C-4/C-5/C-6): (C7) Settings persistence — `Settings/Index.razor` now uses `ILocalStorageService` to load/save all 9 preference fields; save/reset both persist to `localStorage["userSettings"]`. (C8) Audit trail pagination — `SearchAuditLogsAsync()` added to `ApplicationService` using `SearchAuditLogsHandler` (registered in DI); `Audit.razor` Previous/Next buttons wired, `totalCount`/`totalPages` from real backend. (C-4) Null-user auth guard — `EnsureAuthenticated(out Guid userId)` helper added to `Detail.razor`; all 15 workflow action methods guard against expired sessions and redirect to `/login`. (C-5) Collateral mapping corrected — `GetCollateralsForApplicationAsync` now calls `GetCollateralByIdHandler` per item to get full `CollateralDto`; `MarketValue`/`ForcedSaleValue` correctly populated. (C-6) LTV calculation — accepts `loanAmount` parameter; per-item LTV = `Math.Round((loanAmount / acceptableValue) * 100, 2)`. Build: 0 errors. |
| 5.2 | 2026-03-21 | Factory AI | Comprehensive scoring parameters seeder: `SeedScoringParametersAsync()` expanded from 12 basic parameters to 82 parameters across all 9 categories (Weights, CreditHistory, FinancialHealth, Cashflow, DSCR, Collateral, Recommendations, LoanAdjustments, StatementTrust). All parameters now visible and editable in `/admin/scoring` UI with proper min/max constraints and sort order. Existing databases need to clear `ScoringParameters` table and restart to re-seed. Consent flow reviewed and documented — current offline approach (paper forms) is acceptable; OTP-based verification can be added later. |
| 5.1 | 2026-03-21 | Factory AI | Critical migration bug fix: 4 hand-crafted EF Core migrations were missing `.Designer.cs` files, causing them to never apply. `IndustrySector` column missing from `LoanApplications` broke ALL loan queries — Detail page fell back to mock data, hiding Loan Pack and Offer Letter buttons. Created 4 Designer.cs files, updated `CRMSDbContextModelSnapshot.cs`, made rename and create-table migrations idempotent. All 4 migrations now apply cleanly on startup. |
| 5.0 | 2026-03-20 | Factory AI | Offer letter generation complete. `OfferLetter` aggregate (versioning, schedule summary). `GenerateOfferLetterHandler` orchestrates: load approved terms → calculate repayment schedule via `IFineractDirectService` (hybrid) → extract committee conditions → generate QuestPDF → store. `OfferLetterPdfGenerator` produces professional PDF with: facility details table, full repayment schedule (installment #, due date, principal, interest, total, outstanding), schedule summary box, numbered conditions, acceptance/signature section. UI: "Offer Letter" button on Detail page (Approved/Disbursed only). Help page: new "Offer Letter" section under Loan Process with full documentation. Operations role workflow updated. Migration `20260320110000_AddOfferLettersTable`. 4 DI registrations. Build: 0 errors, tests pass. |
| 4.9 | 2026-03-20 | Factory AI | Fineract Direct API integration for repayment schedule preview + customer exposure. **New domain interface:** `IFineractDirectService` (4 methods: `CalculateRepaymentScheduleAsync`, `GetClientAccountsAsync`, `GetLoanDetailAsync`, `GetCustomerExposureAsync`) with full domain records. **Auth:** `FineractDirectAuthHandler` — `POST /authentication` with username/password + `fineract-platform-tenantid` header, caches `base64EncodedAuthenticationKey`, SSL cert tolerance for self-signed certs. **Real service:** `FineractDirectService` — 3 Fineract endpoints (schedule calc, client accounts, loan detail); **hybrid approach** for schedule: tries Fineract API when `FineractProductId` is set, falls back to in-house financial math. **Mock service:** `MockFineractDirectService` — real PMT/flat/equal-principal calculations + mock client account data. **FineractProductId:** Added nullable `int?` to `LoanProduct` domain entity, DTOs, mappers, `UpdateLoanProductCommand`, Products admin page (`/admin/products` modal). Migration `20260320100000_AddFineractProductIdToLoanProduct`. **Customer exposure flow:** `clientDetails.id` (from existing middleware) → `GET /clients/{id}/accounts` → filter active loans → `GET /loans/{id}` per loan → aggregate outstanding. **Config:** `FineractDirect` section in both appsettings.json (BaseUrl, Username, Password, TenantId, UseMock). DI registered with UseMock toggle + retry policy. Build: 0 errors, tests pass. |
