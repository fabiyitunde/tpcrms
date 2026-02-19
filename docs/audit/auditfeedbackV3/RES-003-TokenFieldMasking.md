# RES-003: `SensitiveDataMasker` — `"token"` Field Name Too Generic

**Severity:** LOW
**Category:** Data Privacy / Audit Log Quality
**File:** `src/CRMS.Domain/Services/SensitiveDataMasker.cs`
**Status:** OPEN (unchanged from V2)

---

## Problem

The `SensitiveFieldNames` HashSet contains the bare string `"token"`:

```csharp
private static readonly HashSet<string> SensitiveFieldNames = new(StringComparer.OrdinalIgnoreCase)
{
    "bvn", "nin", "accountnumber", "account_number",
    "password", "secret",
    "token",          // <-- too generic
    "apikey", "api_key",
    ...
};
```

The masker normalises property names by stripping underscores before matching:

```csharp
if (SensitiveFieldNames.Contains(key.Replace("_", "")))
```

This means **any** JSON property whose name, after underscore removal, equals `"token"` will be masked to `"********"`. This includes legitimate, non-sensitive fields that the system already uses or may introduce:

| Legitimate Field Name     | Audit Context                             | Masked? |
|---------------------------|-------------------------------------------|---------|
| `Token` / `token`         | Workflow action token, CSRF token         | YES ❌  |
| `ActionToken`             | Normalised: `actiontoken` — not matched   | No ✓   |
| `CancellationToken`       | Would only appear if serialised directly  | Unlikely|
| `ResetToken`              | Password reset reference in audit data    | Depends on serialisation |

### Concrete False-Positive Scenario

If an audit log payload includes a workflow action token for traceability:

```json
{ "WorkflowStep": "Disbursement", "Token": "WF-2024-001-APPROVE" }
```

This will be written to the audit log as:

```json
{ "WorkflowStep": "Disbursement", "Token": "********" }
```

Auditors lose the workflow token reference, reducing traceability without any privacy benefit — `"WF-2024-001-APPROVE"` contains no PII.

---

## Root Cause

`"token"` was included to capture API authentication tokens (JWT bearer tokens, API keys passed as `token` parameters). This is a valid security concern, but the field name is ambiguous. The actual sensitive token fields should be named more specifically.

---

## Recommended Fix

Replace the bare `"token"` entry with the specific token-related field names that actually carry sensitive credentials:

```csharp
private static readonly HashSet<string> SensitiveFieldNames = new(StringComparer.OrdinalIgnoreCase)
{
    "bvn",
    "nin",
    "accountnumber",
    "account_number",
    "password",
    "secret",
    // Replace bare "token" with specific sensitive token field names:
    "accesstoken",
    "access_token",
    "refreshtoken",
    "refresh_token",
    "bearertoken",
    "bearer_token",
    "authtoken",
    "auth_token",
    "idtoken",
    "id_token",
    "apikey",
    "api_key",
    "creditcardnumber",
    "credit_card_number",
    "cvv",
    "ssn",
    "socialsecuritynumber",
    "rawbureauresponse",
    "raw_bureau_response",
    "rawresponsejson",
    "raw_response_json"
};
```

Also update `MaskSensitiveValue()` to handle the new token field names:

```csharp
"accesstoken" or "refreshtoken" or "bearertoken" or "authtoken" or "idtoken"
or "apikey" or "password" or "secret" => "********",
```

---

## Acceptance Criteria

- `"token"` removed from `SensitiveFieldNames`
- Specific token field names (`accesstoken`, `refreshtoken`, `bearertoken`, `authtoken`, `idtoken`) added
- `MaskSensitiveValue()` updated to match the new field names
- A JSON payload with `{ "Token": "WF-2024-001-APPROVE" }` is NOT masked
- A JSON payload with `{ "AccessToken": "eyJhbGci..." }` IS masked to `"********"`
