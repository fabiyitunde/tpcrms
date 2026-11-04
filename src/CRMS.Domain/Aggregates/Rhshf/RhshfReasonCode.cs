using System.Text.RegularExpressions;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// A reason staff pick when they return a case to the FAC or decline it, sent to the portal as a
/// code beside the free-text note (CRMSQA#1). Administrator-maintained: the CRMS ships a default list
/// and BOA changes it when it settles its own, rather than the CRMS waiting on that decision.
/// <para>
/// The code is what the portal stores and matches on, so once created it never changes and is never
/// deleted. An unwanted one is switched off: past cases keep it, new ones cannot pick it. The label is
/// wording and can be edited.
/// </para>
/// </summary>
public partial class RhshfReasonCode : AggregateRoot
{
    /// <summary>Upper case with underscores, e.g. DOCUMENT_MISSING.</summary>
    public string Code { get; private set; } = string.Empty;
    public RhshfReasonCodeType Type { get; private set; }
    /// <summary>The wording staff pick from and the portal may show.</summary>
    public string Label { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>The fallback in every list: "see the note". Never switched off, so a reason can
    /// always be given.</summary>
    public const string Other = "OTHER";

    protected RhshfReasonCode() { }

    public static Result<RhshfReasonCode> Create(string code, RhshfReasonCodeType type, string label, int sortOrder)
    {
        var normalised = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (!CodePattern().IsMatch(normalised))
            return Result.Failure<RhshfReasonCode>("A code is 2 to 50 capital letters, digits and underscores, starting with a letter, e.g. DOCUMENT_MISSING.");
        if (string.IsNullOrWhiteSpace(label))
            return Result.Failure<RhshfReasonCode>("Give the reason's wording.");

        return Result.Success(new RhshfReasonCode
        {
            Code = normalised,
            Type = type,
            Label = Cap(label),
            SortOrder = sortOrder,
            IsActive = true,
        });
    }

    public Result Update(string label, int sortOrder, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(label))
            return Result.Failure("Give the reason's wording.");
        if (!isActive && Code == Other)
            return Result.Failure("OTHER cannot be switched off; it is the reason staff fall back on.");

        Label = Cap(label);
        SortOrder = sortOrder;
        IsActive = isActive;
        return Result.Success();
    }

    private static string Cap(string label)
    {
        var v = label.Trim();
        return v.Length > 200 ? v[..200] : v;
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,49}$")]
    private static partial Regex CodePattern();
}
