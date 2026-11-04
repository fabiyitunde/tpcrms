using System.Text.Json;
using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// One amendment of a case's EOPs from the portal (CRMSQA#3): a new submission naming the case in
/// <c>amendsReference</c>. The case takes the amended breakdown and, if it was in review, starts a new
/// review cycle. This record keeps what the amendment replaced, so the earlier review and any offer
/// or decision made in it can always be read against the EOP versions they were made on.
/// Append-only: written once, never edited.
/// </summary>
public class RhshfEopAmendment : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }

    /// <summary>The portal's submissionId for the amendment. A repeat with the same id is a retry.</summary>
    public Guid SubmissionId { get; private set; }

    /// <summary>The review cycle the amended EOPs are reviewed in: the new cycle it started, or the
    /// FAC's next one when the case was with the FAC.</summary>
    public int CycleNumber { get; private set; }

    /// <summary>The cycle under way when the amendment arrived; 0 if the case had never been submitted.</summary>
    public int ReplacedCycleNumber { get; private set; }

    public DateTime AmendedAt { get; private set; }
    public decimal PreviousTotalEopValue { get; private set; }
    public decimal NewTotalEopValue { get; private set; }

    /// <summary>Which EOPs changed, as JSON, read through <see cref="Changes"/>.</summary>
    public string ChangesJson { get; private set; } = "[]";

    /// <summary>The breakdown the amendment replaced, with its hashes and revisions, as JSON. The
    /// basis of anything decided before the amendment.</summary>
    public string PreviousBreakdownJson { get; private set; } = "{}";

    /// <summary>The amendment exactly as the portal sent it.</summary>
    public string RawPayload { get; private set; } = string.Empty;

    private RhshfEopAmendment() { }

    internal RhshfEopAmendment(
        Guid profileId, Guid submissionId, int cycleNumber, int replacedCycleNumber,
        decimal previousTotal, decimal newTotal, IReadOnlyList<RhshfEopRevisionChange> changes,
        string previousBreakdownJson, string rawPayload)
    {
        RhshfCreditProfileId = profileId;
        SubmissionId = submissionId;
        CycleNumber = cycleNumber;
        ReplacedCycleNumber = replacedCycleNumber;
        AmendedAt = DateTime.UtcNow;
        PreviousTotalEopValue = previousTotal;
        NewTotalEopValue = newTotal;
        ChangesJson = JsonSerializer.Serialize(changes);
        PreviousBreakdownJson = previousBreakdownJson;
        RawPayload = rawPayload;
    }

    public IReadOnlyList<RhshfEopRevisionChange> Changes =>
        JsonSerializer.Deserialize<List<RhshfEopRevisionChange>>(ChangesJson) ?? [];
}

/// <summary>An EOP an amendment changed or added, with its revision before and after.</summary>
public record RhshfEopRevisionChange(Guid EopId, string? EopReference, int? FromRevision, int? ToRevision, bool Added)
{
    public string Describe() =>
        $"{EopReference ?? EopId.ToString()} "
        + (Added ? $"added at revision {ToRevision?.ToString() ?? "(none)"}"
            : $"revision {FromRevision?.ToString() ?? "(none)"} to {ToRevision?.ToString() ?? "(none)"}");
}
