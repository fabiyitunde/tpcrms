# Audit Feedback V2 — Phase Verification Report

**Review Date:** 2026-02-18
**Reviewer:** Independent Audit (Domain + Code Review)
**Scope:** Verification of dev team fixes for FB-001 through FB-004 raised in `auditfeedback/`

---

## Executive Summary

All four feedback items (FB-001 through FB-004) from the initial verification round have been **fully addressed**. The implementations are structurally sound and correctly wired into the DI container.

Two residual issues were identified during this review:

| ID      | Severity | Description                                      | Status   |
|---------|----------|--------------------------------------------------|----------|
| RES-001 | HIGH     | Loan pack Generate endpoint uses random userId   | OPEN     |
| RES-002 | MEDIUM   | Duplicate DB queries in GenerateLoanPackHandler  | OPEN     |
| RES-003 | LOW      | `"token"` in sensitive field list — false-positive risk | OPEN |
| RES-004 | LOW      | `LogLoginAsync` bypasses `LogAsync()` pipeline  | OPEN     |

---

## FB-001: Consent Validation (C-02) — CONFIRMED FIXED

**Original gap:** `ConsentRecordId` was stored on `BureauReport` but never looked up or validated in the handler before making bureau calls. Consent enforcement was cosmetic only.

### Verification — `RequestBureauReportHandler`

```csharp
// File: src/CRMS.Application/CreditBureau/Commands/RequestBureauReportCommand.cs
var consent = await _consentRepository.GetByIdAsync(request.ConsentRecordId, ct);
if (consent == null)
    return ApplicationResult<Guid>.Failure("Consent record not found...");
if (!consent.IsValid())
    return ApplicationResult<Guid>.Failure($"Consent is {consent.Status}...");
if (consent.ConsentType != ConsentType.CreditBureauCheck)
    return ApplicationResult<Guid>.Failure("Consent type does not authorize credit bureau checks.");
```

- `IConsentRecordRepository` is injected into the handler constructor ✓
- Consent existence, validity (`IsValid()`), and type (`ConsentType.CreditBureauCheck`) are all validated before the bureau call ✓
- Failure paths return descriptive error messages ✓

### Verification — `ProcessLoanCreditChecksHandler`

```csharp
// File: src/CRMS.Application/CreditBureau/Commands/ProcessLoanCreditChecksCommand.cs
var consent = await GetValidConsentAsync(bvn, ConsentType.CreditBureauCheck, ct);
if (consent == null)
{
    result.PartiesProcessed.Add(new CreditCheckResult(..., false, "No valid consent found for BVN..."));
    continue;
}
```

- `IConsentRecordRepository` is injected ✓
- Each party's BVN is consent-checked before a bureau request is dispatched ✓
- Parties without valid consent are skipped with a recorded failure result rather than a hard exception ✓

**VERDICT: FB-001 FULLY RESOLVED.**

---

## FB-002: File Storage Integration (C-08) — CONFIRMED FIXED

**Original gap:** `GenerateLoanPackHandler` had a `// TODO: Save PDF bytes to file storage` comment; the PDF was generated and discarded. The download endpoint returned `NotFound("PDF file storage not implemented")`.

### Verification — `GenerateLoanPackHandler`

```csharp
// File: src/CRMS.Application/LoanPack/Commands/GenerateLoanPackCommand.cs (line 152)
var actualStoragePath = await _fileStorage.UploadAsync(
    containerName: "loanpacks",
    fileName: $"{loanApp.ApplicationNumber}/{fileName}",
    content: pdfBytes,
    contentType: "application/pdf",
    ct: ct);

loanPack.SetDocument(fileName, actualStoragePath, pdfBytes.Length, contentHash);
```

- `IFileStorageService` is injected in constructor ✓
- `UploadAsync()` is called with the correct container, structured path, and MIME type ✓
- The actual storage path returned by the service (which may differ from the computed path in cloud providers) is used to update the entity ✓

### Verification — Download Endpoint

