namespace CRMS.Application.Rhshf.Services;

/// <summary>
/// Recognises a core banking product charge as one of the charges the EOP already finances.
/// <para>
/// The programme's rule is that no fee may be financed inside the EOP and then deducted again at
/// booking. CRMS never adds those fees itself, but the offer letter prints whatever the core banking
/// product says it will deduct — so a product configured with insurance or working capital would
/// have the letter promise the borrower exactly the double charge the rule forbids.
/// </para>
/// </summary>
public static class RhshfFinancedChargeMatcher
{
    /// <summary>
    /// Whether a product charge names something already financed.
    /// <para>
    /// The product charge must contain the financed name, never the other way round. A fee called
    /// "Working Capital Fee" is the financed working capital; one called "Capital Gains Levy" is not,
    /// and matching loosely in both directions would quietly drop a legitimate fee off the letter —
    /// a failure nobody would notice until the borrower queried a deduction the letter never
    /// mentioned.
    /// </para>
    /// </summary>
    /// <param name="chargeName">The product charge's name, as core banking returns it.</param>
    /// <param name="financedCharges">Code and label of each charge financed inside the EOP.</param>
    public static bool IsAlreadyFinanced(
        string chargeName, IEnumerable<(string Code, string Label)> financedCharges)
    {
        var name = Normalise(chargeName);
        if (name.Length == 0)
            return false;

        return financedCharges
            .SelectMany(c => Keys(c.Code, c.Label))
            .Any(name.Contains);
    }

    /// <summary>
    /// The names a financed charge might wear in a core banking product: its own label, plus the
    /// known spellings of the three programme charges. A product charge is named by whoever
    /// configured it, so "workingCapital" turns up as "Working Capital Fee" or "WORKING-CAPITAL".
    /// </summary>
    private static IEnumerable<string> Keys(string code, string label)
    {
        // Short labels are skipped: a two- or three-letter label would match most fee names by
        // accident, and suppressing a real fee is worse than missing a collision.
        var normalisedLabel = Normalise(label);
        if (normalisedLabel.Length >= 6)
            yield return normalisedLabel;

        string[] synonyms = Normalise(code) switch
        {
            "insurance" => ["insurance"],
            "workingcapital" => ["workingcapital"],
            "techservicesfee" => ["techservicesfee", "technologyservicesfee", "technologyservice", "techservice"],
            _ => [],
        };

        foreach (var synonym in synonyms)
            yield return synonym;
    }

    /// <summary>Lower-cased letters and digits only, so spacing, casing and punctuation stop mattering.</summary>
    private static string Normalise(string? value) =>
        new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
