# CRMS - Credit Risk Management System
## Full Design Specification Document

**Version:** 1.1  
**Last Updated:** 2026-02-17  
**Status:** Implementation Phase

---

## 1. Executive Summary

CRMS is a Nigeria-focused, AI-augmented Credit Governance Platform integrated with Fineract-based core banking. The system supports two distinct lending paradigms:

- **Retail/Self-Service Loans**: Customer-initiated, automated decisioning, smaller ticket sizes
- **Corporate/Officer-Driven Loans**: Loan officer-initiated, AI-advisory decisioning, committee approval workflows, larger facilities

The platform scales from ₦50,000 microloans to ₦5B corporate facilities without architectural redesign.

---

## 2. Business Context

### 2.1 Problem Statement
Nigerian lending processes remain slow and subjective due to:
- Manual checks and incomplete data
- Inconsistent risk rules
- Non-standard documentation
- Lack of objective decisioning

### 2.2 Solution Goals
- Reduce manual intervention
- Standardize risk rules
- Improve turnaround time (TAT)
- Increase compliance/audit readiness
- Scale lending volumes safely

### 2.3 Success Metrics
- % of loans fully automated (retail)
- Average decision time
- Default rate vs baseline
- Fraud/identity mismatch detection rate
- Ops workload reduction

---

## 3. System Modes

### Mode A: Retail/Self-Service Loans

| Aspect | Description |
|--------|-------------|
| Initiator | Customer via web portal |
| Decisioning | Automated (Approve/Decline/Refer) |
| Ticket Size | Small to Medium |
| AI Role | Decision Authority |
| Workflow | Streamlined (Credit Officer → Risk Manager) |
| Turnaround | Minutes to Hours |

### Mode B: Corporate/Officer-Driven Loans

| Aspect | Description |
|--------|-------------|
| Initiator | Loan Officer via intranet portal |
| Decisioning | Committee-driven with AI advisory |
| Ticket Size | Medium to Large |
| AI Role | Decision Support Only |
| Workflow | Multi-stage (Branch → Head Office → Committee → Management) |
| Turnaround | Days to Weeks |
| Special Requirements | Director/Signatory credit checks, Audited financials, PDF loan pack |

---

## 4. Functional Requirements

### 4.1 Corporate Loan Flow

1. **Loan Officer Initiation**
   - Enter corporate account number, loan amount, interest rate details
   - System validates account via Core Banking API
   - Create LoanApplicationCase with status = Draft

2. **Automatic Core Banking Data Pull**
   - Corporate profile (name, registration, industry)
   - Directors with BVN information
   - Signatories with BVN information
   - 6-month account statement

3. **External Document Uploads**
   - Other bank statements (PDF/CSV)
   - Audited financials (3 years): Balance Sheet, Income Statement, Cash Flow

4. **Branch Level Approval**
   - Approve for Processing / Return for Editing / Reject
   - Full audit logging

5. **Credit Bureau Checks**
   - Run on each Director and Signatory
   - Integrations: FirstCentral, CreditRegistry, Mono (aggregator)
   - Output: Bureau score, delinquency records, active loans, legal flags

6. **AI Decision Support**
   - Generate Score Matrix
   - Suggest: Recommended amount, interest tier, repayment schedule, DSCR
   - Generate: Risk commentary, strengths/weaknesses, red flags
   - Output: Dashboard view + Exportable PDF

7. **Head Office/Committee Workflow**
   - Comment threads, voting, document attachment
   - Digital signatures
   - States: HOReview → CommitteeCirculation → FinalDecision

8. **PDF Loan Pack Generation**
   - Application summary, financial ratios, bureau reports
   - AI score matrix, committee comments, final decision page

9. **Final Approval & Disbursement**
   - Notification to branch
   - Core banking loan booking
   - Disbursement and repayment schedule sync

### 4.2 Retail Loan Flow

1. **Customer Application**
   - Multi-step form: Personal details, employment, income
   - Consent capture (credit bureau + data processing)
   - Statement upload or open banking connection

2. **Automated Processing**
   - KYC validation and duplicate detection
   - Credit bureau check
   - Statement analysis and cashflow features

3. **Decision Engine**
   - Hard rules (fail-fast)
   - Points-based scorecard
   - ML augmentation (Phase 2)
   - Output: Approve/Decline/Refer + reasons

4. **Workflow**
   - Auto-approved: Proceed to offer
   - Referred: Credit Officer review queue
   - Declined: Notification with reasons

5. **Offer & Disbursement**
   - Generate offer letter, e-sign acceptance
   - Create loan in Fineract, disburse

---

## 5. Non-Functional Requirements

### 5.1 Security
- Encryption in transit (TLS 1.3) and at rest (AES-256)
- Secrets management (Azure Key Vault / AWS Secrets Manager)
- Role-based access control (RBAC)
- Sensitive field encryption (BVN, bureau payloads)

### 5.2 Compliance
- Nigeria Data Protection Act (NDPA) alignment
- Consent stored with timestamp and text version
- Full audit trails for decisions, overrides, data access
- Separate raw bureau payload access (Compliance/Audit only)

### 5.3 Performance
- Decisions under 5 minutes (where external APIs respond)
- Target 99.9% availability
- Graceful degradation if bureau APIs down

