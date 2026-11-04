using System.Text.Json.Serialization;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Application.Rhshf.Webhooks;

/// <summary>The offer as the portal needs to see it. Version goes up with every offer issued on the
/// case — each ratification and each correction — so a corrected offer is distinguishable from the
/// one the FAC already accepted.</summary>
public record RhshfPortalOfferPayload(
    string Status,
    int Version,
    decimal? ApprovedAmount,
    string Currency,
    DateTime IssuedAt,
    DateTime? RespondedAt);

/// <summary>
/// One EOP a CRMS decision was made on (CRMSQA#3 contract points 4 and 5). The portal compares it with
/// its current EOP and does not carry the decision over to an amended one. The hash is v2 when the
/// portal sent one, because it also covers the provider assignment and rate, otherwise v1.
/// </summary>
public record RhshfPortalDecisionBasis(
    Guid EopId,
    string? EopReference,
    int? Revision,
    string? SnapshotHash,
    string? SnapshotHashScheme);

/// <summary>What the FAC was asked to correct. Message is the returning officer's note, which the
/// return dialogs warn staff the FAC will read.</summary>
public record RhshfPortalCorrectionPayload(
    string? Stage,
    string? Message,
    DateTime RequestedAt,
    /// <summary>The reason picked from the administrator's list (CRMSQA#1); null for returns made
    /// before codes existed.</summary>
    string? ReasonCode = null);

/// <summary>
/// Everything the portal needs to show the FAC where their case stands, derived in one place so the
/// webhook and the status endpoint can never disagree.
///
/// CreditStatus exists because Status is too coarse to drive a screen: UNDER_REVIEW covers
/// everything from appraisal to disbursement, which is how a case with an offer waiting showed
/// "Under review" on the portal. Status is kept as it was so the portal's existing handling keeps
/// working while it moves over.
/// </summary>
public record RhshfPortalState(
    string CreditStatus,
    int StateVersion,
    string? ActionRequired,
    string? ActionUrl,
    RhshfPortalOfferPayload? Offer,
    RhshfPortalCorrectionPayload? Correction,
    /// <summary>The EOPs the offer or decision was made on. Null until there is one.</summary>
    IReadOnlyList<RhshfPortalDecisionBasis>? DecisionBasis = null,
    /// <summary>By when the FAC must act on ActionRequired: the offer's expiry date while an offer
    /// awaits acceptance. Null when there is no deadline, including for a correction, which has none.
    /// The CRMS does not yet lapse an offer on this date.</summary>
    DateTime? ActionDeadline = null,
    /// <summary>Why the case is where it is (CRMSQA#1): the correction's reason code while it is back
    /// with the FAC, the decline's reason code once declined, OFFER_REJECTED_BY_FAC once the FAC
    /// rejected the offer. Null otherwise, and for decisions made before codes existed.</summary>
    string? StatusReasonCode = null);

public static class RhshfPortalStateBuilder
{
    public const string ReviewOffer = "REVIEW_OFFER";
    public const string CorrectProfile = "CORRECT_PROFILE";

    /// <summary>The status reason when the FAC rejected the offer. Fixed, not from the administrator's
    /// list: no member of staff picks it.</summary>
    public const string OfferRejectedByFac = "OFFER_REJECTED_BY_FAC";

    /// <summary>
    /// The version of the update contract, sent on every webhook and status check (CRMSQA#1). Goes up
    /// only for a change a reader must act on: a field removed, renamed or given a new meaning, or a
    /// new value in an existing field. A new optional field does not change it.
    /// </summary>
    public const int SchemaVersion = 1;

