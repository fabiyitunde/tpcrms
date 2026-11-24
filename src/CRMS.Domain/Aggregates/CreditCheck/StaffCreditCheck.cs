using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.CreditCheck;

/// <summary>
/// One credit check a member of staff ran from the intranet, on an individual (by BVN) or a
/// company (by registration number). Kept apart from the partner API's jobs on purpose: its own
/// table, its own worker in the intranet host, its own billing flow, so nothing here can change
/// what partners see, are billed or wait for.
/// <para>
/// The table is the queue, as for partner jobs. The validated request (it holds the BVN or the
/// account numbers) and the result are stored only as ciphertext; the row carries a keyed hash and
/// a masked label of the subject for the history list.
/// </para>
/// </summary>
public class StaffCreditCheck : AggregateRoot
{
    public const string Queued = "QUEUED";
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";

    public const string Individual = "INDIVIDUAL";
    public const string Company = "COMPANY";

    public string SubjectType { get; private set; } = Individual;
    /// <summary>Keyed hash of the BVN or the registration number, for finding earlier checks.</summary>
    public string SubjectHash { get; private set; } = string.Empty;
    /// <summary>What the history shows: "BVN ****4321", or the RC number and the name given.</summary>
    public string SubjectLabel { get; private set; } = string.Empty;
    /// <summary>The name the check found (BVN record or registry), once it has run.</summary>
    public string? SubjectName { get; private set; }

    public Guid RequestedByUserId { get; private set; }
    public string RequestedByName { get; private set; } = string.Empty;
    public string Purpose { get; private set; } = string.Empty;
    public string? CaseReference { get; private set; }

    public string Status { get; private set; } = Queued;
    /// <summary>Comma-separated canonical section names.</summary>
    public string SectionsRequested { get; private set; } = string.Empty;
    public string RequestCiphertext { get; private set; } = string.Empty;
    public string? ResultCiphertext { get; private set; }
    /// <summary>Comma-separated "section=STATUS" pairs, one per section.</summary>
    public string? SectionOutcomes { get; private set; }

    public int RunAttempts { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int DurationMs { get; private set; }
    public string? BureauUsed { get; private set; }
    public int BilledCalls { get; private set; }
    public string? ErrorMessage { get; private set; }

    public decimal? AdvisoryScore { get; private set; }
    public string? AdvisoryRating { get; private set; }
    public string? AdvisoryRecommendation { get; private set; }
    public int WarningCount { get; private set; }

    private StaffCreditCheck() { }

    public static Result<StaffCreditCheck> Create(
        string subjectType, string subjectHash, string subjectLabel, Guid requestedByUserId, string requestedByName,
        string purpose, string? caseReference, IEnumerable<string> sections, string requestCiphertext)
    {
        if (subjectType is not (Individual or Company))
            return Result.Failure<StaffCreditCheck>("Unknown subject type.");
        if (requestedByUserId == Guid.Empty)
            return Result.Failure<StaffCreditCheck>("The member of staff running the check is required.");
        var reason = purpose?.Trim() ?? string.Empty;
        if (reason.Length < 5)
            return Result.Failure<StaffCreditCheck>("Say briefly why the check is being run (at least 5 characters).");
        var list = sections.ToList();
        if (list.Count == 0)
            return Result.Failure<StaffCreditCheck>("Choose at least one section.");

        var check = new StaffCreditCheck
        {
            SubjectType = subjectType,
            SubjectHash = subjectHash,
            SubjectLabel = Cap(subjectLabel, 250)!,
            RequestedByUserId = requestedByUserId,
            RequestedByName = Cap(requestedByName, 200)!,
            Purpose = Cap(reason, 500)!,
            CaseReference = string.IsNullOrWhiteSpace(caseReference) ? null : Cap(caseReference.Trim(), 100),
            SectionsRequested = Cap(string.Join(",", list), 250)!,
            RequestCiphertext = requestCiphertext,
            Status = Queued,
        };
        check.SetAuditInfo(requestedByUserId.ToString(), isNew: true);
        return Result.Success(check);
    }

    public bool IsOpen => Status is Queued or Processing;

    public void Complete(
        string resultCiphertext, IEnumerable<KeyValuePair<string, string>> sectionOutcomes, string? subjectName,
        string? bureauUsed, int billedCalls, int durationMs, DateTime now,
        decimal? advisoryScore, string? advisoryRating, string? advisoryRecommendation, int warningCount)
    {
        Status = Completed;
        ResultCiphertext = resultCiphertext;
        SectionOutcomes = Cap(string.Join(",", sectionOutcomes.Select(kv => $"{kv.Key}={kv.Value}")), 500);
        SubjectName = Cap(subjectName, 200);
        BureauUsed = Cap(bureauUsed, 50);
        BilledCalls = billedCalls;
        DurationMs = durationMs;
        AdvisoryScore = advisoryScore;
        AdvisoryRating = Cap(advisoryRating, 50);
        AdvisoryRecommendation = Cap(advisoryRecommendation, 50);
        WarningCount = warningCount;
        ErrorMessage = null;
        CompletedAt = now;
    }

    public void Fail(string message, int durationMs, DateTime now)
    {
        Status = Failed;
        ErrorMessage = Cap(message, 1000);
        DurationMs = durationMs;
        CompletedAt = now;
    }

    public IReadOnlyList<string> SectionList =>
        SectionsRequested.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public IReadOnlyDictionary<string, string> OutcomeMap =>
        (SectionOutcomes ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .GroupBy(p => p[0])
            .ToDictionary(g => g.Key, g => g.First()[1]);

    private static string? Cap(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}
