using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CRMS.Infrastructure.ExternalServices.SmartComply;

/// <summary>
/// Handles SmartComply API fields that are returned with inconsistent types.
/// Observed patterns:
///   - int fields returned as floats:    "maxNoOfDays": 30.0
///   - decimal fields returned as strings: "loanAmount": "500000"
///   - DateTime? fields returned as empty strings: "dateReported": ""
///   - DateTime? fields returned as null
/// All converters degrade gracefully to default/null rather than throwing.
/// </summary>

public class FlexibleDecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetDecimal();

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s)) return 0m;
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                return result;
            return 0m;
        }

        if (reader.TokenType == JsonTokenType.Null)
            return 0m;

        throw new JsonException($"Cannot convert {reader.TokenType} to decimal");
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}

public class FlexibleNullableDecimalConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetDecimal();

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                return result;
            return null;
        }

        throw new JsonException($"Cannot convert {reader.TokenType} to decimal?");
    }

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value.HasValue) writer.WriteNumberValue(value.Value);
        else writer.WriteNullValue();
    }
}

public class FlexibleIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out var i)) return i;
            return (int)reader.GetDecimal();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s)) return 0;
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return (int)d;
            return 0;
        }

        if (reader.TokenType == JsonTokenType.Null)
            return 0;

        throw new JsonException($"Cannot convert {reader.TokenType} to int");
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}

public class FlexibleNullableDateTimeConverter : JsonConverter<DateTime?>
{
    private static readonly string[] Formats =
    [
        "yyyy-MM-ddTHH:mm:ss.fffZ",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd",
        "MM/dd/yyyy",
        "dd/MM/yyyy",
        "dd-MM-yyyy",
        "MM-dd-yyyy",
    ];

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s)) return null;

            // Try built-in ISO 8601 first
            if (reader.TryGetDateTime(out var dt)) return dt;

            // Try common formats
            if (DateTime.TryParseExact(s, Formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out var dt2))
                return dt2;

            // Last resort
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt3))
                return dt3;

            // Unrecognised string — return null rather than throw
            return null;
        }

        throw new JsonException($"Cannot convert {reader.TokenType} to DateTime?");
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue) writer.WriteStringValue(value.Value.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        else writer.WriteNullValue();
    }
}

/// <summary>
/// Handles string fields that SmartComply sometimes returns as arrays, objects, or numbers.
/// Skips the entire token and returns null rather than throwing.
/// </summary>
public class FlexibleStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                return reader.GetDecimal().ToString(CultureInfo.InvariantCulture);
            case JsonTokenType.True:
                return "true";
            case JsonTokenType.False:
                return "false";
            // Array or object — skip the entire token, return null
            case JsonTokenType.StartArray:
            case JsonTokenType.StartObject:
                reader.TrySkip();
                return null;
            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value);
    }
}

internal static class SmartComplyJsonHelpers
{
    public static int? ReadInt(JsonElement e, string name)
    {
        if (e.TryGetProperty(name, out var p))
        {
            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i)) return i;
            if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var j)) return j;
        }
        return null;
    }

    public static long? ReadLong(JsonElement e, string name)
    {
        if (e.TryGetProperty(name, out var p))
        {
            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var i)) return i;
            if (p.ValueKind == JsonValueKind.String && long.TryParse(p.GetString(), out var j)) return j;
        }
        return null;
    }

    public static string? ReadString(JsonElement e, string name)
        => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}

