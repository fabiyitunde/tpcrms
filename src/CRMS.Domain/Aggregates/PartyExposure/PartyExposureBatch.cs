using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.PartyExposure;

/// <summary>
/// A batch of party-exposure lookups (up to 500 subjects), worked through in the background so it
/// never competes with live CRMS work.
/// <para>
/// The subjects' identifiers are deliberately <b>not</b> stored here. They are held in memory only
/// while the batch is worked, and the results kept are summaries keyed by the portal's own subject
/// reference. The cost: a batch caught by a restart cannot be resumed and is marked failed, to be
/// sent again. That is preferred to keeping thousands of BVNs at rest for a report.
/// </para>
/// </summary>
public class PartyExposureBatch : AggregateRoot
{
    public string RequestId { get; private set; } = string.Empty;
    public string Purpose { get; private set; } = string.Empty;
    public string? RequestedByUserId { get; private set; }
    public string? RequestedByRole { get; private set; }
    public string? CallbackUrl { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public int SubjectCount { get; private set; }
    public int ProcessedCount { get; private set; }
    /// <summary>JSON array of per-subject summaries. Holds no identifiers.</summary>
    public string? ResultsJson { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? Failure { get; private set; }
    /// <summary>HTTP status of the PARTY_EXPOSURE_BATCH_READY post, when a callback was asked for.</summary>
    public int? CallbackStatusCode { get; private set; }
    public DateTime? CallbackSentAt { get; private set; }

    public const string Queued = "QUEUED";
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";

    private PartyExposureBatch() { }

    public static PartyExposureBatch Create(
        string requestId, string purpose, string? requestedByUserId, string? requestedByRole,
        string? callbackUrl, int subjectCount)
        => new()
        {
            RequestId = requestId,
            Purpose = purpose,
            RequestedByUserId = requestedByUserId,
            RequestedByRole = requestedByRole,
            CallbackUrl = callbackUrl,
            Status = Queued,
            SubjectCount = subjectCount,
        };

    public void Start() => Status = Processing;

    public void Progress(int processed) => ProcessedCount = processed;

    public void Complete(string resultsJson)
    {
        Status = Completed;
        ProcessedCount = SubjectCount;
        ResultsJson = resultsJson;
        CompletedAt = DateTime.UtcNow;
    }

    public void Fail(string reason)
    {
        Status = Failed;
        Failure = reason.Length > 1000 ? reason[..1000] : reason;
        CompletedAt = DateTime.UtcNow;
    }

    public void RecordCallback(int? statusCode)
    {
        CallbackStatusCode = statusCode;
        CallbackSentAt = DateTime.UtcNow;
    }
}
