# CorporateLoanInitiation Module

**Module ID:** 4  
**Status:** 🟢 Completed  
**Priority:** P1  
**Bounded Context:** LoanApplication  
**Last Updated:** 2026-02-16

---

## 1. Purpose

Enable loan officers to initiate corporate loan applications, automatically pull customer data from core banking, manage documents, and route applications through the branch approval workflow.

---

## 2. Domain Model

### LoanApplication Aggregate

**Core Entity:** `LoanApplication`

| Property | Type | Description |
|----------|------|-------------|
| ApplicationNumber | string | Unique reference (LA{date}{id}) |
| Type | enum | Corporate / Retail |
| Status | enum | Workflow state (20 states) |
| LoanProductId | Guid | Product reference |
| AccountNumber | string | Core banking account |
| CustomerId | string | Core banking customer ID |
| RequestedAmount | Money | Loan amount requested |
| RequestedTenorMonths | int | Loan duration |
| InterestRatePerAnnum | decimal | Interest rate |
| ApprovedAmount | Money? | Final approved amount |

### Child Entities

| Entity | Description |
|--------|-------------|
| LoanApplicationDocument | Uploaded files (statements, financials) |
| LoanApplicationParty | Directors, signatories, guarantors |
| LoanApplicationComment | Workflow comments |
| LoanApplicationStatusHistory | Audit trail of status changes |

### Workflow States

```
Draft → Submitted → DataGathering → BranchReview
                                  ↓
                    BranchApproved / BranchReturned / BranchRejected
                          ↓
                    CreditAnalysis → HOReview → CommitteeCirculation
                                                       ↓
                                    CommitteeApproved / CommitteeRejected
                                           ↓
                                    FinalApproval → Approved
                                                      ↓
                                    OfferGenerated → OfferAccepted → Disbursed → Closed
```

---

## 3. Use Cases (Commands)

| Command | Description |
|---------|-------------|
| InitiateCorporateLoan | Create new application, auto-pull directors/signatories |
| SubmitLoanApplication | Submit for review |
| ApproveBranch | Branch manager approves |
| ReturnFromBranch | Return to initiator with comments |
| UploadDocument | Add document to application |
| VerifyDocument | Mark document as verified |

---

## 4. Queries

| Query | Description |
|-------|-------------|
| GetLoanApplicationById | Full application with documents/parties |
| GetLoanApplicationByNumber | Lookup by application number |
| GetLoanApplicationsByStatus | Filter by workflow status |
| GetMyLoanApplications | Applications initiated by user |
| GetPendingBranchReview | Queue for branch approvers |

---

## 5. API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/v1/loan-applications/corporate | Initiate corporate loan |
| POST | /api/v1/loan-applications/{id}/submit | Submit application |
| POST | /api/v1/loan-applications/{id}/branch-approve | Branch approval |
| POST | /api/v1/loan-applications/{id}/branch-return | Return to initiator |
| POST | /api/v1/loan-applications/{id}/documents | Upload document |
| POST | /api/v1/loan-applications/{id}/documents/{docId}/verify | Verify document |
| GET | /api/v1/loan-applications/{id} | Get by ID |
| GET | /api/v1/loan-applications/by-number/{number} | Get by number |
| GET | /api/v1/loan-applications/by-status/{status} | Filter by status |
| GET | /api/v1/loan-applications/my-applications | My applications |
| GET | /api/v1/loan-applications/pending-branch-review | Branch queue |

---

## 6. Integration with CoreBanking

On loan initiation:
1. Fetch customer info by account number
2. Verify account is corporate type
3. Auto-pull directors from core banking
4. Auto-pull signatories from core banking
5. Populate parties in loan application

---

## 7. Database Tables

| Table | Description |
|-------|-------------|
| LoanApplications | Main application data |
| LoanApplicationDocuments | Uploaded files |
| LoanApplicationParties | Directors, signatories |
| LoanApplicationComments | Workflow comments |
| LoanApplicationStatusHistory | Audit trail |

---

## 8. Key Features

- **Auto Data Pull**: Directors and signatories pulled from Fineract on initiation
- **Document Management**: Upload, verify, reject documents
- **Status History**: Full audit trail of all status changes
- **Branch Routing**: Applications routed to branch for approval
- **Workflow Comments**: Comments tracked by category (Branch Return, etc.)

---

## 9. Domain Events

| Event | Triggered When |
|-------|----------------|
| LoanApplicationCreated | New application created |
| LoanApplicationSubmitted | Application submitted |
| LoanApplicationBranchApproved | Branch approval granted |
| LoanApplicationApproved | Final approval |
| LoanApplicationDisbursed | Loan disbursed |

---

## 10. Pending Enhancements

- [ ] Add HO review and committee approval commands
- [ ] Add offer generation and acceptance
- [ ] Add disbursement integration with core banking
- [ ] Add BVN verification for parties
- [ ] Add file storage integration (Azure Blob/S3)
- [ ] Add AI advisory integration

---

## Document History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-16 | Initial implementation with branch workflow |
