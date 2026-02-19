# Audit Report: FinancialDocumentAnalyzer Module

**Module ID:** 9
**Audit Date:** 2026-02-17
**Auditor:** Domain Expert Review
**Module Status (Documented):** 🟢 Completed
**Audit Verdict:** ⚠️ Logic Issues — DSCR Calculation Requires Correction

---

## 1. Executive Summary

The FinancialDocumentAnalyzer is well-structured with a comprehensive set of financial ratios (20+) and a clear data entry workflow. The aggregate model correctly separates BalanceSheet, IncomeStatement, and CashFlowStatement as distinct entities. However, a significant bug exists in the DSCR calculation when the cash flow statement is absent, the balance sheet validation tolerance is too tight for large corporate volumes, and the `FinancialRatios` value object equality implementation is incomplete. Additionally, the module relies entirely on manual data entry — there is no automated PDF extraction.

---

## 2. Confirmed Bugs

### 2.1 DSCR Overestimated When Cash Flow Statement Is Absent (HIGH)

```csharp
var annualDebtService = inc.InterestExpense + (cf?.RepaymentOfBorrowings ?? 0);
ratios.DebtServiceCoverageRatio = SafeDivide(inc.EBITDA, annualDebtService);
```

When no cash flow statement is provided (`cf == null`), `RepaymentOfBorrowings` defaults to 0. This means the DSCR is calculated as:

```
DSCR = EBITDA / InterestExpense (only)
```

This omits principal repayments entirely, massively overstating the borrower's debt service capacity. A company with ₦10M EBITDA, ₦2M interest, and ₦8M principal repayments annually would show:
- Correct DSCR = 10 / (2 + 8) = **1.0x** (borderline)
- Calculated DSCR = 10 / 2 = **5.0x** (appears excellent)

**Impact:** Loans with inadequate cashflow coverage receive high DSCR scores, leading to incorrect `Approve` or `StrongApprove` recommendations.

**Recommendation:**
- If no cash flow statement is provided, the DSCR should be calculated using the income statement's financing costs (total debt service estimate from income statement)
- Alternatively, flag DSCR as unreliable and apply a penalty to the DebtServiceCapacity score in the AIAdvisoryEngine when CF data is missing
- Document clearly in the UI that cash flow statement improves DSCR accuracy

### 2.2 `FinancialRatios.GetEqualityComponents()` Incomplete (LOW-MEDIUM)

```csharp
protected override IEnumerable<object?> GetEqualityComponents()
{
    yield return CurrentRatio;
    yield return DebtToEquityRatio;
    yield return NetProfitMarginPercent;
    yield return ReturnOnEquity;
}
```

The value object has 20+ computed ratios, but equality is only checked on 4. This means two `FinancialRatios` instances with the same 4 values but different DSCR, InventoryTurnover, etc. would be considered equal.

**Impact:** Incorrect behavior if `FinancialRatios` is used in collections, dictionaries, or change detection.

**Recommendation:** Include all significant ratios in `GetEqualityComponents()`, or reconsider whether equality comparison on this value object is needed (it may be better to treat it as a read-only snapshot with no equality semantics).

### 2.3 Balance Sheet Validation Tolerance Too Tight (MEDIUM)

The module states:
> "Balance Check: Total Assets must equal Total Liabilities + Total Equity (±1 NGN tolerance)"

For large Nigerian corporations with statements in the billions (e.g., ₦50B total assets), rounding errors from auditors using thousands as the unit of reporting will produce discrepancies far exceeding ±1 NGN. For example, if figures are entered in ₦'000s, a ±1 unit difference equals ±₦1,000 actual, not ±₦1.

**Recommendation:** Make the tolerance configurable or proportional:
- `Tolerance = Max(1.0, TotalAssets × 0.0001)` (0.01% tolerance)
- Allow configuration via `ScoringConfiguration` or `appsettings.json`

---

## 3. Business Logic Issues

### 3.1 No Financial Year Overlap Detection

Multiple `FinancialStatement` records can be created for the same loan application and the same year (e.g., two "Audited 2024" statements). There is no domain validation preventing duplicate fiscal year entries.

**Recommendation:** In `CreateFinancialStatementHandler`, check for existing statements with the same `FiscalYear` and `StatementType` for the same `LoanApplicationId`.

### 3.2 Trend Analysis Uses Absolute Values, Not Normalized (MEDIUM)

The trend analysis compares revenue, profitability, etc. across years. If a company reports in different scales (₦'000s vs ₦'millions) across years, or if inflationary adjustments are not considered, the trend analysis will show false "growth" or "decline."

**Recommendation:**
- Document the required reporting scale (e.g., all values in NGN)
- Add UI validation that all years use the same reporting scale
- Consider flagging analysis where inter-year variance exceeds 500% as potentially a scaling error

### 3.3 `SafeDivide` Returns 0 on Zero Denominator (LOW-MEDIUM)

```csharp
private static decimal SafeDivide(decimal numerator, decimal denominator)
{
    if (denominator == 0) return 0;
    return Math.Round(numerator / denominator, 4);
}
```

Returning 0 when the denominator is 0 is ambiguous:
- `CurrentRatio = 0` when `CurrentLiabilities = 0` — this should mean "infinite" (excellent liquidity), not zero (terrible liquidity)
- `InventoryTurnover = 0` when `Inventory = 0` — this should mean "N/A" (service business), not zero

The assessment methods (`GetLiquidityAssessment()`, etc.) may incorrectly classify these as "Critical" or "Weak" when they should be "Excellent" or "N/A."

**Recommendation:**
- Use `decimal?` for ratios that may be undefined
- Return `null` when the denominator is zero
- Handle `null` ratios in assessment methods with "N/A" output

---

## 4. Input Method Gaps

### 4.1 Only Manual Entry Implemented

The `InputMethod` enum includes `ManualEntry`, `ExcelUpload`, `PdfExtraction`, and `ApiImport`. Currently only `ManualEntry` is implemented. For large corporate clients, manual entry of 3 years × 24+ balance sheet fields is error-prone and time-consuming.

**Recommendation (Phase 1.5):** Implement Excel template upload using a standardized format. Provide a downloadable template to clients.

### 4.2 No OCR for PDF Financial Statements

`PdfExtraction` is defined but not implemented. Financial statements are commonly provided as PDFs (including audited accounts). Without OCR, the system cannot extract data automatically.

---

## 5. Compliance Issues

### 5.1 No Auditor Verification

Financial statements are marked as "Audited" by the loan officer entering data, but there is no verification that the auditor details are valid:
- No integration with the Financial Reporting Council of Nigeria (FRCN) to verify auditor licensing
- No cross-check that the auditor name matches the signature on the uploaded PDF

**Recommendation:** Add a manual verification step where a credit officer confirms the auditor credentials, and flag unverified auditor details in the analysis output.

---

## 6. Recommendations Summary

| Priority | Item |
|----------|------|
| CRITICAL | Fix DSCR calculation — do not omit principal when CF statement is absent |
| HIGH | Widen balance sheet validation tolerance for large-value statements |
| HIGH | Prevent duplicate financial year entries for the same loan application |
| MEDIUM | Fix `FinancialRatios.GetEqualityComponents()` to include all ratios |
| MEDIUM | Use `decimal?` for `SafeDivide` to distinguish zero from undefined |
| MEDIUM | Add inflation/scale normalization guidance for trend analysis |
| LOW | Implement Excel template upload |
| LOW | Plan OCR extraction for PDF financial statements |
