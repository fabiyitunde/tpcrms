using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.CustomerCheck;

/// <summary>
/// One customer credit check asked of the CRMS, kept for audit and for billing visibility per consumer.
/// Identifiers are stored only as keyed hashes: the log shows when the same person was looked up
/// again, but cannot be read back into a BVN or account number.
/// </summary>
public class CustomerCheckLog : AggregateRoot
{
    public string RequestId { get; private set; } = string.Empty;
    /// <summary>The job this check ran under. Null only for checks recorded before jobs existed.</summary>
    public Guid? JobId { get; private set; }
    public Guid ConsumerId { get; private set; }
    public string ConsumerName { get; private set; } = string.Empty;
    public string BvnHash { get; private set; } = string.Empty;
    /// <summary>Comma-separated keyed hashes, one per account number given.</summary>
    public string? NubanHashes { get; private set; }
    public string SectionsRequested { get; private set; } = string.Empty;
    /// <summary>Comma-separated "section=STATUS" pairs, one per section.</summary>
    public string SectionOutcomes { get; private set; } = string.Empty;
    /// <summary>Which bureau answered, or null when none was called or none answered.</summary>
    public string? BureauUsed { get; private set; }
    public bool BureauReused { get; private set; }
    /// <summary>Billed third-party calls made during this check, in total and by kind.</summary>
    public int BilledCalls { get; private set; }
    public int IdentityCalls { get; private set; }
    public int BureauCalls { get; private set; }
    public int ScoreCalls { get; private set; }
    public int FraudCalls { get; private set; }
    public int AdvisoryCalls { get; private set; }
    public int DurationMs { get; private set; }
    /// <summary>Why a check produced no answer. Never contains an identifier.</summary>
    public string? Error { get; private set; }

    private CustomerCheckLog() { }

    public static CustomerCheckLog Record(
        string requestId, Guid consumerId, string consumerName, string bvnHash, IReadOnlyList<string> nubanHashes,
        IEnumerable<string> sectionsRequested, IEnumerable<KeyValuePair<string, string>> sectionOutcomes,
        string? bureauUsed, bool bureauReused, BilledCallCounts billed, int durationMs, string? error, Guid? jobId = null)
        => new()
        {
            RequestId = Cap(requestId, 100)!,
            JobId = jobId,
            ConsumerId = consumerId,
            ConsumerName = Cap(consumerName, 200)!,
            BvnHash = bvnHash,
            NubanHashes = nubanHashes.Count == 0 ? null : Cap(string.Join(",", nubanHashes), 1000),
            SectionsRequested = Cap(string.Join(",", sectionsRequested), 200)!,
            SectionOutcomes = Cap(string.Join(",", sectionOutcomes.Select(kv => $"{kv.Key}={kv.Value}")), 500)!,
            BureauUsed = Cap(bureauUsed, 50),
            BureauReused = bureauReused,
            BilledCalls = billed.Total,
            IdentityCalls = billed.Identity,
            BureauCalls = billed.Bureau,
            ScoreCalls = billed.Score,
            FraudCalls = billed.Fraud,
            AdvisoryCalls = billed.Advisory,
            DurationMs = durationMs,
            Error = Cap(error, 1000),
        };

    private static string? Cap(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}
