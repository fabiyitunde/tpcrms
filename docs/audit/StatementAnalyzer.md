# Audit Report: StatementAnalyzer Module

**Module ID:** 6
**Audit Date:** 2026-02-17
**Auditor:** Domain Expert Review
**Module Status (Documented):** 🟢 Completed
**Audit Verdict:** ⚠️ Significant Functional Gaps — Core Feature Missing

---

## 1. Executive Summary

The StatementAnalyzer module has a well-structured domain model with good cashflow feature coverage (DSCR, volatility, salary detection, gambling flags). However, a critical functional gap exists: **PDF statement parsing is completely unimplemented**. The current API requires external parsing before loading transactions, which means the module cannot process the bank statements that loan officers upload. Keyword-based transaction categorization is also fragile for Nigerian bank statement formats, and several risk indicators may produce misleading results.

---

## 2. Critical Functional Gaps

### 2.1 PDF Statement Parsing Not Implemented (CRITICAL)

The module's stated purpose is to "parse bank statements." However, the current implementation requires transactions to be submitted already parsed via:

```
POST /api/v1/statements/{id}/transactions
```

For the corporate loan flow, loan officers upload PDF bank statements. There is no component that reads a PDF file and extracts transactions. This means the **end-to-end flow is broken** — a PDF is uploaded via `LoanApplicationDocument`, but nothing converts it into `StatementTransaction` records.

**Impact:** The AIAdvisoryEngine will have no cashflow data for scoring, the `CashflowStability` and `DebtServiceCapacity` scores will use defaults, and the loan pack PDF will show empty cashflow sections.

**Recommendation:**
- Phase 1: Implement a structured PDF parser for common Nigerian bank statement formats (GTBank, Access Bank, First Bank, Zenith Bank, UBA)
- Use a library such as PdfPig or iText7 for PDF text extraction, then apply regex-based parsers per bank format
- Consider partnering with a statement parsing service (e.g., Mono's statement parsing API) for Phase 1 speed-to-market

### 2.2 CSV/Excel Import Not Implemented

No CSV or Excel import pipeline exists. Many corporate clients have their own accounting systems that export statements in these formats. This is listed as a future enhancement but blocks some corporate clients.

### 2.3 No File Virus Scanning

Uploaded statement files are not scanned for malware. A malicious PDF could be uploaded and later executed if any server-side component attempts to process it.

**Recommendation:** Integrate a virus scanning step (e.g., ClamAV or a cloud scanning service) before processing any uploaded file.

---

## 3. Algorithmic Issues

### 3.1 DSCR Calculation May Be Incorrect

```
DSCR = (Income - Non-debt expenses) / Debt payments
```

The implementation of `DebtServiceCoverageRatio` in `CashflowSummary` may not correctly identify what constitutes "non-debt expenses." If loan repayment transactions are categorized as `LoanRepayment` (correct) but also appear in total debits, they might be double-counted in the denominator.

**Recommendation:** Clearly define and document the DSCR formula used in the code:
- Numerator: Average monthly net inflow (after all expenses except debt service)
- Denominator: Average monthly debt service (loan repayments only)
- Validate with sample calculations

### 3.2 Salary Detection Fragility

Salary detection uses keyword matching (`SALARY`, `PAYROLL`, `SAL/`). Nigerian bank statements have highly varied formats:
- `CRDT:TRANSFER/ACME-INDUSTRIES/JAN-SAL`
- `NGIP:ACH:PAYROLL:001234`
- `ONLINE TRANSFER FROM ACME INDUSTRIES PLC`

None of these would match the documented patterns with high confidence. The LLM fallback addresses this partially, but LLM calls add latency and cost.

**Recommendation:**
- Expand keyword patterns to cover Nigerian-specific formats
- Add unit tests with real (anonymized) Nigerian bank statement transaction descriptions
- Document the minimum confidence threshold for salary classification

### 3.3 Repeat Analysis Not Guarded

The `BankStatement` entity tracks `AnalysisStatus` but there is no guard in the domain preventing `Analyze()` from being called multiple times on the same statement. Re-running analysis overwrites the `CashflowSummary`, which could cause issues if different results are produced.

**Recommendation:** Guard re-analysis: if `AnalysisStatus == Completed`, require explicit re-analysis flag or create a new statement version.

### 3.4 Statement Period Validation Against Required Coverage

The analysis checks total months covered but does not validate:
- That the statement period is recent (e.g., within the last 12 months)
- That there are no large gaps within the statement period
- That multiple statements don't represent the same period (duplicate detection)

**Recommendation:** Add validation that:
- The most recent transaction is within the last 60 days
- No two statements for the same account overlap in date range

---

## 4. Risk Indicator Issues

### 4.1 Bounced Transaction Detection

"Bounced transactions" detection depends on keywords like "INSUFFICIENT FUNDS" or "RETURNED." Nigerian bank statement descriptions for failed transactions vary significantly by bank. Without bank-specific patterns, this indicator will be underreported.

### 4.2 Gambling Flag Coverage

The gambling keyword list includes `BET9JA` and `SPORTYBET` with 0.99 confidence, which is good for known Nigerian betting platforms. However, the list does not cover:
- `BETKING`, `MERRYBET`, `BANGBET`, `NAIJABET`, `LOTTO`
- International platforms accessible from Nigeria: `BETWAY`, `1XBET`, `22BET`

**Recommendation:** Maintain a comprehensive and regularly updated gambling keyword list. Consider a community-maintained Nigerian fintech keyword database.

---

## 5. LLM Integration Risks

### 5.1 Silent LLM Failure Not Alerting

When LLM categorization fails, the system falls back to keyword matching. This fallback is silent — no alert, no log entry visible to credit officers. If the LLM API key expires or quota is exhausted, all new statements will silently use keyword matching without notification.

**Recommendation:** Log LLM failures with a warning-level structured log that can trigger an alert.

### 5.2 LLM Categorization Has No Audit Trail

For compliance purposes, credit decisions must be explainable. If a transaction is categorized by LLM as "Gambling" and causes a rejection, the credit officer must be able to see why. LLM reasoning should be stored.

**Recommendation:** Store the `reason` field returned by the LLM alongside each transaction categorization.

---

## 6. Recommendations Summary

| Priority | Item |
|----------|------|
| CRITICAL | Implement PDF statement parsing pipeline for major Nigerian banks |
| HIGH | Add file virus scanning for uploaded statements |
| HIGH | Validate and document the DSCR formula with test cases |
| HIGH | Prevent duplicate analysis of already-completed statements |
| MEDIUM | Validate statement recency and gap detection |
| MEDIUM | Expand Nigerian-specific gambling and failed-transaction keywords |
| MEDIUM | Alert on LLM failure instead of silent fallback |
| LOW | Store LLM categorization reasoning for audit trail |
| LOW | Implement CSV/Excel import |
