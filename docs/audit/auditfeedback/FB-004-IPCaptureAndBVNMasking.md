# Feedback Issue FB-004: IP Capture and BVN Masking Incomplete

**Original Issues:** H-19 (HIGH), H-20 (HIGH)
**Phase Claimed Fixed:** D
**Verified Status:** PARTIALLY FIXED ⚠️
**Severity:** HIGH — Forensic and compliance gaps remain

---

## H-19: IP Address Not Captured in Audit Logs

### What Was Done

The `AuditService.LogAsync()` method accepts `ipAddress` as an optional parameter. `LogLoginAsync()` also accepts it. The signature is correct.

### What Is Still Missing

`AuditService` does not inject `IHttpContextAccessor`. The IP is only captured if each caller individually extracts it from the request context and passes it through. In practice:

- **Domain event handlers** (the majority of audit-triggering paths) have no HTTP context access. `CommitteeDecisionWorkflowHandler`, `AuditEventHandlers`, `WorkflowIntegrationHandlers` — none of these can supply the originating request IP.
- **Direct API controller calls** (login, configuration changes) may supply it if the developer remembers to extract `HttpContext.Connection.RemoteIpAddress`.

Result: most audit log entries will have `IpAddress = null`.

### Recommended Fix

**Approach A — Scoped IP Context Service (Recommended)**

Create a scoped service that middleware populates at the start of each request:

```csharp
// 1. Define the service
public interface IAuditContextService
{
    string? ClientIpAddress { get; }
    string? UserAgent { get; }
}

public class HttpAuditContextService : IAuditContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpAuditContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? ClientIpAddress =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent =>
        _httpContextAccessor.HttpContext?.Request.Headers.UserAgent;
}

// 2. Register in DI
services.AddHttpContextAccessor();
services.AddScoped<IAuditContextService, HttpAuditContextService>();

// 3. Inject into AuditService
public class AuditService
{
    private readonly IAuditContextService _auditContext;
    // ...

    public async Task LogAsync(...)
    {
        var ip = ipAddress ?? _auditContext.ClientIpAddress; // use caller-supplied or auto-capture
        // ...
    }
}
```

For background/event-driven contexts, `IHttpContextAccessor.HttpContext` will be null, so IP will remain null — which is semantically correct (background jobs have no originating HTTP request).

---

## H-20: BVN and Sensitive Data Masking Not Implemented

### What Was Done

Nothing — the `AuditService.LogAsync()` serialization is unchanged:

```csharp
oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
newValues != null ? JsonSerializer.Serialize(newValues) : null,
```

### What Is Still Missing

No masking layer exists. If any caller passes an object containing `BVN`, `NIN`, `AccountNumber`, or similar fields as `oldValues`/`newValues`, the raw value will appear in the `OldValues` or `NewValues` JSON column of the `AuditLogs` table.

### Recommended Fix

Implement a sensitive field masking serializer:

```csharp
// Sensitive field names to mask (case-insensitive)
private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
{
    "BVN", "NIN", "AccountNumber", "CreditCardNumber",
    "Password", "PasswordHash", "Pin", "CVV"
};

private static string? SerializeWithMasking(object? value)
{
    if (value == null) return null;

    var json = JsonSerializer.Serialize(value);
    using var doc = JsonDocument.Parse(json);
    using var ms = new MemoryStream();
    using var writer = new Utf8JsonWriter(ms);

    MaskJsonElement(doc.RootElement, writer);
    writer.Flush();

    return System.Text.Encoding.UTF8.GetString(ms.ToArray());
}

private static void MaskJsonElement(JsonElement element, Utf8JsonWriter writer)
{
    switch (element.ValueKind)
    {
        case JsonValueKind.Object:
            writer.WriteStartObject();
            foreach (var prop in element.EnumerateObject())
            {
                writer.WritePropertyName(prop.Name);
                if (SensitiveFields.Contains(prop.Name) && prop.Value.ValueKind == JsonValueKind.String)
                {
                    var raw = prop.Value.GetString() ?? "";
                    // Show first 4 and last 2 characters, mask the rest
                    var masked = raw.Length > 6
                        ? raw[..4] + new string('*', raw.Length - 6) + raw[^2..]
                        : "****";
                    writer.WriteStringValue(masked);
                }
                else
                {
                    MaskJsonElement(prop.Value, writer);
                }
            }
            writer.WriteEndObject();
            break;

        case JsonValueKind.Array:
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray())
                MaskJsonElement(item, writer);
            writer.WriteEndArray();
            break;

        default:
            element.WriteTo(writer);
            break;
    }
}
```

Then replace:
```csharp
// Before
oldValues != null ? JsonSerializer.Serialize(oldValues) : null,

// After
SerializeWithMasking(oldValues),
```

---

## Test Scenarios

### H-19 IP Capture
- [ ] Login request → `AuditLog.IpAddress` is populated with client IP
- [ ] Committee vote (via HTTP request) → `AuditLog.IpAddress` populated
- [ ] Background job audit entry → `AuditLog.IpAddress` is null (expected)

### H-20 BVN Masking
- [ ] Log an action with an object containing `BVN: "22234567890"` → stored as `"2223****90"`
- [ ] Log an action with `AccountNumber: "0123456789"` → stored as `"0123****89"`
- [ ] Log an action with no sensitive fields → stored verbatim (no masking applied to non-sensitive data)
- [ ] Nested objects with BVN field → masking applied recursively
