using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CRMS.Infrastructure.ExternalServices.SmartComply;

/// <summary>
/// Lists the fields of a JSON payload that a DTO type has no property for — i.e. what the
/// deserializer silently drops. Matching is case-insensitive, as the provider's serializer options
/// are. Array items are merged under "name[]" so a list of 40 loans reports each field once.
/// </summary>
internal static class JsonFieldAudit
{
    public static IReadOnlyList<string> Unread(string json, Type dtoType)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var found = new SortedSet<string>(StringComparer.Ordinal);
            Walk(doc.RootElement, dtoType, "", found);
            return found.ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void Walk(JsonElement element, Type type, string path, ISet<string> found)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (element.ValueKind == JsonValueKind.Array)
        {
            var itemType = ItemType(type);
            if (itemType is null) return;
            foreach (var item in element.EnumerateArray())
                Walk(item, itemType, path + "[]", found);
            return;
        }

        if (element.ValueKind != JsonValueKind.Object || IsLeaf(type)) return;

        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is null)
            .GroupBy(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().PropertyType, StringComparer.OrdinalIgnoreCase);

        foreach (var prop in element.EnumerateObject())
        {
            var childPath = path.Length == 0 ? prop.Name : $"{path}.{prop.Name}";
            if (props.TryGetValue(prop.Name, out var childType))
                Walk(prop.Value, childType, childPath, found);
            else
                found.Add(childPath);
        }
    }

    // Types the deserializer consumes whole; nothing inside them can go unread.
    private static bool IsLeaf(Type type) =>
        type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
        || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid)
        || type == typeof(object) || type == typeof(JsonElement)
        || typeof(IDictionary).IsAssignableFrom(type)
        || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>));

    private static Type? ItemType(Type type)
    {
        if (type.IsArray) return type.GetElementType();
        if (type == typeof(string)) return null;
        return type.GetInterfaces().Append(type)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];
    }
}
