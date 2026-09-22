using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// A record that the FAC confirmed one profiling stage.
///
/// Previously each of the five stages was an untracked button press: the form advanced
/// <c>CurrentStage</c> and nothing else was written down. There was no way to answer "who attested
/// that the company details were correct, and when" — which matters, because stages 1 and 3 are
/// explicitly attestations ("these details are correct", "this EOP is correct") that the bank later
/// relies on.
///
/// The actor is the FAC organisation, not a CRMS user: profiling is token-authenticated against the
/// case, so there is no individual identity to record. FacId is what we can honestly attribute, and
/// the IP/user-agent give a thin audit trail beyond that.
/// </summary>
public class RhshfStageConfirmation : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }
    public int CycleNumber { get; private set; }
    public RhshfProfilingStage Stage { get; private set; }

    /// <summary>The FAC the profiling token was issued to.</summary>
    public Guid ConfirmedByFacId { get; private set; }
    public DateTime ConfirmedAt { get; private set; }

    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    protected RhshfStageConfirmation() { }

    public RhshfStageConfirmation(
        Guid rhshfCreditProfileId, int cycleNumber, RhshfProfilingStage stage,
        Guid confirmedByFacId, string? ipAddress, string? userAgent)
    {
        RhshfCreditProfileId = rhshfCreditProfileId;
        CycleNumber = cycleNumber;
        Stage = stage;
        ConfirmedByFacId = confirmedByFacId;
        ConfirmedAt = DateTime.UtcNow;
        // Truncated defensively — these come straight off an inbound request header.
        IpAddress = Truncate(ipAddress, 45);
        UserAgent = Truncate(userAgent, 400);
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null
        : value.Length <= max ? value
        : value[..max];
}
