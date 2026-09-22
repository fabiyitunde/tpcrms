using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// One row per state change on a case — the chronological record of how it moved.
///
/// RH-SHF had no audit trail at all: the aggregate carried only its *current* Status/InternalStage,
/// and the individual decision records (appraisals, ratifications, disbursements) each covered one
/// stage in isolation. Nothing answered "what happened to this case, in order" — which matters most
/// precisely where the per-stage records are thinnest: return-to-FAC round trips, the internal
/// re-route from Legal back to Ratification, and everything that happened in a prior cycle.
///
/// Three differences from NAMP's NampStatusHistory, all deliberate:
///
///  1. It records the <b>full</b> state (external status, internal stage, profiling stage), not just
///     one status string. RH-SHF's position is genuinely two-dimensional — "UnderReview" alone does
///     not say whether a case sits with the Risk Officer or with Legal.
///  2. It records the <b>cycle</b>. A resubmitted case repeats stages, and a flat list with no cycle
///     marker reads as if the same stage happened twice for no reason.
///  3. The actor is <b>attributable or explicitly labelled</b>, never silently dropped. NAMP's own
///     Workflow tab renders no actor at all; a trail that cannot say who acted is not much of a
///     trail. Where no CRMS user acted — the FAC working through a token-authenticated form, or a
///     transition derived from a vote tally — ActorLabel says so instead of inventing a user.
/// </summary>
public class RhshfStatusHistory : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }

    /// <summary>The cycle in force when this happened. Profiling entries are filed under the cycle
    /// being assembled (ProfilingTargetCycleNumber), which is the cycle they will be reviewed as.</summary>
    public int CycleNumber { get; private set; }

    public RhshfCaseStatus Status { get; private set; }
    public RhshfInternalStage? InternalStage { get; private set; }
    public RhshfProfilingStage? ProfilingStage { get; private set; }

    /// <summary>What was done, in the language of the process — "Risk review cleared", not
    /// "InternalStage=CommitteeVoting". The state columns already carry the machine-readable half.</summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary>The CRMS user who acted, where one did. Null for FAC-side and derived transitions —
    /// resolved to a display name by the Application layer, never rendered raw.</summary>
    public Guid? ActorUserId { get; private set; }

    /// <summary>Who acted when it was not a CRMS user: "FAC", "Committee", "System". Set only when
    /// ActorUserId is null, so the two can never disagree about who is being credited.</summary>
    public string? ActorLabel { get; private set; }

    public string? Note { get; private set; }
    public DateTime ChangedAt { get; private set; }

    protected RhshfStatusHistory() { }

    internal RhshfStatusHistory(
        Guid rhshfCreditProfileId,
        int cycleNumber,
        RhshfCaseStatus status,
        RhshfInternalStage? internalStage,
        RhshfProfilingStage? profilingStage,
        string action,
        Guid? actorUserId,
        string? actorLabel,
        string? note)
    {
        RhshfCreditProfileId = rhshfCreditProfileId;
        CycleNumber = cycleNumber;
        Status = status;
        InternalStage = internalStage;
        ProfilingStage = profilingStage;
        Action = action;
        ActorUserId = actorUserId;
        // An actor is either a known user or a label, never both — otherwise the UI has to decide
        // which one to believe.
        ActorLabel = actorUserId is null ? (actorLabel ?? "System") : null;
        Note = Truncate(note, 2000);
        ChangedAt = DateTime.UtcNow;
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null
        : value.Length <= max ? value
        : value[..max];
}
