# CollateralManagement Module

**Module ID:** 17  
**Status:** 🟢 Completed  
**Priority:** P1  
**Bounded Context:** Lending  
**Last Updated:** 2026-02-17

---

## 1. Purpose

Manage loan collateral/security including real estate, vehicles, equipment, cash deposits, and securities. Tracks valuations, lien perfection, insurance, and calculates Loan-to-Value (LTV) ratios.

---

## 2. Supported Collateral Types

| Type | Typical Haircut | Description |
|------|----------------|-------------|
| CashDeposit | 0% | Cash lien on deposit accounts |
| FixedDeposit | 5% | Term deposits with bank |
| TreasuryBills | 5% | Government securities |
| Bonds | 10% | Corporate/government bonds |
| RealEstate | 20% | Land, buildings, property |
| Vehicle | 30% | Cars, trucks, equipment |
| Stocks | 30% | Listed equities |
| Equipment | 40% | Machinery, plant |
| Inventory | 50% | Stock-in-trade |

---

## 3. Domain Model

### Collateral Aggregate

| Property | Type | Description |
|----------|------|-------------|
| CollateralReference | string | Unique ref (e.g., RE20260217...) |
| Type | enum | Asset category |
| Status | enum | Proposed→Valued→Approved→Perfected→Released |
| PerfectionStatus | enum | NotStarted→InProgress→Perfected |
| MarketValue | Money | Professional valuation |
| ForcedSaleValue | Money | Distress sale value (typically 70% of market) |
| HaircutPercentage | decimal | Risk discount applied |
| AcceptableValue | Money | MarketValue × (1 - Haircut) |
| LienType | enum | FirstCharge, SecondCharge, Pledge, etc. |
| LienReference | string | Registration number |
| IsInsured | bool | Insurance status |
| InsuredValue | Money | Coverage amount |

### CollateralValuation Entity

Tracks valuation history:
- ValuationType (Initial, Revaluation, ForcedSale)
- ValuerName, ValuerCompany, ValuerLicense
- ValuationReportReference
- ExpiryDate (typically 1 year)

### CollateralDocument Entity

Supporting documents:
- Title deeds, vehicle registration
- Valuation reports
- Insurance certificates
- Lien registration documents

---

## 4. Collateral Workflow

```
┌──────────┐    ┌────────────┐    ┌──────────┐    ┌───────────┐    ┌──────────┐
│ Proposed │───>│ Valuated   │───>│ Approved │───>│ Perfected │───>│ Released │
└──────────┘    └────────────┘    └──────────┘    └───────────┘    └──────────┘
     │               │                  │
     │               │                  └─── Rejected
     │               └─── Disputed
     └─── Rejected
```

---

## 5. API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/v1/collaterals | Add collateral to loan |
| GET | /api/v1/collaterals/{id} | Get collateral details |
| GET | /api/v1/collaterals/by-loan-application/{id} | Get all collaterals for loan |
| PUT | /api/v1/collaterals/{id}/valuation | Set/update valuation |
| POST | /api/v1/collaterals/{id}/approve | Approve collateral |
| POST | /api/v1/collaterals/{id}/perfection | Record lien perfection |
| GET | /api/v1/collaterals/ltv/{loanAppId}?loanAmount=X | Calculate LTV ratio |

---

## 6. LTV Calculation

Loan-to-Value ratio determines collateral adequacy:

```
LTV = (Loan Amount / Total Acceptable Collateral Value) × 100
```

| LTV Range | Assessment |
|-----------|------------|
| ≤ 50% | Excellent - Well secured |
| 51-70% | Good - Adequately secured |
| 71-80% | Acceptable - Marginally secured |
| 81-100% | Poor - Under-collateralized |
| > 100% | Critical - Significantly under-collateralized |

---

## 7. Lien Types

| Type | Description |
|------|-------------|
| FirstCharge | Primary security interest |
| SecondCharge | Subordinate to first charge |
| FloatingCharge | Over changing assets (inventory) |
| FixedCharge | Over specific identified assets |
| Pledge | Physical possession transferred |
| Hypothecation | Possession retained by borrower |
| Assignment | Transfer of receivables/contracts |

---

## 8. Database Tables

| Table | Description |
|-------|-------------|
| Collaterals | Main collateral records |
| CollateralValuations | Valuation history |
| CollateralDocuments | Supporting documents |

---

## 9. Integration Points

- **LoanApplication**: Linked via LoanApplicationId
- **Valuation Services**: Future integration with professional valuers
- **Land Registry**: Future integration for title verification
- **Insurance**: Track policy status and renewals

---

## 10. Future Enhancements

- [ ] Integration with land registry APIs
- [ ] Automated revaluation reminders
- [ ] Insurance expiry alerts
- [ ] Collateral substitution workflow
- [ ] Multi-currency collateral support
- [ ] Collateral pool management

---

## Document History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-17 | Initial implementation |
