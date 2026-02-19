using System.Text.Json;
using System.Text.RegularExpressions;

namespace CRMS.Domain.Services;

/// <summary>
/// Utility for masking sensitive data before audit logging.
/// </summary>
public static class SensitiveDataMasker
{
    private static readonly HashSet<string> SensitiveFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bvn",
        "nin",
        "accountnumber",
        "account_number",
        "password",
        "secret",
        "token",
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

    /// <summary>
    /// Mask a BVN (show first 3 and last 2 digits).
    /// Example: 22234567890 -> 222****90
    /// </summary>
    public static string MaskBvn(string? bvn)
    {
        if (string.IsNullOrEmpty(bvn) || bvn.Length < 5)
            return "****";

        return $"{bvn[..3]}****{bvn[^2..]}";
    }

    /// <summary>
    /// Mask an account number (show last 4 digits).
    /// Example: 0012345678 -> ******5678
    /// </summary>
    public static string MaskAccountNumber(string? accountNumber)
    {
        if (string.IsNullOrEmpty(accountNumber) || accountNumber.Length < 4)
            return "****";

        return $"******{accountNumber[^4..]}";
    }

    /// <summary>
    /// Mask a phone number (show last 4 digits).
    /// Example: +2348012345678 -> +234******5678
    /// </summary>
    public static string MaskPhoneNumber(string? phone)
    {
        if (string.IsNullOrEmpty(phone) || phone.Length < 4)
            return "****";

        if (phone.StartsWith("+") && phone.Length > 7)
            return $"{phone[..4]}******{phone[^4..]}";

        return $"******{phone[^4..]}";
    }

    /// <summary>
    /// Mask an email address (show first 2 chars and domain).
    /// Example: john.doe@example.com -> jo****@example.com
    /// </summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            return "****@****.***";

        var parts = email.Split('@');
        var local = parts[0];
        var domain = parts[1];

        var maskedLocal = local.Length > 2 ? $"{local[..2]}****" : "****";
        return $"{maskedLocal}@{domain}";
    }

    /// <summary>
    /// Mask sensitive fields in a JSON string.
    /// </summary>
    public static string? MaskJsonSensitiveFields(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return json;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var maskedObj = MaskJsonElement(doc.RootElement);
            return JsonSerializer.Serialize(maskedObj);
        }
        catch
        {
            // If JSON parsing fails, return original
            return json;
        }
    }

    private static object? MaskJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object?>();
                foreach (var property in element.EnumerateObject())
                {
                    var key = property.Name;
                    var value = property.Value;

                    if (SensitiveFieldNames.Contains(key.Replace("_", "")))
                    {
                        // Mask the sensitive value
                        dict[key] = MaskSensitiveValue(key, value);
                    }
                    else
                    {
                        dict[key] = MaskJsonElement(value);
                    }
                }
                return dict;

            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(MaskJsonElement(item));
                }
                return list;

            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var longVal))
                    return longVal;
                return element.GetDecimal();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            default:
                return null;
        }
    }

    private static string MaskSensitiveValue(string fieldName, JsonElement value)
    {
        var strValue = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        
        var normalizedField = fieldName.ToLowerInvariant().Replace("_", "");

        return normalizedField switch
        {
            "bvn" => MaskBvn(strValue),
            "nin" => MaskBvn(strValue), // Same masking pattern as BVN
            "accountnumber" => MaskAccountNumber(strValue),
            "password" or "secret" or "token" or "apikey" => "********",
            "rawbureauresponse" or "rawresponsejson" => "[REDACTED - Sensitive Data]",
            _ => "****"
        };
    }

    /// <summary>
    /// Create a masked copy of an object for audit logging.
    /// </summary>
    public static object? MaskObjectForAudit(object? obj)
    {
        if (obj == null) return null;

        var json = JsonSerializer.Serialize(obj);
        var maskedJson = MaskJsonSensitiveFields(json);
        
        return maskedJson != null ? JsonSerializer.Deserialize<object>(maskedJson) : null;
    }
}
