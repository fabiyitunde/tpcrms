# RES-002: Duplicate Database Queries in GenerateLoanPackHandler

**Severity:** MEDIUM
**Category:** Performance / Code Quality
**File:** `src/CRMS.Application/LoanPack/Commands/GenerateLoanPackCommand.cs`
**Method:** `GenerateLoanPackHandler.Handle()`

---

## Description

`GenerateLoanPackHandler.Handle()` fetches four repositories twice within a single request:

| Repository                    | First fetch (in)            | Second fetch (lines 117–120) |
|-------------------------------|-----------------------------|-------------------------------|
| `_advisoryRepository`         | `BuildLoanPackDataAsync()`  | `GetLatestByLoanApplicationIdAsync()` |
| `_bureauRepository`           | `BuildLoanPackDataAsync()`  | `GetByLoanApplicationIdAsync()`       |
| `_collateralRepository`       | `BuildLoanPackDataAsync()`  | `GetByLoanApplicationIdAsync()`       |
| `_guarantorRepository`        | `BuildLoanPackDataAsync()`  | `GetByLoanApplicationIdAsync()`       |

The second set of calls is used exclusively to populate `loanPack.SetContentSummary()`:

```csharp
// Lines 117–120 — redundant second fetch
var advisory    = await _advisoryRepository.GetLatestByLoanApplicationIdAsync(request.LoanApplicationId, ct);
var bureauReports = await _bureauRepository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
var collaterals = await _collateralRepository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
var guarantors  = await _guarantorRepository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);

loanPack.SetContentSummary(
    advisory?.RecommendedAmount,
    overallScore,
    riskRating,
    loanApp.Parties.Count(p => p.PartyType == PartyType.Director),
    bureauReports.Count,
    collaterals.Count,
    guarantors.Count);
```

These data were already fetched (in parallel) inside `BuildLoanPackDataAsync()`. The first fetch result is consumed to build the PDF; the second fetch repeats the exact same queries to produce counts and the advisory recommended amount.

---

## Impact

- **4 unnecessary database round-trips** per loan pack generation request
- For a loan application with large collateral and guarantor datasets these are non-trivial reads
- As loan pack generation is already a heavy synchronous operation (building a PDF), adding avoidable DB latency compounds response time

---

## Root Cause

`BuildLoanPackDataAsync()` maps its results to DTO types (`BureauReportData`, `CollateralData`, etc.) before returning, discarding the raw domain objects. The `Handle()` method then needs the raw domain objects for `SetContentSummary()`, so it re-fetches from the database rather than retaining the first results.

---

## Recommended Fix

Refactor `Handle()` to capture the raw results from `BuildLoanPackDataAsync()` before mapping, or return a tuple/intermediate object from `BuildLoanPackDataAsync()` that includes both the DTO (`LoanPackData`) and the raw counts/advisory needed for `SetContentSummary()`.

**Simplest fix — extend `LoanPackData` to carry the summary counts:**

Add summary fields directly to `LoanPackData` (or a companion object) and populate them inside `BuildLoanPackDataAsync()` where the data already exist:

```csharp
// Inside BuildLoanPackDataAsync() — data already loaded, add counts to return value
var contentSummary = new LoanPackContentSummary(
    advisory?.RecommendedAmount,
    advisory != null ? (int)advisory.OverallScore : (int?)null,
    advisory?.OverallRating.ToString(),
    directors.Count,
    bureauReports.Count,
    collaterals.Count,
    guarantors.Count
);

return (packData, contentSummary);  // return both from BuildLoanPackDataAsync
```

Then in `Handle()`:

```csharp
var (packData, contentSummary) = await BuildLoanPackDataAsync(...);
var pdfBytes = await _pdfGenerator.GenerateAsync(packData, ct);
// ...
loanPack.SetContentSummary(
    contentSummary.RecommendedAmount,
    contentSummary.OverallScore,
    contentSummary.RiskRating,
    contentSummary.DirectorCount,
    contentSummary.BureauReportCount,
    contentSummary.CollateralCount,
    contentSummary.GuarantorCount);
```

This eliminates all four redundant queries with no change to observable behaviour.

---

## Acceptance Criteria

- `Handle()` does not call `_advisoryRepository`, `_bureauRepository`, `_collateralRepository`, or `_guarantorRepository` after `BuildLoanPackDataAsync()` returns
- `SetContentSummary()` is populated with the same data already fetched during pack building
- No regression in the counts or amounts passed to `SetContentSummary()`