```csharp
// File: src/CRMS.API/Controllers/LoanPackController.cs
[Authorize(Roles = "CreditOfficer,HOReviewer,RiskManager,CommitteeMember,FinalApprover,SystemAdmin")]
public async Task<IActionResult> Download(Guid id, CancellationToken ct)
{
    if (!await _fileStorage.ExistsAsync(pack.StoragePath, ct))
        return NotFound("Loan pack file not found in storage");
    var fileBytes = await _fileStorage.DownloadAsync(pack.StoragePath, ct);
    return File(fileBytes, "application/pdf", pack.FileName);
}
```

- Role-based authorization applied to download endpoint ✓
- File existence checked before download attempt ✓
- Correct MIME type returned ✓

**VERDICT: FB-002 FULLY RESOLVED.**

> ⚠️ **See RES-001 and RES-002** for two residual issues in the same handler/controller.

---

## FB-003: Role Names Mismatch (NEW-01) — CONFIRMED FIXED

**Original gap:** `SeedData.cs` used hardcoded strings (`"BranchManager"`, `"CommitteeChair"`, `"CustomerService"`) that did not match constants in `Roles.cs`, causing startup role seeding to create misnamed roles which broke `[Authorize(Roles = "...")]` enforcement.

### Verification — `SeedData.cs`

```csharp
// File: src/CRMS.Infrastructure/Persistence/SeedData.cs
using CRMS.Domain.Constants;
// ...
foreach (var roleName in Roles.AllRoles)
{
    var description = Roles.RoleDescriptions.GetValueOrDefault(roleName, roleName);
    var role = ApplicationRole.Create(roleName, description, RoleType.System);
    await context.Roles.AddAsync(role);
}
```

- All roles now sourced from `Roles.AllRoles` — a static collection in `Roles.cs` ✓
- Descriptions sourced from `Roles.RoleDescriptions` dictionary ✓
- Hardcoded strings eliminated; future additions to `Roles.cs` will automatically flow through to seeding ✓

**VERDICT: FB-003 FULLY RESOLVED.**

---

## FB-004: IP Capture and Sensitive Data Masking (H-19 / H-20) — CONFIRMED FIXED

**Original gap:** `AuditService` had no mechanism to auto-capture client IP address; all audit logs were written with `ipAddress = null` unless callers passed it explicitly. BVN, NIN, and other PII fields were serialized in plain text into audit log JSON payloads.

### Verification — `IAuditContextProvider` (new interface)

```
File: src/CRMS.Domain/Interfaces/IAuditContextProvider.cs
```

- Domain-layer interface with `GetClientIpAddress()` and `GetUserAgent()` ✓
- Explicitly documented to return `null` for background processes (no HTTP context) ✓
- Correctly placed in Domain layer to avoid Infrastructure dependency leaking upward ✓

### Verification — `HttpContextService`

```
File: src/CRMS.Infrastructure/Services/HttpContextService.cs
```

- Implements `IHttpContextService : IAuditContextProvider` ✓
- `GetClientIpAddress()` checks `X-Forwarded-For` → `X-Real-IP` → `RemoteIpAddress` in order ✓
- Returns `null` gracefully when `HttpContext` is null (background job / event handler context) ✓

### Verification — `SensitiveDataMasker`

```
File: src/CRMS.Domain/Services/SensitiveDataMasker.cs
```

- `MaskBvn()`: Exposes first 3 + last 2 digits (e.g., `222****90`) ✓
- `MaskAccountNumber()`: Exposes last 4 digits (e.g., `******5678`) ✓
- `MaskJsonSensitiveFields()`: Recursive JSON walk with case-insensitive, underscore-normalized field name matching ✓
- Sensitive field list covers: `bvn`, `nin`, `accountnumber`, `password`, `token`, `apikey`, `rawbureauresponse`, `ssn`, `taxid`, `driverlicense` ✓

### Verification — `AuditService` (updated)

```csharp
// File: src/CRMS.Domain/Services/AuditService.cs
var effectiveIpAddress = ipAddress ?? _auditContext?.GetClientIpAddress();
var userAgent = _auditContext?.GetUserAgent();

var maskedOldValues = oldValues != null
    ? SensitiveDataMasker.MaskJsonSensitiveFields(JsonSerializer.Serialize(oldValues))
    : null;
var maskedNewValues = newValues != null
    ? SensitiveDataMasker.MaskJsonSensitiveFields(JsonSerializer.Serialize(newValues))
    : null;
var maskedAdditionalData = additionalData != null
    ? SensitiveDataMasker.MaskJsonSensitiveFields(JsonSerializer.Serialize(additionalData))
    : null;
```

