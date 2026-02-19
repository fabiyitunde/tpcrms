# RES-004: `LogLoginAsync` Bypasses the `LogAsync()` Pipeline

**Severity:** LOW
**Category:** Audit Consistency / IP Capture Completeness
**File:** `src/CRMS.Domain/Services/AuditService.cs`
**Method:** `LogLoginAsync()`
**Status:** OPEN (unchanged from V2)

---

## Problem

`LogLoginAsync()` creates an `AuditLog` directly via `AuditLog.Create()` instead of delegating to the shared `LogAsync()` method:

```csharp
public async Task LogLoginAsync(
    Guid? userId, string? userName, string? email,
    bool isSuccess, string? failureReason = null,
    string? ipAddress = null, string? userAgent = null,
    CancellationToken ct = default)
{
    var log = AuditLog.Create(
        isSuccess ? AuditAction.Login : AuditAction.LoginFailed,
        AuditCategory.Authentication,
        ...
        ipAddress,    // uses caller-supplied value only
        userAgent,    // uses caller-supplied value only
        ...);

    await _auditRepository.AddAsync(log, ct);
    await _unitOfWork.SaveChangesAsync(ct);
}
```

Compare with `LogAsync()`:

```csharp
public async Task LogAsync(...)
{
    // Auto-capture IP from context if not provided
    var effectiveIpAddress = ipAddress ?? _auditContext?.GetClientIpAddress();
    var userAgent = _auditContext?.GetUserAgent();
    ...
}
```

The gap: if a caller invokes `LogLoginAsync(... ipAddress: null ...)`, the `_auditContext.GetClientIpAddress()` **fallback does not trigger**. The login log will record `null` for the IP address even when the HTTP context has a valid client IP available.

---

## Why This Matters for Login Events Specifically

Login events (both successful and failed) are among the **highest-value audit records** from a security and fraud detection perspective:

- **Brute-force detection** requires IP addresses for failed login attempts. A null IP in the login audit log defeats IP-based alerting.
- **Suspicious location detection** requires IP history per user — not possible if IPs are not captured consistently.
- **NDPA and CBN regulatory requirements** for financial systems mandate capturing the originating IP for authentication events.

Currently, the caller (`AuthService` or equivalent) must remember to explicitly resolve and pass the IP. This is a leaky abstraction — the IP capture responsibility has already been centralised in `IAuditContextProvider`, but `LogLoginAsync` does not use it.

---

## Recommended Fix

Refactor `LogLoginAsync` to delegate to `LogAsync()`, following the exact same pattern as all other `LogXxxAsync()` methods:

```csharp
public async Task LogLoginAsync(
    Guid? userId,
    string? userName,
    string? email,
    bool isSuccess,
    string? failureReason = null,
    string? ipAddress = null,
    CancellationToken ct = default)
{
    await LogAsync(
        isSuccess ? AuditAction.Login : AuditAction.LoginFailed,
        AuditCategory.Authentication,
        isSuccess
            ? $"User logged in: {userName ?? email}"
            : $"Login failed for: {userName ?? email}",
        "User",
        entityId: userId,
        entityReference: userName ?? email,
        userId: userId,
        userName: userName,
        ipAddress: ipAddress,   // LogAsync will fallback to _auditContext if null
        isSuccess: isSuccess,
        errorMessage: failureReason,
        ct: ct);
}
```

Note: The original `LogLoginAsync` accepted a `userAgent` parameter and passed it directly to `AuditLog.Create()`. In the refactored version this is not needed — `LogAsync()` captures `userAgent` automatically from `_auditContext?.GetUserAgent()`. The explicit `userAgent` parameter can be removed from the signature.

---

## Acceptance Criteria

- `LogLoginAsync` no longer calls `AuditLog.Create()` directly
- `LogLoginAsync` delegates to `LogAsync()`
- When a caller passes `ipAddress: null`, the `_auditContext.GetClientIpAddress()` fallback is applied automatically
- Failed login audit records capture the client IP when called within an HTTP request context
- The explicit `userAgent` parameter is removed from `LogLoginAsync` signature (it is auto-captured by `LogAsync`)
- All existing callers of `LogLoginAsync` that previously passed `userAgent:` explicitly are updated to remove that argument
