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

    public static Task<BureauPull<SmartComplyBusinessCreditReport>> GetBusinessReportAsync(
        ISmartComplyProvider smartComply, string rcNumber, CancellationToken ct) =>
        PullAsync(
        [
            (CreditBureauProvider.CRC, () => smartComply.GetCRCBusinessHistoryAsync(rcNumber, ct)),
            (CreditBureauProvider.FirstCentral, () => smartComply.GetFirstCentralBusinessAsync(rcNumber, ct)),
        ]);

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
