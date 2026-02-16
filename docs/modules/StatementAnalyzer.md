# StatementAnalyzer Module

**Module ID:** 6  
**Status:** 🟢 Completed  
**Priority:** P1  
**Bounded Context:** CreditAssessment  
**Last Updated:** 2026-02-16

---

## 1. Purpose

Parse bank statements, categorize transactions, and extract cashflow analytics for credit assessment. Supports salary detection, recurring obligation identification, and risk indicator calculation.

---

## 2. Domain Model

### BankStatement Aggregate

| Property | Type | Description |
|----------|------|-------------|
| AccountNumber | string | Bank account number |
| AccountName | string | Account holder name |
| BankName | string | Financial institution |
| PeriodStart/End | DateTime | Statement period |
| OpeningBalance | decimal | Starting balance |
| ClosingBalance | decimal | Ending balance |
| AnalysisStatus | enum | Pending/Processing/Completed/Failed |
| CashflowSummary | ValueObject | Analysis results |

### StatementTransaction Entity

| Property | Type | Description |
|----------|------|-------------|
| Date | DateTime | Transaction date |
| Description | string | Raw description |
| NormalizedDescription | string | Uppercase, trimmed |
| Amount | decimal | Transaction amount |
| Type | enum | Credit/Debit |
| Category | enum | Categorized type |
| CategoryConfidence | decimal | 0-1 confidence score |
| IsRecurring | bool | Recurring pattern detected |

### CashflowSummary Value Object

**Flow Totals:**
- TotalCredits, TotalDebits, NetCashflow
- CreditCount, DebitCount
- AverageMonthlyCredits/Debits/Balance

**Salary Detection:**
- DetectedMonthlySalary
- HasRegularSalary
- SalaryPayDay
- SalarySource

**Obligations:**
- TotalMonthlyObligations
- DetectedLoanRepayments
- DetectedRentPayments
- DetectedUtilityPayments

**Risk Indicators:**
- GamblingTransactionsTotal/Count
- BouncedTransactionCount
- DaysWithNegativeBalance
- LowestBalance, HighestBalance
- BalanceVolatility, IncomeVolatility

**Ratios:**
- CreditToDebitRatio
- DebtServiceCoverageRatio (DSCR)
- DisposableIncomeRatio

---

## 3. Transaction Categories

### Income Categories
- Salary, BusinessIncome, Investment
- LoanInflow, TransferIn, Reversal, OtherIncome

### Expense Categories
- LoanRepayment, RentOrMortgage, Utilities
- Gambling (red flag), Entertainment
- TransferOut, BankCharges, CardPayment
- CashWithdrawal, OtherExpense

---

## 4. Domain Services

### TransactionCategorizationService

Keyword-based categorization with confidence scores:

| Pattern | Category | Confidence |
|---------|----------|------------|
| SALARY, PAYROLL | Salary | 0.90-0.95 |
| LOAN REPAY, EMI | LoanRepayment | 0.85-0.95 |
| BET9JA, SPORTYBET | Gambling | 0.99 |
| RENT, LANDLORD | RentOrMortgage | 0.85-0.95 |
| ELECTRIC, PHCN | Utilities | 0.90-0.95 |
| ATM, WITHDRAWAL | CashWithdrawal | 0.85-0.95 |

### CashflowAnalysisService

Calculates:
- Monthly totals and averages
- Salary detection (regularity, pay day, source)
- Volatility metrics (coefficient of variation)
- Days with negative balance
- DSCR = (Income - Non-debt expenses) / Debt payments

---

## 5. API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/v1/statements | Upload statement metadata |
| POST | /api/v1/statements/{id}/transactions | Add parsed transactions |
| POST | /api/v1/statements/{id}/analyze | Run analysis pipeline |
| GET | /api/v1/statements/{id} | Get statement with summary |
| GET | /api/v1/statements/{id}/transactions | Get all transactions |
| GET | /api/v1/statements/by-loan-application/{id} | Get statements for loan |

---

## 6. Analysis Pipeline

```
1. Upload Statement Metadata
   ↓
2. Add Transactions (parsed externally or from API)
   ↓
3. Trigger Analysis
   ├─ Categorize each transaction
   ├─ Detect salary patterns
   ├─ Calculate monthly totals
   ├─ Calculate volatility metrics
   ├─ Identify risk indicators
   └─ Generate CashflowSummary
   ↓
4. Return Analysis Result
   ├─ Statement with CashflowSummary
   ├─ Category breakdown
   ├─ Red flags list
   └─ Positive indicators list
```

---

## 7. Red Flags Detected

| Flag | Condition |
|------|-----------|
| Gambling activity | Any gambling transactions |
| Negative balance | >5 days with negative balance |
| Bounced transactions | Failed/insufficient funds |
| High income volatility | CoV > 50% |
| High balance volatility | CoV > 100% |
| Low DSCR | < 1.2x |
| Low disposable income | < 20% of income |

---

## 8. Positive Indicators

| Indicator | Condition |
|-----------|-----------|
| Regular salary | Detected in 70%+ of months |
| No negative balance | Zero days negative |
| Positive net cashflow | Credits > Debits |
| Strong DSCR | >= 2.0x |
| Healthy credit/debit ratio | > 1.1x |
| Stable income | Volatility < 20% |

---

## 9. Database Tables

| Table | Description |
|-------|-------------|
| BankStatements | Statement metadata + cashflow summary |
| StatementTransactions | Individual transactions |

---

## 10. Future Enhancements

- [ ] PDF statement parsing (OCR integration)
- [ ] CSV/Excel import
- [ ] Open Banking integration (Mono, Okra)
- [ ] ML-based transaction categorization
- [ ] Recurring pattern detection
- [ ] Multi-account aggregation

---

## Document History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-16 | Initial implementation |
