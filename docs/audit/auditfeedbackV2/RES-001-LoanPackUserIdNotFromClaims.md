# RES-001: Loan Pack Generate Endpoint — UserId Not Resolved from Claims

**Severity:** HIGH
**Category:** Audit Trail / Security
**File:** `src/CRMS.API/Controllers/LoanPackController.cs`
**Relates to:** C-08 (LoanPackGenerator), H-19 (Audit Completeness)

---

## Description

The `Generate` action in `LoanPackController` contains a TODO comment that was never resolved:

```csharp
// TODO: Get user info from claims
var userId = Guid.NewGuid();
var userName = User.Identity?.Name ?? "System";
```

Every loan pack generation event is recorded against a **randomly generated GUID** as the `GeneratedByUserId`. The `GenerateLoanPackCommand` is constructed with this fake ID and persisted on the `LoanPack` entity via `LoanPack.Create(... generatedByUserId ...)`.

---

## Impact

1. **Broken audit trail for loan pack generation** — regulatory auditors reviewing who generated a loan pack for a credit application will see a random non-traceable GUID rather than the actual officer's identity.

2. **Inconsistency with role enforcement** — the endpoint is restricted to `[Authorize(Roles = "CreditOfficer,HOReviewer,RiskManager,SystemAdmin")]`, meaning authentication is enforced at the gate, but the authenticated identity is then immediately discarded and replaced with a random GUID. This defeats the purpose of the role restriction.

3. **Cannot correlate loan pack to workflow step** — downstream processes that join loan packs to the workflow actor for the "Document Generation" step will find no matching user.

4. **NDPA / internal policy risk** — if a loan pack containing sensitive customer data is generated, there is no record of which staff member initiated it.

---

## Root Cause

The `userName` from claims (`User.Identity?.Name`) is retrieved correctly, but `userId` requires an explicit lookup from the JWT sub/NameIdentifier claim. This was noted as a TODO during initial scaffolding and not completed when the storage integration fix was applied.

---

## Recommended Fix

Extract `userId` and `userName` from the authenticated principal using the existing `IHttpContextService` (already registered in DI), or resolve directly from claims in the controller:

```csharp
// Option A: Inject IHttpContextService and use GetCurrentUserId()
[HttpPost("generate/{loanApplicationId:guid}")]
[Authorize(Roles = "CreditOfficer,HOReviewer,RiskManager,SystemAdmin")]
public async Task<IActionResult> Generate(Guid loanApplicationId, CancellationToken ct)
{
    var userId = _httpContextService.GetCurrentUserId();
    if (userId == null)
        return Unauthorized("Could not resolve authenticated user identity.");

    var userName = User.Identity?.Name ?? "Unknown";

    var handler = _serviceProvider.GetRequiredService<GenerateLoanPackHandler>();
    var result = await handler.Handle(
        new GenerateLoanPackCommand(loanApplicationId, userId.Value, userName), ct);

    return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
}
```

```csharp
// Option B: Resolve directly from claims (no extra service dependency)
var userIdClaim = User.FindFirst("sub")?.Value
               ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

if (!Guid.TryParse(userIdClaim, out var userId))
    return Unauthorized("User identity claim is invalid or missing.");
```

**Preferred:** Option A — consistent with how other controllers should resolve user identity, and reuses the existing `IHttpContextService` pattern.

---

## Acceptance Criteria

- `LoanPack.GeneratedByUserId` persisted in DB matches the JWT `sub` claim of the authenticated user
- `LoanPack.GeneratedByUserName` matches the authenticated user's display name from claims
- No `Guid.NewGuid()` call remains in the Generate action
- The TODO comment is removed
