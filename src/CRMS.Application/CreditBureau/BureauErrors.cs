namespace CRMS.Application.CreditBureau;

/// <summary>
/// Reads a bureau answer kept on a report and says, in plain words, what kind of problem it is.
/// The answer itself is never changed or shortened: staff see it in full under the plain heading,
/// because the exact wording is what tells them (or SmartComply) what went wrong.
/// </summary>
public static class BureauErrors
{
    public const string WalletShortfallText = "Insufficient wallet balance";

    public static bool IsWalletShortfall(string? message) =>
        message?.Contains(WalletShortfallText, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>A short heading and a hint for the card above the full message; null for none.</summary>
    public static (string Title, string Hint)? Explain(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;
        var m = message;
        if (IsWalletShortfall(m))
            return ("SmartComply wallet balance too low",
                "SmartComply refused the call because the wallet behind the bank's key does not have enough funds. Fund it, then run the check again.");
        if (m.Contains("rejected this server's API key", StringComparison.OrdinalIgnoreCase) || m.Contains("Authorization Failed", StringComparison.OrdinalIgnoreCase))
            return ("SmartComply refused the bank's API key",
                "The key in this server's settings was not accepted. An administrator can confirm it under Admin > Diagnostics.");
        if (m.Contains("Invalid registration number", StringComparison.OrdinalIgnoreCase))
            return ("The RC number cannot be checked",
                "Correct the registration number on the case, then run the check again.");
        if (m.Contains("not found in credit bureau", StringComparison.OrdinalIgnoreCase) || m.Contains("no record", StringComparison.OrdinalIgnoreCase))
            return ("No bureau holds a file",
                "The bureaus answered that they have no credit record for this subject.");
        if (m.Contains("score unavailable", StringComparison.OrdinalIgnoreCase) || m.Contains("no credit score", StringComparison.OrdinalIgnoreCase))
            return ("The bureau score could not be fetched",
                "The report itself was received. The score will be fetched again the next time the checks are run.");
        if (m.Contains("circuit is now open", StringComparison.OrdinalIgnoreCase) || m.Contains("timed out", StringComparison.OrdinalIgnoreCase)
            || m.Contains("Connection error", StringComparison.OrdinalIgnoreCase) || m.Contains("could not be reached", StringComparison.OrdinalIgnoreCase))
            return ("SmartComply could not be reached",
                "The call did not get an answer in time. Run the check again shortly.");
        if (m.Contains("could not be completed", StringComparison.OrdinalIgnoreCase))
            return ("The bureau could not complete the check",
                "SmartComply gave no further detail. Send the message below to SmartComply if it keeps happening.");
        return ("The bureau check did not complete", "The bureau's full answer is below.");
    }
}
