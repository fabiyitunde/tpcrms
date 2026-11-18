using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.CreditBureau;

/// <summary>A bureau pull and the bureau that answered it. On failure, <see cref="Bureau"/> is the
/// last one tried.</summary>
public sealed record BureauPull<T>(Result<T> Result, CreditBureauProvider Bureau);

/// <summary>
/// Every loan track's bureau pull goes CRC first, then First Central — whenever CRC has no record
/// or the CRC call fails. A pull only reads "not found" when <i>both</i> bureaus said so, so the
/// handlers' existing "not found" test keeps meaning "no bureau holds a file for this subject".
/// </summary>
public static class BureauFallback
{
    private const string NotFound = "Subject not found in credit bureau";

    public static async Task<BureauPull<SmartComplyIndividualCreditReport>> GetIndividualReportAsync(
        ISmartComplyProvider smartComply, string bvn, CancellationToken ct)
    {
        var crc = await smartComply.GetCRCFullAsync(bvn, ct);
        if (crc.IsSuccess) return new(crc, CreditBureauProvider.CRC);

        var firstCentral = await smartComply.GetFirstCentralFullAsync(bvn, ct);
        return firstCentral.IsSuccess
            ? new(firstCentral, CreditBureauProvider.FirstCentral)
            : new(Combine<SmartComplyIndividualCreditReport>(crc.Error, firstCentral.Error), CreditBureauProvider.FirstCentral);
    }

    /// <summary>The score comes from the same bureau as the report, so the two never describe
    /// different files.</summary>
    public static Task<Result<SmartComplyCreditScore>> GetScoreAsync(
        ISmartComplyProvider smartComply, CreditBureauProvider bureau, string bvn, CancellationToken ct) =>
        bureau == CreditBureauProvider.FirstCentral
            ? smartComply.GetFirstCentralScoreAsync(bvn, ct)
            : smartComply.GetCRCScoreAsync(bvn, ct);

    public static async Task<BureauPull<SmartComplyBusinessCreditReport>> GetBusinessReportAsync(
        ISmartComplyProvider smartComply, string rcNumber, CancellationToken ct)
    {
        var crc = await smartComply.GetCRCBusinessHistoryAsync(rcNumber, ct);
        if (crc.IsSuccess) return new(crc, CreditBureauProvider.CRC);

        var firstCentral = await smartComply.GetFirstCentralBusinessAsync(rcNumber, ct);
        return firstCentral.IsSuccess
            ? new(firstCentral, CreditBureauProvider.FirstCentral)
            : new(Combine<SmartComplyBusinessCreditReport>(crc.Error, firstCentral.Error), CreditBureauProvider.FirstCentral);
    }

    public static string Label(CreditBureauProvider bureau) => bureau switch
    {
        CreditBureauProvider.FirstCentral => "First Central",
        _ => bureau.ToString()
    };

    private static Result<T> Combine<T>(string? crcError, string? firstCentralError)
    {
        var crcNone = IsNotFound(crcError);
        var fcNone = IsNotFound(firstCentralError);
        if (crcNone && fcNone)
            return Result.Failure<T>($"{NotFound} (CRC and First Central)");

        // At least one bureau failed outright, so "no record" is not proven. Keep the phrase
        // "not found" out of the message, or callers would file it as a clean no-record outcome.
        return Result.Failure<T>(
            $"CRC: {Describe(crcError, crcNone)}; First Central: {Describe(firstCentralError, fcNone)}");
    }

    private static bool IsNotFound(string? error) =>
        error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true;

    private static string Describe(string? error, bool none) =>
        none ? "no record" : error ?? "unknown error";
}
