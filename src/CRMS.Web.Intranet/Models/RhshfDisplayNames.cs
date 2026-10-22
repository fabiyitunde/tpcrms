using CRMS.Domain.Enums;

namespace CRMS.Web.Intranet.Models;

/// <summary>
/// Staff-facing labels for RH-SHF enums whose member name does not read the way the programme team
/// names the thing. Most categories are fine split on capitals ("OffTakeAgreement" → "Off Take
/// Agreement"), so this stays a short list of deliberate exceptions rather than a full lookup table
/// that would silently drift from the enum.
/// </summary>
public static class RhshfDisplayNames
{
    /// <summary>
    /// A document category as staff should read it.
    /// <para>
    /// <see cref="RhshfDocumentCategory.FarmerRegister"/> is the one override: the programme team
    /// renamed this document from "Out-Grower Register" to <b>Farmers Register</b> (2026-10-13), and
    /// the FAC uploading it sees that name because it comes from the requirement's admin-editable
    /// Title. Staff reviewing the upload must see the same name, and the enum member cannot be
    /// renamed to match — it is persisted by name on RhshfSupportingDocuments and
    /// RhshfDocumentRequirements, so renaming would orphan every existing row.
    /// </para>
    /// </summary>
    public static string CategoryLabel(RhshfDocumentCategory category) => category switch
    {
        RhshfDocumentCategory.FarmerRegister => "Farmers Register",
        _ => SplitOnCapitals(category.ToString()),
    };

    /// <summary>A guarantor relationship category as it should read in a dropdown — "BusinessPartner"
    /// becomes "Business Partner". No overrides needed: the member names were chosen to match the
    /// wording the programme team approved.</summary>
    public static string GuarantorRelationshipLabel(RhshfGuarantorRelationship relationship) =>
        SplitOnCapitals(relationship.ToString());

    /// <summary>
    /// The file picker's accept filter for a slot, so the FAC's file browser offers what will
    /// actually be taken. Empty where the slot accepts anything, which leaves the picker unfiltered.
    /// </summary>
    public static string AcceptAttribute(IReadOnlyList<RhshfDocumentFormat>? formats)
    {
        if (formats is null || formats.Count == 0)
            return string.Empty;

        return string.Join(",", formats.SelectMany(f => f switch
        {
            RhshfDocumentFormat.Pdf => new[] { ".pdf" },
            RhshfDocumentFormat.Spreadsheet => new[] { ".xlsx" },
            RhshfDocumentFormat.Image => new[] { ".png", ".jpg", ".jpeg" },
            _ => Array.Empty<string>(),
        }).Distinct());
    }

    /// <summary>What the slot accepts, in a sentence the FAC can act on before choosing a file.</summary>
    public static string AcceptedFormatsText(IReadOnlyList<RhshfDocumentFormat>? formats)
    {
        if (formats is null || formats.Count == 0)
            return string.Empty;

        var names = formats.Select(f => f switch
        {
            RhshfDocumentFormat.Pdf => "PDF",
            RhshfDocumentFormat.Spreadsheet => "Excel (.xlsx)",
            RhshfDocumentFormat.Image => "PNG or JPEG image",
            _ => f.ToString(),
        }).Distinct().ToList();

        return names.Count == 1
            ? $"Accepts {names[0]}."
            : $"Accepts {string.Join(", ", names.Take(names.Count - 1))} or {names[^1]}.";
    }

    private static string SplitOnCapitals(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, "(?<!^)([A-Z])", " $1");
}
