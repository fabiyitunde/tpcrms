using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.CustomerCheck;

/// <summary>
/// A bureau report fetched by a customer check, kept so a consumer with "reuse bureau reports"
/// switched on is not billed again for the same person within its reuse window. Keyed by the
/// keyed hash of the BVN; the report itself is stored as the provider's structured result.
/// </summary>
public class CustomerBureauReport : AggregateRoot
{
    public string BvnHash { get; private set; } = string.Empty;
    /// <summary>FirstCentral, CRC or CreditRegistry.</summary>
    public string Bureau { get; private set; } = string.Empty;
    public string ReportJson { get; private set; } = string.Empty;
    public string? ScoreJson { get; private set; }
    public DateTime FetchedAt { get; private set; }
    public Guid FetchedByConsumerId { get; private set; }

    private CustomerBureauReport() { }

    public static CustomerBureauReport Store(string bvnHash, string bureau, string reportJson, string? scoreJson, Guid consumerId)
        => new()
        {
            BvnHash = bvnHash,
            Bureau = bureau,
            ReportJson = reportJson,
            ScoreJson = scoreJson,
            FetchedAt = DateTime.UtcNow,
            FetchedByConsumerId = consumerId,
        };

    /// <summary>A later check with the score option fetches the score the stored report lacks.</summary>
    public void AttachScore(string scoreJson) => ScoreJson = scoreJson;
}
