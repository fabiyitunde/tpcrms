# Feedback Issue FB-002: File Storage Service Exists but Not Integrated

**Original Issue:** C-08 (CRITICAL)
**Phase Claimed Fixed:** C
**Verified Status:** PARTIALLY FIXED ⚠️
**Severity:** CRITICAL — Loan pack download remains non-functional

---

## What Was Done (Correct)

The dev team implemented the storage infrastructure:
- `IFileStorageService` interface with `UploadAsync`, `DownloadAsync`, `GetStreamAsync`, `DeleteAsync`, `ExistsAsync`
- `LocalFileStorageService` — fully implemented (writes to local disk, reads back, deletes)
- `S3FileStorageService` — implemented for cloud deployments
- `FileStorageSettings` for configuration

This is the right infrastructure. The problem is it was never wired into the loan pack generation flow.

---

## What Is Still Broken

### Problem 1: PDF bytes are never written to storage

In `GenerateLoanPackCommand.cs` line 151:
```csharp
// Save loan pack metadata
await _loanPackRepository.AddAsync(loanPack, ct);
await _unitOfWork.SaveChangesAsync(ct);

// TODO: Save PDF bytes to file storage (S3/Azure Blob)  ← still a TODO

return ApplicationResult<LoanPackResultDto>.Success(new LoanPackResultDto(...));
```

The `StoragePath` is computed and saved to the database, but the PDF bytes are discarded after generation. The file does not exist on disk or in S3.

### Problem 2: Download endpoint returns a hardcoded error

In `LoanPackController.cs`:
```csharp
[HttpGet("download/{id:guid}")]
public async Task<IActionResult> Download(Guid id, CancellationToken ct)
{
    // ...retrieves metadata...

    // TODO: Retrieve actual PDF from file storage using pack.StoragePath
    // For now, return a placeholder response
    return NotFound("PDF file storage not implemented - file would be at: " + pack.StoragePath);
}
```

Any committee member requesting the loan pack will receive a `404 Not Found`.

---

## Required Fix

### Step 1: Inject `IFileStorageService` into `GenerateLoanPackHandler`

```csharp
public class GenerateLoanPackHandler : IRequestHandler<...>
{
    private readonly IFileStorageService _fileStorage;
    // ... existing fields

    public GenerateLoanPackHandler(
        IFileStorageService fileStorage,
        // ... existing parameters
    )
    {
        _fileStorage = fileStorage;
        // ...
    }
}
```

### Step 2: Upload the PDF after generation

Replace the TODO comment with the actual upload:

```csharp
// Save loan pack metadata
await _loanPackRepository.AddAsync(loanPack, ct);
await _unitOfWork.SaveChangesAsync(ct);

// Save PDF bytes to file storage
var storedPath = await _fileStorage.UploadAsync(
    containerName: "loanpacks",
    fileName: fileName,
    content: pdfBytes,
    contentType: "application/pdf",
    ct: ct);

// Update storage path if different from expected
// (storage service may prefix with container)
```

### Step 3: Implement the Download endpoint

```csharp
[HttpGet("download/{id:guid}")]
public async Task<IActionResult> Download(Guid id, CancellationToken ct)
{
    var handler = _serviceProvider.GetRequiredService<GetLoanPackByIdHandler>();
    var result = await handler.Handle(new GetLoanPackByIdQuery(id), ct);

    if (!result.IsSuccess)
        return NotFound(result.Error);

    var pack = result.Data!;

    if (string.IsNullOrEmpty(pack.StoragePath))
        return NotFound("Loan pack file has not been generated yet");

    var fileStorage = _serviceProvider.GetRequiredService<IFileStorageService>();

    // Optional: verify content hash before serving
    var fileBytes = await fileStorage.DownloadAsync(pack.StoragePath, ct);

    return File(fileBytes, "application/pdf", pack.FileName);
}
```

### Step 4: Add authorization to download endpoint

The download endpoint currently has no role restriction beyond `[Authorize]`. It should restrict to roles that legitimately need to view loan packs:

```csharp
[Authorize(Roles = "CreditOfficer,HOReviewer,RiskManager,CommitteeMember,SystemAdmin")]
```

---

## Configuration Required

Ensure `appsettings.Development.json` and `appsettings.json` include:

```json
"FileStorage": {
  "Provider": "Local",
  "LocalPath": "/path/to/storage"
}
```

And `Program.cs` or `DependencyInjection.cs` registers the correct implementation:

```csharp
services.AddScoped<IFileStorageService, LocalFileStorageService>(); // dev
// or
services.AddScoped<IFileStorageService, S3FileStorageService>(); // prod
```

---

## Test Scenarios

- [ ] Generate a loan pack → verify a file exists at `StoragePath` on disk/S3
- [ ] Call `GET /api/LoanPack/download/{id}` → verify a PDF is returned (not 404)
- [ ] Verify the downloaded PDF matches the `ContentHash` stored in the database
- [ ] Unauthorized user calling download → receives 403
