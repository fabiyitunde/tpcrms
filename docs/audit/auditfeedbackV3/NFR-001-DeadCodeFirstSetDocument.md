# NFR-001: Dead Code — `storagePath` Variable and First `SetDocument()` Call

**Severity:** LOW
**Category:** Code Quality / Dead Code
**File:** `src/CRMS.Application/LoanPack/Commands/GenerateLoanPackCommand.cs`
**Method:** `GenerateLoanPackHandler.Handle()`

---

## Problem

The handler computes a `storagePath` string and calls `loanPack.SetDocument()` before the file is uploaded to storage. That same `SetDocument()` is then called again after the upload with the actual storage path. The first call and the variable that feeds it are dead code.

```csharp
// Line 105: file name — legitimately used below
var fileName = $"LoanPack_{loanApp.ApplicationNumber}_v{existingVersion + 1}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

// Line 106: computed path — DEAD; only used in the first SetDocument call
var storagePath = $"loanpacks/{loanApp.ApplicationNumber}/{fileName}";

// Lines 109–111: hash — legitimately used in both SetDocument calls
using var sha256 = System.Security.Cryptography.SHA256.Create();
var hashBytes = sha256.ComputeHash(pdfBytes);
var contentHash = Convert.ToBase64String(hashBytes);

// Line 114: DEAD — entity not yet saved; value will be overwritten at line 146
loanPack.SetDocument(fileName, storagePath, pdfBytes.Length, contentHash);

// ... SetContentSummary, SetIncludedSections ...

// Lines 138–143: actual upload to storage
var actualStoragePath = await _fileStorage.UploadAsync(
    containerName: "loanpacks",
    fileName: $"{loanApp.ApplicationNumber}/{fileName}",
    content: pdfBytes,
    contentType: "application/pdf",
    ct: ct);

// Line 146: SetDocument called again — THIS is the call that matters
loanPack.SetDocument(fileName, actualStoragePath, pdfBytes.Length, contentHash);

// Line 149: entity saved — only the state from line 146 is persisted
await _loanPackRepository.AddAsync(loanPack, ct);
await _unitOfWork.SaveChangesAsync(ct);
```

### Why the first call is truly dead

- The `loanPack` entity is a local in-memory object, not yet persisted to the database
- `SetDocument()` mutates in-memory state only
- The mutation from line 114 is completely overwritten by line 146 with a different value (`actualStoragePath` vs the computed `storagePath`)
- `AddAsync()` and `SaveChangesAsync()` are only called after line 146 — only the second `SetDocument()` state is ever written to the database

### Origin of the dead code

This was a remnant of the pre-FB-002 code which had `SetDocument()` as a placeholder before file storage was wired up. When `IFileStorageService.UploadAsync()` was added, a second `SetDocument()` call was appended after the upload rather than replacing the first one.

---

## Recommended Fix

Remove the `storagePath` variable and the first `SetDocument()` call entirely. Move the single `SetDocument()` call to after the upload:

```csharp
// Generate file name
var fileName = $"LoanPack_{loanApp.ApplicationNumber}_v{existingVersion + 1}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

// Calculate content hash
using var sha256 = System.Security.Cryptography.SHA256.Create();
var hashBytes = sha256.ComputeHash(pdfBytes);
var contentHash = Convert.ToBase64String(hashBytes);

// Set content summary (using packData, not re-fetching)
loanPack.SetContentSummary(...);
loanPack.SetIncludedSections(...);

// Upload to storage
var actualStoragePath = await _fileStorage.UploadAsync(
    containerName: "loanpacks",
    fileName: $"{loanApp.ApplicationNumber}/{fileName}",
    content: pdfBytes,
    contentType: "application/pdf",
    ct: ct);

// Set document details once, with the real storage path
loanPack.SetDocument(fileName, actualStoragePath, pdfBytes.Length, contentHash);
```

Lines removed: `var storagePath = ...` and the first `loanPack.SetDocument(fileName, storagePath, ...)` call.

---

## Acceptance Criteria

- `var storagePath = ...` variable removed
- First `loanPack.SetDocument()` call removed
- Single `loanPack.SetDocument()` call placed after `_fileStorage.UploadAsync()` returns
- `loanPack.SetDocument()` is called exactly once per successful loan pack generation
- Behaviour is identical to before (no functional change — only dead code removed)
