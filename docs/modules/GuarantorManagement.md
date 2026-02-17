# GuarantorManagement Module

**Module ID:** 18  
**Status:** 🟢 Completed  
**Priority:** P1  
**Bounded Context:** Lending  
**Last Updated:** 2026-02-17

---

## 1. Purpose

Manage loan guarantors including individuals and corporates. Supports full credit bureau checks on guarantors, net worth verification, and guarantee agreement tracking. Ensures guarantors are creditworthy and not over-extended.

---

## 2. Guarantor Types

| Type | Description |
|------|-------------|
| Individual | Personal guarantor |
| Corporate | Company as guarantor |
| Director | Company director guaranteeing corporate loan |
| Shareholder | Shareholder providing guarantee |
| ThirdParty | Unrelated third-party guarantor |
| Government | Government/agency guarantee |

---

## 3. Guarantee Types

| Type | Description |
|------|-------------|
| Unlimited | Full liability for loan amount |
| Limited | Capped at specific amount |
| Joint | Multiple guarantors share liability |
| JointAndSeveral | Each guarantor liable for full amount |
| Continuing | Covers future advances |

---

## 4. Domain Model

### Guarantor Aggregate

| Property | Type | Description |
|----------|------|-------------|
| GuarantorReference | string | Unique ref (e.g., GR20260217...) |
| Type | enum | Individual/Corporate/Director/etc. |
| Status | enum | Proposed→CreditCheckCompleted→Approved→Active |
| GuaranteeType | enum | Unlimited/Limited/Joint/etc. |
| FullName | string | Guarantor name |
| BVN | string | Bank Verification Number |
| DeclaredNetWorth | Money | Self-declared net worth |
| VerifiedNetWorth | Money | Bank-verified net worth |
| GuaranteeLimit | Money | Maximum guarantee amount |
| IsUnlimited | bool | Unlimited guarantee flag |
| CreditScore | int | Bureau credit score |
| CreditScoreGrade | string | A+/A/B/C/D/E |
| HasCreditIssues | bool | NPLs, delinquencies, legal issues |
| ExistingGuaranteeCount | int | Active guarantees elsewhere |
| TotalExistingGuarantees | Money | Total outstanding guarantees |

### GuarantorDocument Entity

Supporting documents:
- ID documents
- Net worth statement
- Signed guarantee agreement
- Board resolution (for corporate)

---

## 5. Guarantor Workflow

```
┌──────────┐    ┌─────────────────┐    ┌──────────┐    ┌────────┐    ┌──────────┐
│ Proposed │───>│ CreditCheckDone │───>│ Approved │───>│ Active │───>│ Released │
└──────────┘    └─────────────────┘    └──────────┘    └────────┘    └──────────┘
     │                   │                   │
     │                   │                   └─── Rejected (credit issues)
     │                   └─── Rejected (score too low)
     └─── PendingVerification
```

---

## 6. Credit Check Integration

When `RunCreditCheck` is called:

1. Search bureau by BVN
2. Retrieve full credit report
3. Analyze for issues:
   - Non-performing loans (NPL > 0)
   - Max delinquency > 60 days
   - Legal actions/litigation
4. Store bureau report linked to guarantor
5. Count existing active guarantees
6. Update guarantor status

---

## 7. API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/v1/guarantors/individual | Add individual guarantor |
| GET | /api/v1/guarantors/{id} | Get guarantor details |
| GET | /api/v1/guarantors/by-loan-application/{id} | Get guarantors for loan |
| GET | /api/v1/guarantors/by-bvn/{bvn} | Get guarantor history by BVN |
| POST | /api/v1/guarantors/{id}/credit-check | Run credit bureau check |
| POST | /api/v1/guarantors/{id}/approve | Approve guarantor |
| POST | /api/v1/guarantors/{id}/reject | Reject guarantor |

---

## 8. Credit Assessment Criteria

| Factor | Threshold | Action |
|--------|-----------|--------|
| Credit Score | < 550 | Automatic rejection |
| Non-Performing Loans | > 0 | Flag for review |
| Max Delinquency | > 90 days | Flag for review |
| Legal Actions | Any | Flag for review |
| Existing Guarantees | > 3 | Verify capacity |

---

## 9. Director/Shareholder as Guarantor

For corporate loans, directors and shareholders often guarantee:

- **IsDirector**: Indicates board member status
- **IsShareholder**: Indicates equity ownership
- **ShareholdingPercentage**: % ownership stake

Directors with >25% shareholding typically required to guarantee.

---

## 10. Database Tables

| Table | Description |
|-------|-------------|
| Guarantors | Main guarantor records |
| GuarantorDocuments | Supporting documents |

---

## 11. Integration Points

- **CreditBureauIntegration**: Bureau checks via ICreditBureauProvider
- **BureauReportRepository**: Stores credit reports
- **LoanApplication**: Linked via LoanApplicationId
- **IdentityService**: User who approved/rejected

---

## 12. Future Enhancements

- [ ] Corporate guarantor with CAC integration
- [ ] Automated net worth verification
- [ ] Guarantee exposure reports
- [ ] E-signature for guarantee agreements
- [ ] Guarantor risk scoring model
- [ ] Cross-default monitoring

---

## Document History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-17 | Initial implementation with credit check |
