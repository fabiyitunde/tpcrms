using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.CreditBureau;

/// <summary>A bureau pull and the bureau that answered it. On failure, <see cref="Bureau"/> is the
/// last one tried.</summary>
public sealed record BureauPull<T>(Result<T> Result, CreditBureauProvider Bureau);

/// <summary>
/// Every loan track's bureau pull works down a chain of bureaus, moving on whenever one has no
/// record or its call fails: individuals go CRC, First Central, then Credit Registry; companies go
/// CRC, then First Central (SmartComply has no Credit Registry company report). A pull only reads
/// "not found" when <i>every</i> bureau tried said so, so the handlers' existing "not found" test
/// keeps meaning "no bureau holds a file for this subject".
/// </summary>
public static class BureauFallback
{
    private const string NotFound = "Subject not found in credit bureau";

    public static Task<BureauPull<SmartComplyIndividualCreditReport>> GetIndividualReportAsync(
        ISmartComplyProvider smartComply, string bvn, CancellationToken ct) =>
        PullAsync(
        [
            (CreditBureauProvider.CRC, () => smartComply.GetCRCFullAsync(bvn, ct)),
            (CreditBureauProvider.FirstCentral, () => smartComply.GetFirstCentralFullAsync(bvn, ct)),
            (CreditBureauProvider.CreditRegistry, () => smartComply.GetCreditRegistryFullAsync(bvn, ct)),
        ]);

    /// <summary>The score comes from the same bureau as the report, so the two never describe
    /// different files. Credit Registry has no score report on SmartComply, so its pulls get none
    /// and the handlers fall back to their derived score.</summary>
    public static Task<Result<SmartComplyCreditScore>> GetScoreAsync(
        ISmartComplyProvider smartComply, CreditBureauProvider bureau, string bvn, CancellationToken ct) =>
        bureau switch
        {
            CreditBureauProvider.FirstCentral => smartComply.GetFirstCentralScoreAsync(bvn, ct),
            CreditBureauProvider.CreditRegistry => Task.FromResult(
                Result.Failure<SmartComplyCreditScore>("Credit Registry has no score report")),
            _ => smartComply.GetCRCScoreAsync(bvn, ct)
        };

    public static async Task<BureauPull<SmartComplyBusinessCreditReport>> GetBusinessReportAsync(
        ISmartComplyProvider smartComply, string rcNumber, CancellationToken ct)
    {
        // A number no bureau will accept is refused here, before any billed call, with the reason
        // in plain words instead of the bureau's generic "could not be completed".
        if (InvalidRegistrationNumber(rcNumber) is { } invalid)
            return new(Result.Failure<SmartComplyBusinessCreditReport>(invalid), CreditBureauProvider.CRC);

        var pull = await PullAsync(
        [
            (CreditBureauProvider.CRC, () => smartComply.GetCRCBusinessHistoryAsync(rcNumber, ct)),
            (CreditBureauProvider.FirstCentral, () => smartComply.GetFirstCentralBusinessAsync(rcNumber, ct)),
        ]);
        return pull with { Result = CrcCouldNotCompleteButNoRecord(pull.Result) };
    }

    /// <summary>
    /// Why a registration number cannot be sent to the bureaus, or null when it can. CRC takes the
    /// prefix (RC, BN or IT) and 1 to 8 digits; a number without a prefix is sent as RC. Anything
    /// else is answered by CRC with a vague 400, so the case is told the real reason instead.
    /// </summary>
    public static string? InvalidRegistrationNumber(string? rcNumber)
    {
        var text = (rcNumber ?? string.Empty).Trim();
        if (text.Length == 0)
            return "Invalid registration number: the case has no RC number.";
        var rest = text;
        foreach (var prefix in new[] { "RC", "BN", "IT" })
            if (rest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                rest = rest[prefix.Length..];
                break;
            }
        var digits = new string(rest.Where(c => !char.IsWhiteSpace(c) && c is not ('-' or '/' or '.' or ':')).ToArray());
        if (digits.Length == 0 || !digits.All(char.IsAsciiDigit))
            return $"Invalid registration number \"{text}\": it should be RC, BN or IT followed by digits only. Correct it on the case and run the check again.";
        if (digits.Length > 8)
            return $"Invalid registration number \"{text}\": it has {digits.Length} digits, but CAC registration numbers have 1 to 8. Correct it on the case and run the check again.";
        return null;
    }

    private const string CrcCouldNotComplete = "Credit check could not be completed";

    /// <summary>
    /// CRC answers some companies with HTTP 400 "Credit check could not be completed" where it would
    /// otherwise say it holds no file. When First Central then says it has no record either, that is
    /// read as "not found" rather than a failed check. Both bureaus' own words are kept in the message,
    /// so the case shows exactly what each answered.
    /// </summary>
    private static Result<SmartComplyBusinessCreditReport> CrcCouldNotCompleteButNoRecord(Result<SmartComplyBusinessCreditReport> result)
    {
        if (result.IsSuccess || result.Error is not { } error || IsNotFound(error))
            return result;
        var parts = error.Split("; ", StringSplitOptions.RemoveEmptyEntries);
        var crcCouldNotComplete = parts.Any(p => p.StartsWith("CRC:", StringComparison.Ordinal)
            && p.Contains(CrcCouldNotComplete, StringComparison.OrdinalIgnoreCase));
        var othersNoRecord = parts.Where(p => !p.StartsWith("CRC:", StringComparison.Ordinal))
            .All(p => p.EndsWith(": no record", StringComparison.Ordinal));
        return crcCouldNotComplete && othersNoRecord && parts.Length > 1
            ? Result.Failure<SmartComplyBusinessCreditReport>($"{NotFound}. {error}")
            : result;
    }

    public static string Label(CreditBureauProvider bureau) => bureau switch
    {
        CreditBureauProvider.FirstCentral => "First Central",
        CreditBureauProvider.CreditRegistry => "Credit Registry",
        _ => bureau.ToString()
    };

    private static async Task<BureauPull<T>> PullAsync<T>(
        IReadOnlyList<(CreditBureauProvider Bureau, Func<Task<Result<T>>> Pull)> chain)
    {
        var errors = new List<(CreditBureauProvider Bureau, string? Error)>();
        foreach (var (bureau, pull) in chain)
        {
            var result = await pull();
            if (result.IsSuccess) return new(result, bureau);
            errors.Add((bureau, result.Error));
        }
        return new(Combine<T>(errors), chain[^1].Bureau);
    }

    private static Result<T> Combine<T>(IReadOnlyList<(CreditBureauProvider Bureau, string? Error)> errors)
    {
        var names = string.Join(", ", errors.Select(e => Label(e.Bureau)));
        if (errors.All(e => IsNotFound(e.Error)))
            return Result.Failure<T>($"{NotFound} ({names})");

        // At least one bureau failed outright, so "no record" is not proven. Keep the phrase
        // "not found" out of the message, or callers would file it as a clean no-record outcome.
        return Result.Failure<T>(string.Join("; ",
            errors.Select(e => $"{Label(e.Bureau)}: {(IsNotFound(e.Error) ? "no record" : e.Error ?? "unknown error")}")));
    }

    private static bool IsNotFound(string? error) =>
        error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true;
}