    /// <param name="currentOffer">The offer for the profile's current cycle, if one was issued.</param>
    /// <param name="offerUrl">FAC-facing offer page, without a token; null when no public host is set.</param>
    /// <param name="profilingUrl">FAC-facing profiling form, without a token; null when no public host is set.</param>
    public static RhshfPortalState Build(
        RhshfCreditProfile profile, RhshfOffer? currentOffer, string? offerUrl, string? profilingUrl)
    {
        var outstandingReturn = profile.GetOutstandingReturn();
        var creditStatus = CreditStatus(profile, currentOffer, outstandingReturn);

        var (actionRequired, actionUrl) = creditStatus switch
        {
            "OFFER_READY" => (ReviewOffer, offerUrl),
            "CORRECTION_REQUIRED" => (CorrectProfile, profilingUrl),
            _ => ((string?)null, (string?)null),
        };

        var offer = currentOffer is null
            ? null
            : new RhshfPortalOfferPayload(
                Status: currentOffer.Status.ToExternalOfferStatus(),
                Version: profile.OfferVersion,
                ApprovedAmount: profile.ApprovedAmount,
                Currency: profile.Currency,
                IssuedAt: currentOffer.GeneratedAt,
                RespondedAt: currentOffer.FacRespondedAt);

        var correction = outstandingReturn is null
            ? null
            : new RhshfPortalCorrectionPayload(
                Stage: outstandingReturn.ProfilingStage?.ToExternalStageName(),
                Message: string.IsNullOrWhiteSpace(outstandingReturn.Note) ? null : outstandingReturn.Note,
                RequestedAt: outstandingReturn.ChangedAt,
                ReasonCode: outstandingReturn.ReasonCode);

        // Every transition writes one history row, so the count only ever goes up — a monotonic
        // version without a column of its own. Cases from before the trail existed start low, but
        // still climb from wherever they are.
        DateTime? actionDeadline = creditStatus == "OFFER_READY" ? currentOffer?.ExpiresAt : null;

        var statusReasonCode = creditStatus switch
        {
            "CORRECTION_REQUIRED" => outstandingReturn?.ReasonCode,
            "CREDIT_DECLINED" => profile.DecisionReasonCode,
            "CANCELLED" => OfferRejectedByFac,
            _ => null,
        };

        return new RhshfPortalState(
            creditStatus, profile.StatusHistory.Count, actionRequired, actionUrl, offer, correction,
            DecisionBasis(profile, currentOffer), actionDeadline, statusReasonCode);
    }

    /// <summary>
    /// The EOPs behind an offer or a final decision, as the case holds them. The case never replaces
    /// a breakdown once held and refuses an amended one, so what it holds is what was decided on.
    /// </summary>
    private static IReadOnlyList<RhshfPortalDecisionBasis>? DecisionBasis(RhshfCreditProfile profile, RhshfOffer? currentOffer)
    {
        var decided = currentOffer is not null
                      || profile.Status is RhshfCaseStatus.Approved or RhshfCaseStatus.Declined;
        if (!decided)
            return null;

        var basis = profile.FinancingEops
            .Where(e => e.EopId is not null)
            .Select(e =>
            {
                var (hash, scheme) = e.DecisionHash;
                return new RhshfPortalDecisionBasis(e.EopId!.Value, e.EopReference, e.EopRevision, hash, scheme);
            })
            .ToList();
        return basis.Count == 0 ? null : basis;
    }

    private static string CreditStatus(
        RhshfCreditProfile profile, RhshfOffer? currentOffer, RhshfStatusHistory? outstandingReturn)
    {
        switch (profile.Status)
        {
            case RhshfCaseStatus.Approved: return "CREDIT_APPROVED";
            case RhshfCaseStatus.Declined: return "CREDIT_DECLINED";
            case RhshfCaseStatus.Cancelled: return "CANCELLED";
            case RhshfCaseStatus.Expired: return "EXPIRED";
            case RhshfCaseStatus.InfoRequired: return "CORRECTION_REQUIRED";
            case RhshfCaseStatus.Received:
            case RhshfCaseStatus.ProfilingPending: return "PROFILING_PENDING";
            case RhshfCaseStatus.ProfilingInProgress:
                return outstandingReturn is not null ? "CORRECTION_REQUIRED" : "PROFILING_IN_PROGRESS";
        }

        // UnderReview: the internal stage says how far it has got.
        return profile.InternalStage switch
        {
            RhshfInternalStage.OfferGenerated or RhshfInternalStage.AwaitingOfferAcceptance
                when currentOffer?.Status is RhshfOfferStatus.Generated or RhshfOfferStatus.AwaitingFacResponse
                => "OFFER_READY",
            RhshfInternalStage.PreDeploymentVerification or RhshfInternalStage.Disbursement => "OFFER_ACCEPTED",
            _ => "UNDER_CREDIT_REVIEW",
        };
    }
}