/// <summary>
/// SmartComply returns FK reference fields (e.g. countryFk) sometimes as the embedded object
/// {id, code, name} and sometimes as just the bare foreign-key id (a number) or a string.
/// Default deserialization throws on the scalar form ("The JSON value could not be converted to
/// CacCountryReference"). This converter accepts either shape: an object is mapped normally; a bare
/// number/string is kept as the Id (or Name) so the CAC fetch never fails on this field.
/// </summary>
public class FlexibleCacCountryReferenceConverter : JsonConverter<CacCountryReference?>
{
    public override CacCountryReference? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.StartObject:
                using (var doc = JsonDocument.ParseValue(ref reader))
                {
                    var e = doc.RootElement;
                    return new CacCountryReference
                    {
                        Id = SmartComplyJsonHelpers.ReadInt(e, "id"),
                        Code = SmartComplyJsonHelpers.ReadString(e, "code"),
                        Name = SmartComplyJsonHelpers.ReadString(e, "name"),
                    };
                }
            case JsonTokenType.Number:
                return reader.TryGetInt32(out var n) ? new CacCountryReference { Id = n } : new CacCountryReference();
            case JsonTokenType.String:
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s)) return null;
                return int.TryParse(s, out var id) ? new CacCountryReference { Id = id } : new CacCountryReference { Name = s };
            default:
                reader.TrySkip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, CacCountryReference? value, JsonSerializerOptions options)
    {
        if (value is null) { writer.WriteNullValue(); return; }
        writer.WriteStartObject();
        if (value.Id.HasValue) writer.WriteNumber("id", value.Id.Value);
        if (value.Code is not null) writer.WriteString("code", value.Code);
        if (value.Name is not null) writer.WriteString("name", value.Name);
        writer.WriteEndObject();
    }
}

/// <summary>Same lenient object-or-scalar handling as <see cref="FlexibleCacCountryReferenceConverter"/>, for affiliateType.</summary>
public class FlexibleCacAffiliateTypeReferenceConverter : JsonConverter<CacAffiliateTypeReference?>
{
    public override CacAffiliateTypeReference? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.StartObject:
                using (var doc = JsonDocument.ParseValue(ref reader))
                {
                    var e = doc.RootElement;
                    return new CacAffiliateTypeReference
                    {
                        Id = SmartComplyJsonHelpers.ReadLong(e, "id"),
                        Name = SmartComplyJsonHelpers.ReadString(e, "name"),
                        Description = SmartComplyJsonHelpers.ReadString(e, "description"),
                    };
                }
            case JsonTokenType.Number:
                return reader.TryGetInt64(out var n) ? new CacAffiliateTypeReference { Id = n } : new CacAffiliateTypeReference();
            case JsonTokenType.String:
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s)) return null;
                return long.TryParse(s, out var id) ? new CacAffiliateTypeReference { Id = id } : new CacAffiliateTypeReference { Name = s };
            default:
                reader.TrySkip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, CacAffiliateTypeReference? value, JsonSerializerOptions options)
    {
        if (value is null) { writer.WriteNullValue(); return; }
        writer.WriteStartObject();
        if (value.Id.HasValue) writer.WriteNumber("id", value.Id.Value);
        if (value.Name is not null) writer.WriteString("name", value.Name);
        if (value.Description is not null) writer.WriteString("description", value.Description);
        writer.WriteEndObject();
    }
}

public class FlexibleDateTimeConverter : JsonConverter<DateTime>
{
    private static readonly string[] Formats =
    [
        "yyyy-MM-ddTHH:mm:ss.fffZ",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd",
        "MM/dd/yyyy",
        "dd/MM/yyyy",
        "dd-MM-yyyy",
        "MM-dd-yyyy",
    ];

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return default;

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s)) return default;

            if (reader.TryGetDateTime(out var dt)) return dt;

            if (DateTime.TryParseExact(s, Formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out var dt2))
                return dt2;

            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt3))
                return dt3;

            return default;
        }

        throw new JsonException($"Cannot convert {reader.TokenType} to DateTime");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("yyyy-MM-ddTHH:mm:ssZ"));
}

