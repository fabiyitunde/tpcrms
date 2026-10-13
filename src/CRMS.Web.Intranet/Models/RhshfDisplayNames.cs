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
        _ => System.Text.RegularExpressions.Regex.Replace(category.ToString(), "(?<!^)([A-Z])", " $1"),
    };
}