- `IAuditContextProvider` injected as optional parameter — does not break background service usage ✓
- IP auto-captured from context if not explicitly supplied by caller ✓
- All three JSON payload fields (old, new, additional) run through `SensitiveDataMasker` ✓

### Verification — DI Registration (`DependencyInjection.cs`)

```csharp
services.AddScoped<IHttpContextService, HttpContextService>();
services.AddScoped<IAuditContextProvider>(sp => sp.GetRequiredService<IHttpContextService>());
services.AddScoped<AuditService>();
```

- Single `HttpContextService` instance satisfies both `IHttpContextService` and `IAuditContextProvider` contracts ✓
- `AuditService` registered as scoped ✓

### Verification — `Program.cs`

```csharp
// Line 21: src/CRMS.API/Program.cs
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IHttpContextService, HttpContextService>();
```

- `AddHttpContextAccessor()` is confirmed registered before `AddInfrastructure()` ✓
- The `IHttpContextAccessor` will resolve correctly at runtime ✓

**VERDICT: FB-004 FULLY RESOLVED.**

---

## Residual Items

### RES-001 (HIGH) — See `RES-001-LoanPackUserIdNotFromClaims.md`

The `LoanPackController.Generate()` endpoint still contains:
```csharp
// TODO: Get user info from claims
var userId = Guid.NewGuid();
```

Every generated loan pack is attributed to a random GUID. This breaks the audit trail for loan pack generation and bypasses role-based attribution.

---

### RES-002 (MEDIUM) — See `RES-002-DuplicateDbQueriesInGenerateLoanPack.md`

`GenerateLoanPackHandler.Handle()` fetches advisory, bureau reports, collaterals, and guarantors from the database in `BuildLoanPackDataAsync()`, then fetches those same four resources again a second time (lines 117–120) to populate `SetContentSummary()`. This results in 4 unnecessary database round-trips per loan pack generation.

---

### RES-003 (LOW) — `SensitiveDataMasker`: `"token"` field name risk

The word `"token"` in the sensitive field list is very generic. Audit log payloads that legitimately contain fields named `token` (e.g., `CancellationToken` serialized data, `SessionToken` for non-PII session identifiers, workflow `ActionToken`) will be masked unnecessarily. This reduces audit log readability. The masker should use more specific names like `"accesstoken"`, `"bearertoken"`, `"authtoken"` rather than the bare `"token"`.

**No code change required immediately** — current behaviour is conservative and errs on the side of privacy. Track for refinement.

---

### RES-004 (LOW) — `LogLoginAsync` bypasses masking pipeline

`AuditService.LogLoginAsync()` creates an `AuditLog` directly via `AuditLog.Create()` rather than calling the shared `LogAsync()` method. This means:
- IP is not auto-captured from `_auditContext` — it must be passed explicitly by the caller
- No `SensitiveDataMasker` is applied (acceptable since login logs contain no `oldValues`/`newValues`)

Risk is low since login logs don't hold PII JSON payloads. However, if `LogLoginAsync` is ever extended to log device fingerprints or session data, this bypass will need to be addressed. Consider refactoring to call `LogAsync()` internally for consistency.

---

## Summary Table

| Item   | Original Severity | Status           | Notes                                 |
|--------|-------------------|------------------|---------------------------------------|
| FB-001 | CRITICAL          | RESOLVED         | Consent enforced in both handlers     |
| FB-002 | CRITICAL          | RESOLVED         | Storage connected; download working   |
| FB-003 | HIGH              | RESOLVED         | SeedData uses Roles.AllRoles loop     |
| FB-004 | HIGH              | RESOLVED         | Full IP + masking pipeline in place   |
| RES-001| HIGH (new)        | OPEN             | userId = Guid.NewGuid() in Generate   |
| RES-002| MEDIUM (new)      | OPEN             | 4 duplicate DB queries per pack gen   |
| RES-003| LOW (new)         | OPEN (deferred)  | "token" false-positive masking        |
| RES-004| LOW (new)         | OPEN (deferred)  | LogLoginAsync pipeline bypass         |
