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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ActionRequired = null);

public record RhshfWebhookDecisionPayload(
    string Outcome,
    decimal? ApprovedAmount,
    string Currency,
    string[] Reasons,
    DateTime DecidedAt,
    string DecidedBy);

/// <summary>Builds the exact envelope for either event type off the profile's CURRENT state —
/// safe because both events only ever fire once the state they describe is settled (Decided is
/// terminal; OfferReady's Ratification outcome doesn't change while a retry is pending).</summary>
public static class RhshfWebhookPayloadBuilder
{
    public static RhshfWebhookPayload Build(RhshfCreditProfile profile, RhshfCallbackEventType eventType, string eventId, DateTime eventOccurredAt)
    {
        if (eventType == RhshfCallbackEventType.OfferReady)
        {
            return new RhshfWebhookPayload(
                Reference: profile.Reference,
                SubmissionId: profile.SubmissionId,
                Status: profile.Status.ToExternalStatus(),
                Decision: null,
                EventId: eventId,
                OccurredAt: eventOccurredAt,
                ActionRequired: "REVIEW_OFFER");
        }

        return new RhshfWebhookPayload(
            Reference: profile.Reference,
            SubmissionId: profile.SubmissionId,
            Status: profile.Status.ToExternalStatus(),
            Decision: BuildDecision(profile, eventOccurredAt),
            EventId: eventId,
            OccurredAt: eventOccurredAt);
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
