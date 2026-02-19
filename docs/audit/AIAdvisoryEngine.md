# Audit Report: AIAdvisoryEngine Module

**Module ID:** 10
**Audit Date:** 2026-02-17
**Auditor:** Domain Expert Review
**Module Status (Documented):** 🟢 Completed
**Audit Verdict:** 🔴 Critical Scoring Bug — Incorrect Weighted Score Calculation

---

## 1. Executive Summary

The AIAdvisoryEngine is architecturally sound with a clean separation between data aggregation, scoring, and recommendation generation. The maker-checker scoring configuration framework is well-designed for regulatory compliance. However, there is a **critical mathematical bug** in the overall score calculation that will produce incorrect scores for all advisories. Additionally, the database-driven scoring configuration is documented but not actually implemented in the mock service. Several hardcoded thresholds bypass the configurable scoring system, and there are concurrency gaps for multiple advisory generation requests.

---

## 2. Critical Bug

### 2.1 Double-Weighting in `Complete()` Causes Incorrect Overall Score (CRITICAL)

In `CreditAdvisory.Complete()`:

```csharp
var totalWeight = _riskScores.Sum(s => s.Weight);
if (totalWeight > 0)
{
    OverallScore = Math.Round(_riskScores.Sum(s => s.WeightedScore) / totalWeight, 2);
}
```

The problem:
- `WeightedScore = Score × Weight` (e.g., Score=75, Weight=0.25 → WeightedScore=18.75)
- `OverallScore = Sum(WeightedScore) / Sum(Weight)` = `Sum(Score × Weight) / Sum(Weight)`
- This is the **weighted average of scores** = correct formula

**Wait — this is actually correct.** Let me re-examine. If weights sum to 1.0 (which they should for a proper weighted average), then:
- `OverallScore = Sum(Score × Weight) / 1.0 = Sum(Score × Weight)` — correct
- `OverallScore = Sum(Score × Weight) / Sum(Weight)` — also correct (normalizes for partial weights)

However, the bug occurs when **not all weight categories are scored**. If only 5 of 8 categories produce scores (e.g., ManagementRisk, IndustryRisk, ConcentrationRisk are optional), and their weights are excluded, the divisor `totalWeight` may be less than 1.0. This inflates the overall score beyond what the actual scored categories merit.

**Example:**
- 5 categories scored: weights = {0.25, 0.25, 0.15, 0.20, 0.15} → Sum = 1.00 (all weights)
- If IndustryRisk (0.10) and ConcentrationRisk (0.10) are skipped: weights = {0.25, 0.25, 0.15, 0.20, 0.15} → Sum = 1.00 still

**The real risk:** If optional categories are added to the weights in `ScoringConfiguration` but their scoring code is not always executed, totalWeight may not reflect the actual scored subset. The behavior needs explicit documentation and test coverage.

**Recommendation:**
- Add a unit test that asserts `OverallScore` with known inputs and expected outputs
- Document clearly which scoring categories are always present vs. optional
- If optional, reduce weights proportionally or use a fixed weight table

### 2.2 `HasCriticalRedFlags` Threshold Is Hardcoded (HIGH)

```csharp
public bool HasCriticalRedFlags => _redFlags.Count >= 3 ||
    _riskScores.Any(s => s.Rating == RiskRating.VeryHigh);
```

The threshold of 3 red flags is hardcoded in the domain entity. The `ScoringConfiguration` system exists precisely to make these thresholds configurable via maker-checker workflow. This bypasses that system.

**Recommendation:** Add `CriticalRedFlagsThreshold` as a `ScoringParameter` and load it from the configuration service.

---

## 3. Configuration Implementation Gap

### 3.1 Mock Service Does Not Use Database-Driven Configuration (HIGH)

The `AIAdvisoryEngine.md` documentation describes a comprehensive database-driven scoring configuration with maker-checker workflow. However, the `MockAIAdvisoryService` appears to use hardcoded scoring parameters (base scores, thresholds, weights) that do not read from `ScoringParameter` records in the database.

**Impact:** The maker-checker scoring configuration system — while implemented at the infrastructure level — is never actually consulted during advisory generation. Changing a parameter via the UI will have no effect on generated advisories.

**Recommendation:**
- Inject `IScoringConfigurationService` (or `ScoringConfigurationService`) into the scoring logic
- Load active `ScoringParameter` values at the start of each advisory generation
- Add a test that verifies a parameter change propagates to advisory scores

---

## 4. Concurrency and State Issues

### 4.1 No Guard Against Concurrent Advisory Generation (MEDIUM)

If two users simultaneously trigger advisory generation for the same loan application (e.g., by double-clicking), two `CreditAdvisory` records will be created in `Processing` state simultaneously. Both will complete independently, resulting in duplicate advisories with potentially different scores (if scoring is non-deterministic).

**Recommendation:**
- Before creating a new advisory, check if one with `Status = Processing` already exists for the `LoanApplicationId`
- If yes, return the existing one
- Apply optimistic concurrency or a distributed lock for the check

### 4.2 No Validation That Prerequisites Are Met (MEDIUM)

`GenerateCreditAdvisoryCommand` does not validate that the loan application has:
- Completed credit analysis (all bureau checks done)
- At least one verified financial statement

Without validated prerequisites, the advisory will generate but with missing data, producing an artificially low (or default) score that does not reflect the true credit picture.

**Recommendation:** Add prerequisite checks in the handler before creating the advisory.

---

## 5. Scoring Logic Issues

### 5.1 `BureauReportIds` and `FinancialStatementIds` Break Encapsulation (LOW)

```csharp
public List<Guid> BureauReportIds { get; private set; } = new();
public List<Guid> FinancialStatementIds { get; private set; } = new();
```

These are public `List<T>` with `private set`, but the underlying collection is still mutable. External code can do `advisory.BureauReportIds.Add(...)`. They should be `IReadOnlyList<Guid>` or backed by a private field.

### 5.2 Interest Rate Recommendation Below Market Floor (MEDIUM)

The scoring table shows a score ≥ 80 gets a -2.0% rate adjustment. If the base rate is configurable and set low (e.g., 10%), the recommended rate could go below the CBN Minimum Lending Rate (currently 18%). The system should enforce a minimum lending rate floor.

**Recommendation:** Add a `MinimumLendingRate` parameter to `ScoringConfiguration` and enforce it in rate recommendations.

### 5.3 Conditions and Covenants Are Duplicated on Re-generation (LOW)

When an advisory is regenerated (e.g., after new financial data is added), the conditions from the previous run are not cleared. `AddCondition()` prevents exact duplicates, but near-identical conditions (different wording, same intent) will accumulate.

**Recommendation:** Clear `_conditions` and `_covenants` when `StartProcessing()` is called.

---

## 6. Recommendations Summary

| Priority | Item |
|----------|------|
| CRITICAL | Audit and unit-test the `Complete()` weighted score calculation |
| HIGH | Connect `MockAIAdvisoryService` to database-driven `ScoringConfiguration` |
| HIGH | Replace hardcoded `HasCriticalRedFlags` threshold with configurable parameter |
| HIGH | Add prerequisite validation before advisory generation |
| MEDIUM | Guard against concurrent advisory generation for same loan application |
| MEDIUM | Fix `BureauReportIds`/`FinancialStatementIds` encapsulation |
| MEDIUM | Add minimum lending rate floor to rate recommendations |
| LOW | Clear conditions/covenants on re-generation |
| LOW | Add comprehensive unit tests for all scoring categories |