/// <summary>
/// Catch-all tolerant converter for value types. SmartComply returns scalars with inconsistent JSON
/// types (bools as 0/1 or "Y"/"N", ints as floats or quoted strings, etc.). This factory supplies a
/// lenient converter for EVERY primitive value type and its Nullable&lt;&gt; form, parsing across
/// number / string / bool tokens — and it NEVER throws: anything it can't parse degrades to
/// default/null. This is the blanket safety net so per-field "could not be converted" crashes stop
/// recurring. Types already handled by the explicit Flexible* converters (registered earlier) are
/// excluded so their date-format-aware behaviour is preserved.
/// </summary>
public sealed class TolerantPrimitiveConverterFactory : JsonConverterFactory
{
    private static readonly HashSet<Type> SupportedUnderlying = new()
    {
        typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
        typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(float), typeof(double), typeof(decimal),
        typeof(DateTime), typeof(DateTimeOffset), typeof(Guid),
    };

    // Left to the explicit converters registered before this factory.
    private static readonly HashSet<Type> AlreadyHandled = new()
    {
        typeof(decimal), typeof(decimal?), typeof(int), typeof(DateTime), typeof(DateTime?),
    };

    public override bool CanConvert(Type typeToConvert)
    {
        if (AlreadyHandled.Contains(typeToConvert)) return false;
        var underlying = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
        return SupportedUnderlying.Contains(underlying);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter)Activator.CreateInstance(
            typeof(TolerantPrimitiveConverter<>).MakeGenericType(typeToConvert))!;
}

public sealed class TolerantPrimitiveConverter<T> : JsonConverter<T>
{
    private static readonly Type U = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

    private static readonly string[] DateFormats =
    [
        "yyyy-MM-ddTHH:mm:ss.fffZ", "yyyy-MM-ddTHH:mm:ssZ", "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy", "dd-MM-yyyy", "MM-dd-yyyy",
    ];

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return default;
                case JsonTokenType.True:
                case JsonTokenType.False:
                    return FromBool(reader.GetBoolean());
                case JsonTokenType.Number:
                    return FromDecimal(reader.GetDecimal());
                case JsonTokenType.String:
                    return FromString(reader.GetString());
                case JsonTokenType.StartArray:
                case JsonTokenType.StartObject:
                    reader.Skip();
                    return default;
                default:
                    return default;
            }
        }
        catch
        {
            return default; // never throw — degrade gracefully
        }
    }

    private static T? FromBool(bool b)
    {
        if (U == typeof(bool)) return Box(b);
        if (IsNumber(U)) return Box(Convert.ChangeType(b ? 1 : 0, U, CultureInfo.InvariantCulture));
        return default;
    }

    private static T? FromDecimal(decimal d)
    {
        if (U == typeof(bool)) return Box(d != 0m);
        if (IsNumber(U)) return Box(Convert.ChangeType(d, U, CultureInfo.InvariantCulture));
        return default; // a number for a date/guid — unsupported, default
    }

    private static T? FromString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return default;
        var s = raw.Trim();

        if (U == typeof(bool))
        {
            return s.ToLowerInvariant() switch
            {
                "true" or "1" or "yes" or "y" or "t" => Box(true),
                "false" or "0" or "no" or "n" or "f" => Box(false),
                _ => bool.TryParse(s, out var b) ? Box(b) : default,
            };
        }
        if (U == typeof(Guid))
            return Guid.TryParse(s, out var g) ? Box(g) : default;
        if (U == typeof(DateTime))
            return TryDate(s, out var dt) ? Box(dt) : default;
        if (U == typeof(DateTimeOffset))
            return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto) ? Box(dto) : default;
        if (IsNumber(U))
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var dec)
                ? Box(Convert.ChangeType(dec, U, CultureInfo.InvariantCulture))
                : default;
        return default;
    }

    private static bool TryDate(string s, out DateTime dt)
    {
        if (DateTime.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out dt)) return true;
        return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out dt);
    }

    private static bool IsNumber(Type t) =>
        t == typeof(byte) || t == typeof(sbyte) || t == typeof(short) || t == typeof(ushort) ||
        t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong) ||
        t == typeof(float) || t == typeof(double) || t == typeof(decimal);

    private static T Box(object value) => (T)value;

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value); // default options (no factory) — no recursion
}