### 5.4 Observability
- Structured logging with correlation IDs
- Immutable audit logs
- Decision explainability (reason codes)

### 5.5 Cross-Context Communication (Domain Events)

The system uses **domain events** for communication between bounded contexts where loose coupling is critical:

**Event-Driven Flows (Critical):**
- Workflow transitions → Audit logging
- Committee votes/decisions → Audit logging
- Configuration changes → Audit logging
- Loan application lifecycle → Audit logging

**Infrastructure:**
- `DomainEventPublishingInterceptor` dispatches events after EF Core SaveChanges
- `DomainEventDispatcher` routes events to registered handlers
- Handlers execute in new DI scope for isolation

**Design Decision:** Only critical cross-context flows use events. Other integrations use direct service calls for simplicity (MVP pragmatism).

---

## 6. External Integrations

### 6.1 Core Banking (Fineract)
- Customer/account creation
- Loan creation and disbursement
- Repayment schedule posting
- Status synchronization

### 6.2 Credit Bureaus (Nigeria)
- FirstCentral Credit Bureau API
- CreditRegistry AutoCred API
- Mono (aggregator option)

### 6.3 Statement Providers
- PDF/CSV upload with parsing (MVP)
- Open banking aggregators (Phase 2)

### 6.4 Notifications
- Email (SMTP/SendGrid)
- SMS (Africa's Talking / Termii)
- WhatsApp Business API (optional)

### 6.5 AI/LLM Services
- OpenAI API / Azure OpenAI
- Custom scoring models

---

## 7. User Roles

| Role | Retail Access | Corporate Access |
|------|--------------|------------------|
| Applicant (Customer) | Yes | No |
| Loan Officer | Limited | Full |
| Credit Officer | Yes | Yes |
| Risk Manager | Yes | Yes |
| Branch Approver | No | Yes |
| Head Office Reviewer | No | Yes |
| Committee Member | No | Yes |
| Final Approver | No | Yes |
| Operations | Yes | Yes |
| Admin | Yes | Yes |
| Auditor/Compliance | Read-only | Read-only |

---

## 8. Data Entities (High-Level)

### Core Entities
- Applicant / CustomerProfile
- LoanProduct
- LoanApplication / LoanApplicationCase
- ConsentRecord

### Corporate-Specific
- CorporateProfile
- Director
- Signatory
- FinancialStatement (audited accounts)

### Credit Assessment
- BureauRequest / BureauResponse
- IndividualCreditReport
- StatementTransaction / StatementSummary
- AIScoreMatrix
- DecisionResult

### Workflow
- WorkflowCase / CaseActivity
- CommitteeComment
- ApprovalRecord

### Operations
- DisbursementRecord
- LoanPackDocument
- NotificationLog
- AuditLog

---

## 9. Module List

| Module | Description | Documentation |
|--------|-------------|---------------|
| ProductCatalog | Loan products, eligibility rules, pricing | ProductCatalog.md |
| CustomerPortal | Self-service application for retail loans | CustomerPortal.md |
| CorporateLoanInitiation | Officer-driven corporate loan intake | CorporateLoanInitiation.md |
| CoreBankingAdapter | Fineract integration layer | CoreBankingAdapter.md |
| CreditBureauIntegration | Bureau checks orchestration | CreditBureauIntegration.md |
| StatementAnalyzer | Bank statement parsing and cashflow analytics | StatementAnalyzer.md |
| FinancialDocumentAnalyzer | Audited accounts parsing and ratio extraction | FinancialDocumentAnalyzer.md |
| DecisionEngine | Rules + Scorecard for retail loans | DecisionEngine.md |
| AIAdvisoryEngine | LLM-based decision support for corporate | AIAdvisoryEngine.md |
| WorkflowEngine | State management and approval routing | WorkflowEngine.md |
| CommitteeWorkflow | Multi-user committee approval process | CommitteeWorkflow.md |
| LoanPackGenerator | PDF generation for corporate loans | LoanPackGenerator.md |
| NotificationService | Email/SMS/WhatsApp notifications | NotificationService.md |
| IdentityService | Authentication and authorization | IdentityService.md |
| ReportingService | Dashboards and analytics | ReportingService.md |
| AuditService | Immutable audit logging | AuditService.md |

---

## 10. Phasing Recommendation

### Phase 1 (MVP)
- Product catalog and admin
- Corporate loan initiation flow
- Core banking integration (Fineract)
- Statement upload and basic parsing
- Credit bureau integration (one provider)
- AI advisory engine (basic)
- Branch approval workflow
- PDF loan pack generation

### Phase 2
- Retail self-service portal
- Automated decision engine
- Open banking statement aggregation
- Multiple bureau integrations
- ML model integration
- Committee workflow enhancements

### Phase 3
- Collections automation
- Fraud signals and device fingerprinting
- Employer verification integrations
- Advanced analytics and reporting

---

## 11. Related Documents

- [Implementation Tracker](ImplementationTracker.md) - Roadmap, architecture patterns, module status
- Module-specific documentation in `/docs/modules/` directory

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-16 | Factory AI | Initial design specification |
| 1.1 | 2026-02-17 | Factory AI | Added domain events for cross-context communication |
