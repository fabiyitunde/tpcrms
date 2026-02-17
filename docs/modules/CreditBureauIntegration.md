# CreditBureauIntegration Module

**Module ID:** 5  
**Status:** 🟢 Completed  
**Priority:** P1  
**Bounded Context:** CreditAssessment  
**Last Updated:** 2026-02-17

---

## 1. Purpose

Orchestrate credit bureau checks against Nigerian credit bureaus (CreditRegistry, FirstCentral, CRC). Retrieve credit reports, SMARTScores, and account history for individuals and businesses.

---

## 2. Supported Providers

| Provider | Status | API |
|----------|--------|-----|
| CreditRegistry | Implemented | AutoCred API v8 |
| FirstCentral | Planned | - |
| CRC | Planned | - |

---

## 3. Domain Model

### BureauReport Aggregate

| Property | Type | Description |
|----------|------|-------------|
| Provider | enum | CreditRegistry/FirstCentral/CRC |
| SubjectType | enum | Individual/Business |
| Status | enum | Pending/Processing/Completed/Failed/NotFound |
| BVN | string | Bank Verification Number |
| RegistryId | string | Bureau-specific ID |
| CreditScore | int? | SMARTScore (300-850) |
| ScoreGrade | string | A+/A/B/C/D/E |
| TotalAccounts | int | Total credit accounts |
| PerformingAccounts | int | Current accounts |
| NonPerformingAccounts | int | Delinquent accounts |
| MaxDelinquencyDays | int | Worst delinquency |
| HasLegalActions | bool | Litigation/foreclosure |

### BureauAccount Entity

Tracks individual credit accounts:
- Account number, creditor, type
- Status (Performing/NonPerforming/WrittenOff/Closed)
- DelinquencyLevel (Current to Over360Days)
- CreditLimit, Balance
- PaymentProfile (12-month history)
- LegalStatus

### BureauScoreFactor Entity

Score factors explaining credit score:
- FactorCode, Description
- Impact (Positive/Negative/Neutral)
- Rank

---

## 4. CreditRegistry AutoCred API v8

### Authentication Flow

```
1. POST /api/Login
   - SubscriberID, AgentUserID, Password
   - Returns: SessionCode (valid 30 min)

2. Use SessionCode in all subsequent requests
```

### Key Endpoints

| Endpoint | Product | Description |
|----------|---------|-------------|
| POST /api/FindSummary | 8100 | Search by BVN/Name/TaxID |
| POST /api/FindDetail | 8102 | Detailed search |
| POST /api/GetReport200 | 8200 | Account data + PDF |
| POST /api/GetReport201 | 8201 | Account data + SMARTScore |
| POST /api/GetReport202 | 8202 | SMARTScore only |

### Typical Flow

```
1. Login -> SessionCode
2. FindSummary(BVN) -> RegistryID
3. GetReport201(RegistryID) -> Full credit report
```

---

## 5. API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/v1/credit-bureau/reports | Request credit report |
| GET | /api/v1/credit-bureau/reports/{id} | Get report by ID |
| GET | /api/v1/credit-bureau/reports/by-loan-application/{id} | Get reports for loan |
| GET | /api/v1/credit-bureau/search?bvn={bvn} | Search bureau by BVN |
| POST | /api/v1/credit-bureau/loan-applications/{id}/process | Process all checks synchronously (manual) |
| POST | /api/v1/credit-bureau/loan-applications/{id}/queue | Queue checks for background processing |

---

## 6. Mock Data

For development/testing, mock provider includes:

| BVN | Name | Score | Grade | Notes |
|-----|------|-------|-------|-------|
| 22234567890 | John Adebayo | 720 | A | Clean record |
| 22234567891 | Amina Ibrahim | 650 | B | 1 non-performing |
| 22234567892 | Chukwuma Okonkwo | 580 | C | Written-off account |
| 22212345678 | Oluwaseun Bakare | 780 | A+ | Closed accounts only |

---

## 7. Configuration

```json
{
  "CreditRegistry": {
    "BaseUrl": "https://api.creditregistry.com/nigeria/AutoCred/v8",
    "SubscriberId": "YOUR_SUBSCRIBER_ID",
    "AgentUserId": "YOUR_AGENT_ID",
    "TimeoutSeconds": 60,
    "UseMock": true
  }
}
```

Set `UseMock: false` for production with real credentials.

---

## 8. Payment Profile Decoding

CreditRegistry uses a 12-character string for payment history:

| Digit | Meaning |
|-------|---------|
| 0 | Current (Performing) |
| 1 | < 30 days late |
| 2 | 30-60 days late |
| 3 | 61-90 days late |
| 4 | 91-120 days late |
| 5 | 121-150 days late |
| 6 | 151-180 days late |
| 7 | 181-360 days late |
| 8 | > 360 days late |
| N | No data |

Example: `000000111222` = Current for 6 months, then late

---

## 9. Database Tables

| Table | Description |
|-------|-------------|
| BureauReports | Main report data |
| BureauAccounts | Individual credit accounts |
| BureauScoreFactors | Score explanation factors |

---

## 10. Integration with LoanApplication

Bureau reports are linked via `LoanApplicationId`:
1. When branch approves a loan, credit checks are **automatically queued**
2. Background service processes all directors/signatories/guarantors in parallel
3. Loan status moves to `CreditAnalysis` during processing
4. When all checks complete, loan is ready for `HOReview`
5. Query all reports for a loan: `GET /reports/by-loan-application/{id}`

### Automatic Credit Check Workflow

```
Branch Approves Loan
        │
        ▼
┌───────────────────┐
│ Queue Credit      │  ← Happens automatically
│ Checks (Async)    │
└───────────────────┘
        │
        ▼
┌───────────────────┐
│ Background        │
│ Service Processes │
│ All Parties       │
└───────────────────┘
        │
        ▼
┌───────────────────┐
│ Update Loan       │
│ Status & Track    │
│ Completion        │
└───────────────────┘
        │
        ▼
Ready for HOReview
```

---

## 11. Future Enhancements

- [ ] Add FirstCentral integration
- [ ] Add CRC integration
- [ ] Add consent verification before checks
- [ ] Add retry/circuit breaker policies
- [ ] Add caching for recent reports
- [ ] Add batch bureau check for multiple BVNs
- [ ] Encrypt raw bureau responses

---

## Document History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-17 | Initial implementation with CreditRegistry |
