# Audit Report: GuarantorManagement Module

**Module ID:** 8 (Listed as Module ID 18 in module doc — numbering discrepancy with Implementation Tracker)
**Audit Date:** 2026-02-17
**Auditor:** Domain Expert Review
**Module Status (Documented):** 🟢 Completed
**Audit Verdict:** ⚠️ Bugs Present — Semantic and Business Logic Issues

---

## 1. Executive Summary

The GuarantorManagement module provides good coverage of individual and corporate guarantors, guarantee types, and credit check integration. However, it shares two structural bugs with the CollateralManagement module (`Reject()` using `ApprovedByUserId` and `CreatedAt` property hiding), has a domain mutability violation in `SetDirectorDetails()`, and lacks critical validation on credit score thresholds and currency handling. The BVN is also stored as a plain string instead of using the dedicated value object.

---

## 2. Confirmed Bugs

### 2.1 `Reject()` Stores Rejector in `ApprovedByUserId` (MEDIUM-HIGH)

Same bug as CollateralManagement:
```csharp
public Result Reject(Guid rejectedByUserId, string reason)
{
    Status = GuarantorStatus.Rejected;
    ApprovedByUserId = rejectedByUserId;   // BUG: semantic error
    ApprovedAt = DateTime.UtcNow;
    RejectionReason = reason;
}
```

A rejected guarantor's audit trail will show the rejector as if they approved the guarantor.

**Recommendation:** Add dedicated `RejectedByUserId` (Guid?) and `RejectedAt` (DateTime?) fields.

### 2.2 `CreatedAt` Property Hiding (MEDIUM)

```csharp
public new DateTime CreatedAt { get; private set; }
```

Same issue as CollateralManagement — `new` shadows the base class property and causes EF Core mapping ambiguity.

**Recommendation:** Remove the `new` keyword. Set base class `CreatedAt` via protected setter or constructor.

### 2.3 `SetDirectorDetails()` Mutates `Type` After Factory Creation (MEDIUM)

```csharp
public Result SetDirectorDetails(bool isDirector, bool isShareholder, decimal? shareholdingPercentage)
{
    IsDirector = isDirector;
    IsShareHolder = isShareholder;
    ShareholdingPercentage = shareholdingPercentage;

    if (isDirector)
        Type = GuarantorType.Director;       // Mutating private set property
    else if (isShareholder)
        Type = GuarantorType.Shareholder;
    ...
}
```

This violates the aggregate's immutability principle for `Type`. The `Guarantor` was created via `CreateIndividual()` which set `Type = GuarantorType.Individual`. Calling `SetDirectorDetails()` silently mutates this, making the historical factory call misleading. It also means that calling `SetDirectorDetails(false, false, null)` on a Director type guarantor would not reset `Type`, leaving an inconsistency.

**Recommendation:**
- Remove `Type` mutation from `SetDirectorDetails()`
- Instead, use separate factory methods: `CreateDirector()`, `CreateShareholder()`
- Or pass the guarantor type explicitly to `CreateIndividual()` with director/shareholder subtype

---

## 3. Business Logic Gaps

### 3.1 BVN Not Using `BVN` Value Object (MEDIUM)

The domain layer has a `BVN` value object (`ValueObjects/BVN.cs`) that presumably enforces format validation. However, `Guarantor.BVN` is declared as a plain `string?`:

```csharp
public string? BVN { get; private set; }
```

This allows invalid BVN formats (not 11 digits, letters, etc.) to be stored.

**Recommendation:** Use the `BVN` value object type for the `Guarantor.BVN` property.

### 3.2 Credit Score Rejection Threshold Is Hardcoded (MEDIUM)

The module documentation states a credit score below 550 triggers automatic rejection. However, this threshold should be managed through the `ScoringConfiguration` maker-checker system (like all other scoring parameters). If hardcoded in the application handler, the business cannot change the threshold without a code deployment.

**Recommendation:** Add the guarantor credit score threshold as a `ScoringParameter` in the database configuration.

### 3.3 `CanGuarantee()` Ignores Currency Mismatch (MEDIUM)

```csharp
public bool CanGuarantee(Money loanAmount)
{
    if (IsUnlimited) return true;
    if (GuaranteeLimit == null) return false;

    var availableLimit = GuaranteeLimit.Amount - (TotalExistingGuarantees?.Amount ?? 0);
    return availableLimit >= loanAmount.Amount;
}
```

This compares `Amount` values without checking if `GuaranteeLimit.Currency`, `TotalExistingGuarantees.Currency`, and `loanAmount.Currency` are the same. If a guarantor has a USD guarantee limit and the loan is in NGN, this comparison is meaningless.

**Recommendation:** Add currency matching validation. If currencies differ, either return `false` (safest) or apply an exchange rate (complex, requires FX service).

### 3.4 No Duplicate Guarantor Detection

There is no validation preventing the same individual (same BVN) from being added as a guarantor on the same loan application multiple times. This could lead to:
- Double-counting guarantee coverage
- Running bureau checks twice on the same person

**Recommendation:** In `AddGuarantorHandler`, check if a guarantor with the same BVN already exists for the `LoanApplicationId` before adding.

### 3.5 `ShareholdingPercentage` Not Validated (LOW)

`ShareholdingPercentage` should be between 0 and 100. No validation exists.

### 3.6 Corporate Guarantor Has No CAC Verification

For corporate guarantors, the `RegistrationNumber` is stored but there is no integration with the Corporate Affairs Commission (CAC) to verify that the company exists and is active. A fictitious company could be listed as a guarantor.

**Recommendation (Future):** Integrate with CAC API for corporate guarantor verification. For MVP, add a manual verification step and flag in the guarantor workflow.

---

## 4. Compliance Gaps

### 4.1 BVN Stored in Plain Text

`Guarantor.BVN` is stored as a plain string. BVN is NDPA-sensitive data and must be encrypted at rest, consistent with the FullDesign specification.

### 4.2 No Consent Check Before Running Guarantor Credit Check

Similar to the bureau integration issue, running a credit check on a guarantor requires their consent. The guarantor must agree to a credit check as part of signing the guarantee agreement. Currently, the `RunCreditCheck` endpoint can be called without verifying consent.

---

## 5. Recommendations Summary

| Priority | Item |
|----------|------|
| HIGH | Fix `Reject()` — use `RejectedByUserId`/`RejectedAt` |
| HIGH | Fix `new DateTime CreatedAt` property hiding |
| HIGH | Encrypt `BVN` at rest |
| HIGH | Require consent before running guarantor credit check |
| MEDIUM | Remove `Type` mutation from `SetDirectorDetails()` — use separate factories |
| MEDIUM | Use `BVN` value object instead of plain string |
| MEDIUM | Move credit score threshold to database-driven `ScoringConfiguration` |
| MEDIUM | Add currency check in `CanGuarantee()` |
| MEDIUM | Add duplicate guarantor detection (by BVN) |
| LOW | Validate `ShareholdingPercentage` range (0-100) |
| LOW | Plan for CAC corporate guarantor verification |
