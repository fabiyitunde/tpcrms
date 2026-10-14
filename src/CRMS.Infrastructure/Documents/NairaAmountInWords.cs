using System.Text;

namespace CRMS.Infrastructure.Documents;

/// <summary>
/// Spells a naira amount out in words, the way the bank's offer letters state it:
/// "Two hundred and forty-five million, five hundred and sixty-three thousand, seven hundred and
/// four naira, zero kobo".
/// <para>
/// Lives beside BoaBrand rather than in a loan track's own folder — an amount in words is a
/// formatting concern every offer document shares, not RH-SHF's.
/// </para>
/// <para>
/// British convention throughout, because that is what the approved templates use: "and" before
/// the final group under a hundred ("five hundred and four"), hyphens inside the tens
/// ("forty-five"), commas between scale groups, and the short scale where a billion is a thousand
/// million.
/// </para>
/// </summary>
public static class NairaAmountInWords
{
    private static readonly string[] Ones =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen",
        "nineteen",
    ];

    private static readonly string[] Tens =
    [
        "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety",
    ];

    /// <summary>Largest first — the amount is decomposed by dividing through these in order.</summary>
    private static readonly (long Value, string Name)[] Scales =
    [
        (1_000_000_000_000L, "trillion"),
        (1_000_000_000L, "billion"),
        (1_000_000L, "million"),
        (1_000L, "thousand"),
    ];

    /// <summary>
    /// The full phrase including both currency units, as the offer letter's body states it:
    /// "Two hundred and forty-five million ... naira, zero kobo". Sentence-cased.
    /// <para>
    /// Kobo are rounded to the nearest whole kobo, and a carry is honoured — 99.999 reads as "one
    /// hundred naira, zero kobo", never "ninety-nine naira, one hundred kobo".
    /// </para>
    /// </summary>
    public static string Format(decimal amount)
    {
        if (amount < 0)
            return "minus " + Format(-amount);

        var totalKobo = decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);
        var naira = (long)(totalKobo / 100m);
        var kobo = (int)(totalKobo - (naira * 100L));

        var words = $"{Convert(naira)} naira, {Convert(kobo)} kobo";
        return char.ToUpperInvariant(words[0]) + words[1..];
    }

    /// <summary>
    /// Upper-cased for the letter's subject line, which is set in capitals:
    /// "PROVISIONAL LOAN OFFER OF ₦245,563,704.00 (TWO HUNDRED AND FORTY-FIVE MILLION ... ) ONLY".
    /// </summary>
    public static string FormatUpper(decimal amount) => Format(amount).ToUpperInvariant();

    /// <summary>Spells a whole number. Public for the odd caller that needs a count rather than
    /// money — a farmer count, a tenor in months — without the currency units.</summary>
    public static string Convert(long value)
    {
        if (value == 0) return Ones[0];
        if (value < 0) return "minus " + Convert(-value);

        var parts = new List<string>();
        var remaining = value;

        foreach (var (scaleValue, scaleName) in Scales)
        {
            if (remaining < scaleValue) continue;

            var count = remaining / scaleValue;
            remaining -= count * scaleValue;
            parts.Add($"{UnderThousand(count)} {scaleName}");
        }

        var result = string.Join(", ", parts);
        if (remaining == 0)
            return result;

        var tail = UnderThousand(remaining);
        if (parts.Count == 0)
            return tail;

        // A final group under a hundred joins with "and" and no comma — "one thousand and four",
        // not "one thousand, and four". At a hundred or above the group carries its own internal
        // "and" ("seven hundred and four"), so the groups are separated by a comma instead.
        return remaining < 100 ? $"{result} and {tail}" : $"{result}, {tail}";
    }

    /// <summary>A value in 1..999, with the internal "and" British usage puts before the tens.</summary>
    private static string UnderThousand(long value)
    {
        var sb = new StringBuilder();
        var hundreds = value / 100;
        var rest = value % 100;

        if (hundreds > 0)
        {
            sb.Append(Ones[hundreds]).Append(" hundred");
            if (rest > 0) sb.Append(" and ");
        }

        if (rest > 0) sb.Append(UnderHundred(rest));

        return sb.ToString();
    }

    private static string UnderHundred(long value)
    {
        if (value < 20) return Ones[value];

        var tens = Tens[value / 10];
        var ones = value % 10;
        return ones == 0 ? tens : $"{tens}-{Ones[ones]}";
    }
}
