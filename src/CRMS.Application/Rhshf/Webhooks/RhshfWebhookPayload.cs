using System.Text.Json.Serialization;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Application.Rhshf.Webhooks;

/// <summary>
/// Matches the brief's §4.4 example field-for-field. ActionRequired is omitted from the serialized
/// JSON entirely (not sent as null) on the terminal Decided payload — only the non-terminal
/// OfferReady payload sets it. Lives in Application (not Infrastructure, despite being consumed by
/// Infrastructure's RhshfCallbackService) because it has no Infrastructure dependency of its own,
/// and Phase 11's status endpoint reuses RhshfWebhookDecisionPayload directly for its "decision"
/// field — "same shape as §4.4's payload once populated" per the brief, at the type level, not just
/// structurally.
/// </summary>
public record RhshfWebhookPayload(
    string Reference,
    Guid SubmissionId,
    string Status,
    RhshfWebhookDecisionPayload? Decision,
    string EventId,
    DateTime OccurredAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ActionRequired = null,
    /// <summary>Where the FAC must go to satisfy ActionRequired — the portal appends its own freshly
    /// minted token. Omitted from the JSON when null, same as ActionRequired.</summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ActionUrl = null,
    /// <summary>Why this was sent. Additive — the fields below were added for the portal's credit
    /// status display and are ignored by a consumer that does not know them.</summary>
    string? EventType = null,
    string? CreditStatus = null,
    int StateVersion = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RhshfPortalOfferPayload? Offer = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RhshfPortalCorrectionPayload? Correction = null);

public record RhshfWebhookDecisionPayload(
    string Outcome,
    decimal? ApprovedAmount,
    string Currency,
    string[] Reasons,
    DateTime DecidedAt,
    string DecidedBy);

/// <summary>Builds the envelope off the profile's CURRENT state, not the state when the event fired.
/// A retry that lands after the case has moved on therefore reports where the case is now — with a
/// higher stateVersion — rather than replaying a stale instruction such as "review the offer" for an
/// offer already accepted. The event type only records what prompted the send.</summary>
public static class RhshfWebhookPayloadBuilder
{
    /// <param name="currentOffer">The offer for the profile's current cycle, if any.</param>
    /// <param name="offerUrl">The FAC-facing offer page, resolved by the caller (which has the
    /// configured public host); null omits it.</param>
    /// <param name="profilingUrl">The FAC-facing profiling form, for a correction; null omits it.</param>
    public static RhshfWebhookPayload Build(
        RhshfCreditProfile profile, RhshfCallbackEventType eventType, string eventId, DateTime eventOccurredAt,
        RhshfOffer? currentOffer = null, string? offerUrl = null, string? profilingUrl = null)
    {
        var state = RhshfPortalStateBuilder.Build(profile, currentOffer, offerUrl, profilingUrl);

        return new RhshfWebhookPayload(
            Reference: profile.Reference,
            SubmissionId: profile.SubmissionId,
            Status: profile.Status.ToExternalStatus(),
            Decision: BuildDecision(profile, eventOccurredAt),
            EventId: eventId,
            OccurredAt: eventOccurredAt,
            ActionRequired: state.ActionRequired,
            ActionUrl: state.ActionUrl,
            EventType: eventType.ToExternalEventType(),
            CreditStatus: state.CreditStatus,
            StateVersion: state.StateVersion,
            Offer: state.Offer,
            Correction: state.Correction);
    }

    /// <summary>"reasons" is populated on DECLINE / INFO_REQUIRED only (brief §4.4) — DecisionNotes
    /// on an Approved outcome is an internal audit note (e.g. "Booked as Fineract loan LN-000123."),
    /// not a decline reason, so it must not leak into this array. Shared by both the webhook and
    /// Phase 11's status endpoint.</summary>
    public static RhshfWebhookDecisionPayload? BuildDecision(RhshfCreditProfile profile, DateTime fallbackDecidedAt)
    {
        if (profile.DecisionOutcome is null)
            return null;

        return new RhshfWebhookDecisionPayload(
            Outcome: profile.DecisionOutcome.Value.ToExternalOutcome(),
            ApprovedAmount: profile.ApprovedAmount,
            Currency: profile.Currency,
            Reasons: profile.DecisionOutcome != RhshfDecisionOutcome.Approved && !string.IsNullOrWhiteSpace(profile.DecisionNotes)
                ? [profile.DecisionNotes]
                : [],
            DecidedAt: profile.DecidedAt ?? fallbackDecidedAt,
            DecidedBy: profile.DecidedBy ?? "CRMS");
    }
}
