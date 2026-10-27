using System.Text.Json.Serialization;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Application.Rhshf.Webhooks;

/// <summary>The offer as the portal needs to see it. Version counts ratifications that issued an
/// offer, so a re-issued offer is distinguishable from the one the FAC already saw.</summary>
public record RhshfPortalOfferPayload(
    string Status,
    int Version,
    decimal? ApprovedAmount,
    string Currency,
    DateTime IssuedAt,
    DateTime? RespondedAt);

/// <summary>What the FAC was asked to correct. Message is the returning officer's note, which the
/// return dialogs warn staff the FAC will read.</summary>
public record RhshfPortalCorrectionPayload(
    string? Stage,
    string? Message,
    DateTime RequestedAt);

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
    RhshfPortalCorrectionPayload? Correction);

public static class RhshfPortalStateBuilder
{
    public const string ReviewOffer = "REVIEW_OFFER";
    public const string CorrectProfile = "CORRECT_PROFILE";

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
                Version: profile.Ratifications.Count(r => r.Outcome == RhshfRatificationOutcome.Ratified),
                ApprovedAmount: profile.ApprovedAmount,
                Currency: profile.Currency,
                IssuedAt: currentOffer.GeneratedAt,
                RespondedAt: currentOffer.FacRespondedAt);

        var correction = outstandingReturn is null
            ? null
            : new RhshfPortalCorrectionPayload(
                Stage: outstandingReturn.ProfilingStage?.ToExternalStageName(),
                Message: string.IsNullOrWhiteSpace(outstandingReturn.Note) ? null : outstandingReturn.Note,
                RequestedAt: outstandingReturn.ChangedAt);

        // Every transition writes one history row, so the count only ever goes up — a monotonic
        // version without a column of its own. Cases from before the trail existed start low, but
        // still climb from wherever they are.
        return new RhshfPortalState(
            creditStatus, profile.StatusHistory.Count, actionRequired, actionUrl, offer, correction);
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
