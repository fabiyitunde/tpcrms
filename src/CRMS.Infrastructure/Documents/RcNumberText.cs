namespace CRMS.Infrastructure.Documents;

/// <summary>
/// Renders a company's RC number the way a document should read it.
/// <para>
/// Nigerian registration numbers are commonly held with the "RC" prefix already on them
/// ("RC771204") — that is how CAC returns them and how the portal sends them. Documents that
/// prepended their own prefix therefore printed "(RC RC771204)" on the borrower line and again in
/// the acceptance block, which is the first thing a reader notices on a contractual page.
/// </para>
/// <para>
/// Lives beside <see cref="NairaAmountInWords"/> rather than in a loan track's folder: naming the
/// borrower is a formatting concern every offer document shares.
/// </para>
/// </summary>
public static class RcNumberText
{
    /// <summary>
    /// The number with exactly one "RC" prefix — "RC 771204" whether the stored value was
    /// "771204", "RC771204", "rc-771204" or already spaced. Empty when nothing is on file.
    /// </summary>
    public static string Format(string? rcNumber)
    {
        var trimmed = rcNumber?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return string.Empty;

        // Strip any prefix the stored value already carries, along with whatever separates it from
        // the digits, then put back the single canonical one.
        if (trimmed.StartsWith("RC", StringComparison.OrdinalIgnoreCase))
        {
            var rest = trimmed[2..].TrimStart(' ', '-', '/', '.', ':', '\t');

            // "RC" on its own, or followed by nothing usable, is not a number — leave the original
            // text alone rather than printing a bare prefix.
            if (rest.Length > 0)
                trimmed = rest;
        }

        return $"RC {trimmed}";
    }

    /// <summary>"(RC 771204)", or an empty string when no number is on file — so a caller can
    /// append it unconditionally.</summary>
    public static string Parenthesised(string? rcNumber)
    {
        var formatted = Format(rcNumber);
        return formatted.Length == 0 ? string.Empty : $" ({formatted})";
    }

    /// <summary>"Sagad Farms Limited (RC 771204)", or just the name where no number is held.</summary>
    public static string WithCompany(string companyName, string? rcNumber)
        => companyName + Parenthesised(rcNumber);
}
