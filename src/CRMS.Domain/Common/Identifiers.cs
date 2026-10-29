namespace CRMS.Domain.Common;

/// <summary>
/// Normalises BVN, NIN and account numbers once, when they are written, so lookups can compare exact
/// values on an indexed column. A function in the WHERE clause (REPLACE, TRIM) would defeat the
/// index, which is why the cleaning happens here rather than at query time.
/// </summary>
public static class Identifiers
{
    /// <summary>
    /// Strips the separators people type into a number (spaces, hyphens, dots). Returns null for a
    /// blank value. A value that still holds anything other than digits is returned trimmed but
    /// otherwise as given, so a mistyped value is kept for an officer to see rather than silently
    /// turned into a different number.
    /// </summary>
    public static string? Digits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        var stripped = new string(trimmed.Where(c => c is not (' ' or '-' or '.' or '\t')).ToArray());
        return stripped.Length > 0 && stripped.All(char.IsAsciiDigit) ? stripped : trimmed;
    }
}
