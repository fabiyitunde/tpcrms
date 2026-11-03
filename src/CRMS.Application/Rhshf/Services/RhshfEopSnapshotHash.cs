using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Enums;

namespace CRMS.Application.Rhshf.Services;

/// <summary>
/// Recomputes the portal's EOP snapshot hash, scheme <c>rhshf-eop-snapshot-v1</c>, from the payload
/// the CRMS received, following the portal's published rules (CRMSQA#3, 29 Oct, §5). A decision is
/// reported back to the portal against this hash, so the CRMS checks that the hash really describes
/// the EOP it decided on rather than taking the portal's word for it.
/// <para>
/// The canonical text: keys sorted ordinally, no whitespace, every number except <c>farmers</c> written
/// as a string rounded to six decimals with no trailing zeros, ids as sent, and only <c>"</c>,
/// <c>\</c> and control characters escaped. SHA-256 of its UTF-8 bytes, in lowercase hex.
/// </para>
/// </summary>
public static class RhshfEopSnapshotHash
{
    public const string SchemeV1 = "rhshf-eop-snapshot-v1";

    /// <summary>Whether the hash the portal sent for <paramref name="eop"/> matches its content.</summary>
    public static RhshfSnapshotHashCheck Check(RhshfFinancingDto financing, RhshfFinancingEopDto eop)
    {
        if (string.IsNullOrWhiteSpace(eop.SnapshotHash))
            return RhshfSnapshotHashCheck.NotSent;
        if (!string.Equals(eop.SnapshotHashScheme, SchemeV1, StringComparison.Ordinal))
            return RhshfSnapshotHashCheck.UnsupportedScheme;

        return string.Equals(ComputeV1(financing, eop), eop.SnapshotHash.Trim(), StringComparison.OrdinalIgnoreCase)
            ? RhshfSnapshotHashCheck.Verified
            : RhshfSnapshotHashCheck.Mismatch;
    }

    public static string ComputeV1(RhshfFinancingDto financing, RhshfFinancingEopDto eop)
    {
        var lines = (financing.InputLines ?? [])
            .Where(l => l.EopId == eop.EopId)
            // Stable: equal ids keep payload order.
            .OrderBy(l => Id(l.LineId) ?? "", StringComparer.Ordinal)
            .Select(l => (object?)Obj(
                ("amount", Num(l.Amount)),
                ("approvedRate", Num(l.ServiceProvider?.ApprovedRate)),
                ("basis", l.Basis),
                ("category", l.Category),
                ("inLoan", l.InLoan),
                ("item", l.Item),
                ("lineId", Id(l.LineId)),
                ("numberOfPacks", Num(l.NumberOfPacks)),
                ("packSize", Num(l.PackSize)),
                ("packagingExcess", Num(l.PackagingExcess)),
                ("pricePerPack", Num(l.PricePerPack)),
                ("priceVersionId", l.PriceVersionId),
                ("providerId", l.ServiceProvider?.ProviderId),
                ("quantityPerFarmer", Num(l.QuantityPerFarmer)),
                ("supplier", l.Supplier),
                ("supplierProductId", l.SupplierProductId),
                ("unit", l.Unit),
                ("unitPrice", Num(l.UnitPrice))))
            .ToList();

        var root = Obj(
            ("approvedTotal", Num(eop.ApprovedTotal)),
            ("crop", eop.Crop),
            ("eopId", Id(eop.EopId)),
            ("farmers", eop.Farmers),
            ("inputLines", lines),
            ("inputs", Num(eop.Inputs)),
            ("insurance", Num(eop.Insurance)),
            ("loanAmount", Num(eop.LoanAmount)),
            ("packagingExcess", Num(eop.PackagingExcess)),
            ("scheme", eop.SnapshotHashScheme),
            ("state", eop.State),
            ("techServicesFee", Num(eop.TechServicesFee)),
            ("workingCapital", Num(eop.WorkingCapital)));

        var text = new StringBuilder();
        Write(text, root);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant();
    }

    /// <summary>A hashed number: rounded to six decimals half away from zero, invariant, no exponent,
    /// no trailing zeros. Written as a JSON string, so 14688.0 and 14688 hash the same.</summary>
    public static string? Num(decimal? value)
    {
        if (value is not { } v)
            return null;
        var rounded = Math.Round(v, 6, MidpointRounding.AwayFromZero);
        return rounded == 0m ? "0" : rounded.ToString("0.######", CultureInfo.InvariantCulture);
    }

    /// <summary>Lowercase hyphenated, as the portal sends ids.</summary>
    private static string? Id(Guid? id) => id?.ToString("D");

    private static SortedDictionary<string, object?> Obj(params (string Key, object? Value)[] pairs)
    {
        var obj = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
            obj[key] = value;
        return obj;
    }

    private static void Write(StringBuilder sb, object? value)
    {
        switch (value)
        {
            case null:
                sb.Append("null");
                break;
            case bool b:
                sb.Append(b ? "true" : "false");
                break;
            case int i:
                sb.Append(i.ToString(CultureInfo.InvariantCulture));
                break;
            case string s:
                WriteString(sb, s);
                break;
            case SortedDictionary<string, object?> obj:
                sb.Append('{');
                var first = true;
                foreach (var (k, v) in obj)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, k);
                    sb.Append(':');
                    Write(sb, v);
                }
                sb.Append('}');
                break;
            case IEnumerable<object?> list:
                sb.Append('[');
                var firstItem = true;
                foreach (var item in list)
                {
                    if (!firstItem) sb.Append(',');
                    firstItem = false;
                    Write(sb, item);
                }
                sb.Append(']');
                break;
            default:
                throw new InvalidOperationException($"Unexpected value in the EOP snapshot: {value.GetType().Name}");
        }
    }

    private static void WriteString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (var c in s)
        {
            if (c == '"') sb.Append("\\\"");
            else if (c == '\\') sb.Append("\\\\");
            else if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
            else sb.Append(c);
        }
        sb.Append('"');
    }
}
