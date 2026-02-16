# CoreBankingAdapter Module

**Module ID:** 3  
**Status:** 🟢 Completed (Mock Implementation)  
**Priority:** P1  
**Bounded Context:** CoreBanking  
**Last Updated:** 2026-02-16

---

## 1. Purpose

Provide an integration layer (anti-corruption layer) for the Fineract core banking system. Currently implemented with a mock provider for development and testing. The real Fineract client will be swapped in when ready for integration.

---

## 2. Implementation Summary

### Domain Layer (CRMS.Domain)

**Interface:** `Interfaces/ICoreBankingService.cs`

Operations:
- Customer: GetByAccountNumber, GetById, Create
- Corporate: GetCorporateInfo, GetDirectors, GetSignatories
- Account: GetAccountInfo, GetStatement, GetBalance
- Loan: Create, Approve, Disburse, GetInfo, GetSchedule, GetStatus

**Domain Models:**
- `CustomerInfo`, `CustomerType`, `CreateCustomerRequest`
- `CorporateInfo`, `DirectorInfo`, `SignatoryInfo`
- `AccountInfo`, `AccountStatement`, `StatementTransaction`
- `CreateLoanRequest`, `DisbursementRequest`, `LoanInfo`, `LoanStatus`
- `RepaymentSchedule`, `RepaymentInstallment`, `InstallmentStatus`

### Application Layer (CRMS.Application)

**DTOs:** `CoreBanking/DTOs/CoreBankingDtos.cs`
- `CustomerInfoDto`, `CorporateInfoDto`, `DirectorInfoDto`, `SignatoryInfoDto`
- `AccountInfoDto`, `AccountStatementDto`, `StatementTransactionDto`
- `CorporateAccountDataDto` - Aggregated corporate data

**Queries:** `CoreBanking/Queries/`
- `GetCorporateAccountDataQuery` - Fetches all corporate data in one call
- `GetAccountInfoQuery` - Get single account info
- `GetAccountStatementQuery` - Get account statement for date range

### Infrastructure Layer (CRMS.Infrastructure)

**Mock Provider:** `ExternalServices/CoreBanking/MockCoreBankingService.cs`
- Seeded with sample corporate and individual customers
- 3 directors, 2 signatories for corporate account
- Generates mock transactions for statement
- Generates repayment schedule for loans

**Settings:** `ExternalServices/CoreBanking/FineractSettings.cs`
- Configuration class for future Fineract integration

---

## 3. Mock Data

### Corporate Account: 1234567890
- Company: Acme Industries Ltd
- 3 Directors with BVN
- 2 Signatories (A and B mandate)
- NGN 50M balance

### Individual Account: 0987654321
- Customer: Oluwaseun Bakare
- NGN 2.5M balance

---

## 4. API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/v1/core-banking/corporate/{accountNumber} | Get all corporate account data |
| GET | /api/v1/core-banking/accounts/{accountNumber} | Get account info |
| GET | /api/v1/core-banking/accounts/{accountNumber}/statement | Get account statement |

---

## 5. Integration Pattern

```
┌─────────────────────┐
│   Application       │
│   (Use Cases)       │
└──────────┬──────────┘
           │ ICoreBankingService
           ▼
┌─────────────────────┐
│  MockCoreBanking    │  ◄── Current (Development)
│     Service         │
└─────────────────────┘
           │
           │ (Future: swap implementation)
           ▼
┌─────────────────────┐
│  FineractCoreBanking│  ◄── Future (Production)
│     Service         │
└─────────────────────┘
           │
           ▼
┌─────────────────────┐
│   Fineract API      │
└─────────────────────┘
```

---

## 6. Future Fineract Integration

When ready to integrate with real Fineract:

1. Implement `FineractCoreBankingService : ICoreBankingService`
2. Add HttpClient with Polly resilience policies
3. Map Fineract API responses to domain models
4. Configure via `FineractSettings`:
   - BaseUrl, TenantId, Credentials
   - Timeout, Retry policies
5. Swap registration in DI based on `UseMock` setting

---

## 7. Key Fineract Endpoints (Reference)

| Operation | Fineract Endpoint |
|-----------|-------------------|
| Get Client | GET /clients/{id} |
| Create Client | POST /clients |
| Get Savings | GET /savingsaccounts/{id} |
| Get Transactions | GET /savingsaccounts/{id}/transactions |
| Create Loan | POST /loans |
| Approve Loan | POST /loans/{id}?command=approve |
| Disburse Loan | POST /loans/{id}?command=disburse |

---

## 8. Pending Enhancements

- [ ] Implement FineractCoreBankingService for production
- [ ] Add retry/circuit breaker policies
- [ ] Add caching for customer/account data
- [ ] Add idempotency handling for loan operations
- [ ] Add comprehensive error mapping

---

## Document History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-16 | Initial mock implementation |
