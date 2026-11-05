using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.CustomerCheck;

/// <summary>
/// One customer check asked of the CRMS by a consumer, from acceptance to delivery
/// (docs/Customer_Check.md). The table is the queue: a worker claims QUEUED rows, runs the check,
/// stores the result and posts it to the consumer's callback, retrying on a schedule. The row
/// survives a restart, so a check is never lost; one caught mid-run is claimed again.
/// <para>
/// The validated request (it holds the BVN) and the result (names, balances) are stored only as
/// ciphertext. The job carries the keyed hash of the BVN and its last four digits for history
/// searches and display. Results are kept indefinitely: consumers pay per check and may read a
/// past one again.
/// </para>
/// </summary>
public class CustomerCheckJob : AggregateRoot
{
    public const string Queued = "QUEUED";
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";

    public const string CallbackNotRequested = "NOT_REQUESTED";
    public const string CallbackPending = "PENDING";
    public const string CallbackDelivered = "DELIVERED";
    public const string CallbackFailed = "FAILED";

    public Guid ConsumerId { get; private set; }
    public string ConsumerName { get; private set; } = string.Empty;
    public string RequestId { get; private set; } = string.Empty;
    public string Status { get; private set; } = Queued;
    public string BvnHash { get; private set; } = string.Empty;
    public string BvnLast4 { get; private set; } = string.Empty;
    /// <summary>Comma-separated canonical section names.</summary>
    public string SectionsRequested { get; private set; } = string.Empty;
    /// <summary>The validated query, encrypted. Decrypted only by the worker that runs it.</summary>
    public string RequestCiphertext { get; private set; } = string.Empty;
    public string? CallbackUrl { get; private set; }
    public string CallbackStatus { get; private set; } = CallbackNotRequested;
    /// <summary>Stable across every delivery attempt, so the receiver can de-duplicate.</summary>
    public string EventId { get; private set; } = string.Empty;

    public int RunAttempts { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int DurationMs { get; private set; }

    /// <summary>The reply, encrypted. Present once COMPLETED.</summary>
    public string? ResultCiphertext { get; private set; }
    /// <summary>Comma-separated "section=STATUS" pairs, one per section.</summary>
    public string? SectionOutcomes { get; private set; }
    public string? BureauUsed { get; private set; }
    public bool BureauReused { get; private set; }
    public int BilledCalls { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    public int CallbackAttempts { get; private set; }
    public DateTime? NextCallbackAt { get; private set; }
    public DateTime? CallbackDeliveredAt { get; private set; }
    public int? LastCallbackStatusCode { get; private set; }
    public string? LastCallbackError { get; private set; }

    /// <summary>The advisory's headline, copied out of the result for the dashboard.</summary>
    public decimal? AdvisoryScore { get; private set; }
    public string? AdvisoryRating { get; private set; }
    public string? AdvisoryRecommendation { get; private set; }

    private CustomerCheckJob() { }

    public static CustomerCheckJob Create(
        Guid consumerId, string consumerName, string requestId, string bvnHash, string bvnLast4,
        IEnumerable<string> sections, string requestCiphertext, string? callbackUrl)
        => new()
        {
            ConsumerId = consumerId,
            ConsumerName = Cap(consumerName, 200)!,
            RequestId = Cap(requestId, 100)!,
            BvnHash = bvnHash,
            BvnLast4 = bvnLast4,
            SectionsRequested = Cap(string.Join(",", sections), 200)!,
            RequestCiphertext = requestCiphertext,
            CallbackUrl = string.IsNullOrWhiteSpace(callbackUrl) ? null : callbackUrl.Trim(),
            EventId = $"evt_{Guid.NewGuid():N}",
            Status = Queued,
            CallbackStatus = CallbackNotRequested,
        };

    public bool IsOpen => Status is Queued or Processing;

    /// <summary>The worker has taken the job. Counted, so a job that keeps getting interrupted is
    /// eventually failed rather than run forever.</summary>
    public void Start(DateTime now)
    {
        Status = Processing;
        StartedAt = now;
        RunAttempts++;
    }

    public void Complete(
        string resultCiphertext, IEnumerable<KeyValuePair<string, string>> sectionOutcomes, string? bureauUsed,
        bool bureauReused, int billedCalls, int durationMs, DateTime now,
        decimal? advisoryScore = null, string? advisoryRating = null, string? advisoryRecommendation = null)
    {
        Status = Completed;
        ResultCiphertext = resultCiphertext;
        SectionOutcomes = Cap(string.Join(",", sectionOutcomes.Select(kv => $"{kv.Key}={kv.Value}")), 500);
        BureauUsed = Cap(bureauUsed, 50);
        BureauReused = bureauReused;
        BilledCalls = billedCalls;
        DurationMs = durationMs;
        AdvisoryScore = advisoryScore;
        AdvisoryRating = Cap(advisoryRating, 50);
        AdvisoryRecommendation = Cap(advisoryRecommendation, 50);
        ErrorCode = null;
        ErrorMessage = null;
        CompletedAt = now;
        ScheduleCallback(now);
    }

    public void Fail(string code, string message, int durationMs, DateTime now)
    {
        Status = Failed;
        ErrorCode = Cap(code, 50);
        ErrorMessage = Cap(message, 1000);
        DurationMs = durationMs;
        CompletedAt = now;
        ScheduleCallback(now);
    }

    private void ScheduleCallback(DateTime now)
    {
        if (CallbackUrl is null)
        {
            CallbackStatus = CallbackNotRequested;
            NextCallbackAt = null;
            return;
        }
        CallbackStatus = CallbackPending;
        NextCallbackAt = now;
    }

    /// <summary>Records one delivery attempt. A failed attempt with no next time is the end of the
    /// road: the callback is marked FAILED and the result waits to be fetched.</summary>
    public void RecordCallbackAttempt(bool succeeded, int? statusCode, string? error, DateTime? nextAttemptAt, DateTime now)
    {
        CallbackAttempts++;
        LastCallbackStatusCode = statusCode;
        LastCallbackError = Cap(error, 500);
        if (succeeded)
        {
            CallbackStatus = CallbackDelivered;
            CallbackDeliveredAt = now;
            NextCallbackAt = null;
        }
        else if (nextAttemptAt is null)
        {
            CallbackStatus = CallbackFailed;
            NextCallbackAt = null;
        }
        else
        {
            CallbackStatus = CallbackPending;
            NextCallbackAt = nextAttemptAt;
        }
    }

    /// <summary>A staff-requested re-send after the automatic attempts gave up, or when the partner
    /// says it never arrived. Same event id, so a receiver that did process it ignores the repeat.</summary>
    public Result ResendCallback(DateTime now)
    {
        if (CallbackUrl is null)
            return Result.Failure("This check has no callback URL.");
        if (IsOpen)
            return Result.Failure("The check has not finished yet.");
        CallbackStatus = CallbackPending;
        NextCallbackAt = now;
        return Result.Success();
    }

    public IReadOnlyList<string> SectionList =>
        SectionsRequested.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? Cap(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}

/// <summary>Billed third-party calls made during one check, by kind, for per-call pricing.</summary>
public record BilledCallCounts(int Identity, int Bureau, int Score, int Fraud, int Advisory)
{
    public static readonly BilledCallCounts None = new(0, 0, 0, 0, 0);
    public int Total => Identity + Bureau + Score + Fraud + Advisory;
}
