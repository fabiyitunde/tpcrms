# Feedback Issue FB-001: Consent Validation Gap in Bureau Handler

**Original Issue:** C-02 (CRITICAL)
**Phase Claimed Fixed:** B
**Verified Status:** PARTIALLY FIXED ⚠️
**Severity:** CRITICAL — NDPA compliance gap remains open

---

## What Was Done (Correct)

The dev team:
1. Created the `ConsentRecord` aggregate with `IsValid()`, expiry, and revocation
2. Made `ConsentRecordId` a required (non-nullable) parameter in `RequestBureauReportCommand`
3. Passed `ConsentRecordId` to `BureauReport.Create()`

This is the correct structural approach. The plumbing exists.

---

## What Is Missing

The `RequestBureauReportHandler` does **not look up or validate the consent record** before calling the bureau provider:

```csharp
// RequestBureauReportHandler.Handle()
public async Task<ApplicationResult<BureauReportDto>> Handle(
    RequestBureauReportCommand request, CancellationToken ct = default)
{
    // PROBLEM: ConsentRecordId is accepted but never validated
    var reportResult = BureauReport.Create(
        request.Provider,
        SubjectType.Individual,
        request.SubjectName,
        request.BVN,
        request.RequestedByUserId,
        request.ConsentRecordId,  // ← stored but consent is not checked
        request.LoanApplicationId
    );

    // Bureau call proceeds immediately — consent may be expired or revoked
    var searchResult = await _bureauProvider.SearchByBVNAsync(request.BVN, ct);
    ...
}
```

Any caller can pass a fabricated GUID, an expired consent ID, or a revoked consent ID. The bureau check will proceed regardless.

---

## Required Fix

### 1. Inject `IConsentRecordRepository` into the handler

```csharp
public class RequestBureauReportHandler : IRequestHandler<...>
{
    private readonly IConsentRecordRepository _consentRepository;
    // ... existing fields

    public RequestBureauReportHandler(
        IConsentRecordRepository consentRepository,
        // ... existing parameters
    )
    {
        _consentRepository = consentRepository;
        // ...
    }
}
```

### 2. Validate the consent before proceeding

```csharp
public async Task<ApplicationResult<BureauReportDto>> Handle(
    RequestBureauReportCommand request, CancellationToken ct = default)
{
    // NDPA: Validate consent before any bureau access
    var consent = await _consentRepository.GetByIdAsync(request.ConsentRecordId, ct);
    if (consent == null)
        return ApplicationResult<BureauReportDto>.Failure(
            "Consent record not found. Bureau access requires valid borrower consent.");

    if (!consent.IsValid())
        return ApplicationResult<BureauReportDto>.Failure(
            $"Consent is {consent.Status}. Active consent is required for credit bureau checks.");

    if (consent.ConsentType != ConsentType.CreditBureauCheck)
        return ApplicationResult<BureauReportDto>.Failure(
            "Consent type does not authorize credit bureau access.");

    // Proceed with bureau check...
}
```

### 3. Apply the same fix to `ProcessLoanCreditChecksCommand`

The `ProcessLoanCreditChecksCommand` handler likely triggers bureau checks for all directors automatically. That handler must also validate consent for each subject before triggering their individual bureau check.

---

## Test Scenarios

- [ ] Passing a non-existent consent GUID → should return failure
- [ ] Passing an expired consent GUID → should return failure
- [ ] Passing a revoked consent GUID → should return failure
- [ ] Passing a consent with type `DataProcessing` instead of `CreditBureauCheck` → should return failure
- [ ] Passing a valid, active `CreditBureauCheck` consent → should proceed normally
