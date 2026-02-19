# Audit Report: CollateralManagement Module

**Module ID:** 7 (Listed as Module ID 17 in module doc — numbering discrepancy with Implementation Tracker)
**Audit Date:** 2026-02-17
**Auditor:** Domain Expert Review
**Module Status (Documented):** 🟢 Completed
**Audit Verdict:** ⚠️ Bugs Present — Several Domain Logic Errors

---

## 1. Executive Summary

The CollateralManagement module has good coverage of collateral types, haircut calculations, LTV ratios, and perfection status tracking. However, there are several semantic bugs in the aggregate (notably `ApprovedByUserId` being used for rejections), a property hiding issue with `CreatedAt` that could cause EF Core mapping problems, and important business logic gaps such as missing multi-collateral LTV aggregation and no collateral revaluation workflow.

---

## 2. Confirmed Bugs

### 2.1 `Reject()` Stores Rejector in `ApprovedByUserId` (MEDIUM-HIGH)

```csharp
public Result Reject(Guid rejectedByUserId, string reason)
{
    Status = CollateralStatus.Rejected;
    ApprovedByUserId = rejectedByUserId;  // BUG: semantic error
    ApprovedAt = DateTime.UtcNow;
    RejectionReason = reason;
    ...
}
```

The `Reject()` method stores the rejecting user in `ApprovedByUserId` and the rejection timestamp in `ApprovedAt`. This means after a rejection, the audit trail is corrupted: the entity appears to have been both approved (by the rejector) and rejected simultaneously.

**Recommendation:**
- Add separate fields: `RejectedByUserId` (Guid?) and `RejectedAt` (DateTime?)
- Alternatively, use a single `DecisionByUserId` / `DecisionAt` / `DecisionType` pattern

### 2.2 `CreatedAt` Property Hides Base Class Property (MEDIUM)

```csharp
public new DateTime CreatedAt { get; private set; }
```

The `new` keyword shadows the `CreatedAt` property from the base `Entity` or `AggregateRoot` class. EF Core may map to either the base or derived property depending on configuration, leading to one of them always being empty/default. This is a subtle but potentially serious persistence bug.

**Recommendation:** Remove the `new DateTime CreatedAt` override. Set the base class `CreatedAt` property in the constructor, or expose it via a protected setter in the base class. Never shadow inherited properties with `new`.

### 2.3 `Release()` Resets `PerfectionStatus` to `NotStarted` (LOW-MEDIUM)

```csharp
public Result Release(string reason)
{
    Status = CollateralStatus.Released;
    Notes = reason;
    PerfectionStatus = PerfectionStatus.NotStarted;  // Logically incorrect
    ...
}
```

When collateral is released, setting `PerfectionStatus = NotStarted` implies the perfection process hasn't started, which is historically inaccurate. A released lien was previously perfected; the correct state should be `Released` (if the enum supports it) or the status should remain `Perfected` while `CollateralStatus` indicates release.

**Recommendation:** Either add a `Released` value to the `PerfectionStatus` enum, or leave `PerfectionStatus` unchanged when releasing collateral (the release is captured by `CollateralStatus`).

---

## 3. Business Logic Gaps

### 3.1 `CalculateLTV()` Is Per-Collateral, Not Aggregate (HIGH)

```csharp
public decimal CalculateLTV(Money loanAmount)
{
    if (AcceptableValue == null || AcceptableValue.Amount == 0)
        return 100;

    return (loanAmount.Amount / AcceptableValue.Amount) * 100;
}
```

This method computes LTV as `LoanAmount / SingleCollateralAcceptableValue`. For a loan backed by multiple collaterals, this will always show a high (bad) LTV for each individual piece, rather than the aggregate LTV across all collateral.

The correct formula (shown in the module documentation) is:
```
LTV = (Loan Amount / Total Acceptable Collateral Value across all collaterals) × 100
```

**Impact:** The AIAdvisoryEngine uses LTV for the CollateralCoverage score. Using per-collateral LTV will understate coverage and produce incorrect scoring.

**Recommendation:** Move aggregate LTV calculation to the Application layer (or a domain service) that loads all collaterals for a loan and sums `AcceptableValue` before dividing by loan amount. The per-collateral method should be removed or clearly marked as internal-only.

### 3.2 Haircut Stored as Whole Percentage (e.g., `20` for 20%)

`HaircutPercentage` is stored as a raw decimal like `20` (representing 20%), and the `AcceptableValue` formula correctly divides by 100:
```csharp
AcceptableValue = Money.Create(marketValue.Amount * (1 - HaircutPercentage / 100), ...)
```

However, if any other part of the codebase treats `HaircutPercentage` as a fractional value (e.g., `0.20`), the calculation will be dramatically wrong.

**Recommendation:** Document clearly that `HaircutPercentage` is stored as a percentage (e.g., 20.0 = 20%). Add a validation that `0 <= HaircutPercentage <= 100`.

### 3.3 No Validation That `HaircutPercentage` Is Within Bounds

`SetValuation()` accepts an optional `haircutPercentage` parameter but does not validate that it is between 0 and 100. A negative haircut (boosting value beyond market value) or a haircut > 100 (negative acceptable value) is logically invalid.

### 3.4 No Collateral Revaluation Workflow

Collateral valuations expire annually (`NextRevaluationDue = DateTime.UtcNow.AddYears(1)`). However:
- There is no background service or alert to flag expired valuations
- There is no `Revalue()` domain method
- `CollateralValuation` entity tracks history but its interaction with `SetValuation()` is unclear (does re-valuing add to history or overwrite?)

**Impact:** Stale collateral values may continue to be used for LTV calculations, overstating coverage.

### 3.5 `AddValuation()` and `AddDocument()` Are Public Void Methods

These methods bypass domain logic, allowing arbitrary valuations and documents to be added without validation:
```csharp
public void AddValuation(CollateralValuation valuation) { _valuations.Add(valuation); }
public void AddDocument(CollateralDocument document) { _documents.Add(document); }
```

**Recommendation:** Make these private and only callable from within domain methods that perform proper validation (e.g., `SetValuation()` should internally create and add a `CollateralValuation` history entry).

---

## 4. Insurance Gap

### 4.1 No Insurance Expiry Monitoring

`InsuranceExpiryDate` is stored but there is no alert or monitoring when insurance expires. An uninsured pledged asset creates credit risk — if the asset is damaged or destroyed, the lender has no recovery path.

**Recommendation:** Integrate with the `NotificationService` to send alerts 30 and 7 days before insurance expiry.

---

## 5. Recommendations Summary

| Priority | Item |
|----------|------|
| HIGH | Fix `Reject()` — separate `RejectedByUserId`/`RejectedAt` from `ApprovedByUserId`/`ApprovedAt` |
| HIGH | Fix `new DateTime CreatedAt` property hiding — remove the `new` keyword |
| HIGH | Fix `CalculateLTV()` — move to aggregate calculation at application layer |
| MEDIUM | Fix `Release()` — do not reset `PerfectionStatus` to `NotStarted` |
| MEDIUM | Add validation: `0 <= HaircutPercentage <= 100` |
| MEDIUM | Implement collateral revaluation workflow with expiry monitoring |
| MEDIUM | Make `AddValuation()` and `AddDocument()` private |
| LOW | Add insurance expiry notifications |
| LOW | Document haircut storage format clearly (percentage vs. fraction) |
