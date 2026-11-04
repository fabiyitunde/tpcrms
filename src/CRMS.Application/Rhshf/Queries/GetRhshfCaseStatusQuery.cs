using CRMS.Application.Common;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Application.Rhshf.Webhooks;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

/// <summary>GET /v1/credit-profiles/{reference}/status (§4.5, Phase 11) — lets the portal poll and
/// reconcile if a webhook is missed. Same auth as §4.1 (X-Api-Key), enforced by the controller.</summary>
public record GetRhshfCaseStatusQuery(string Reference) : IRequest<ApplicationResult<RhshfCaseStatusDto>>;

public record RhshfStageDto(string Current, int Index, int Total);

public record RhshfCaseStatusDto(
    string Reference,
    Guid SubmissionId,
    string Status,
    RhshfStageDto? Stage,
    RhshfWebhookDecisionPayload? Decision,
    DateTime UpdatedAt,
    string? ActionRequired,
    /// <summary>Where the FAC must go to satisfy ActionRequired. Returned beside it so the portal
    /// never has to hardcode a CRMS route; the portal appends a freshly minted token itself. Null
    /// when no action is pending, or when no public host is configured.</summary>
    string? ActionUrl,
    /// <summary>The finer-grained status for display; see RhshfPortalState.</summary>
    string CreditStatus,
    int StateVersion,
    RhshfPortalOfferPayload? Offer,
    RhshfPortalCorrectionPayload? Correction,
    /// <summary>The EOPs the offer or decision was made on; null until there is one.</summary>
    IReadOnlyList<RhshfPortalDecisionBasis>? DecisionBasis = null,
    /// <summary>The type of the last update the CRMS sent for this case (OFFER_READY, DECIDED, …);
    /// null if none has been sent. A poll is not an event of its own, so it reports the last one.</summary>
    string? EventType = null,
    /// <summary>By when the FAC must act on ActionRequired; null when there is no deadline.</summary>
    DateTime? ActionDeadline = null,
    int SchemaVersion = RhshfPortalStateBuilder.SchemaVersion);

public class GetRhshfCaseStatusHandler : IRequestHandler<GetRhshfCaseStatusQuery, ApplicationResult<RhshfCaseStatusDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfOfferRepository _offerRepo;
    private readonly IRhshfPublicUrlProvider _urls;
    private readonly IRhshfCallbackAttemptRepository _updates;

    public GetRhshfCaseStatusHandler(
        IRhshfCreditProfileRepository repo, IRhshfOfferRepository offerRepo, IRhshfPublicUrlProvider urls,
        IRhshfCallbackAttemptRepository updates)
    {
        _repo = repo;
        _offerRepo = offerRepo;
        _urls = urls;
        _updates = updates;
    }

    public async Task<ApplicationResult<RhshfCaseStatusDto>> Handle(GetRhshfCaseStatusQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfCaseStatusDto>.Failure("Case not found.");

        RhshfStageDto? stage = profile.CurrentStage is null
            ? null
            : new RhshfStageDto(
                profile.CurrentStage.Value.ToExternalStageName(),
                (int)profile.CurrentStage.Value,
                Enum.GetValues<RhshfProfilingStage>().Length);

        var decision = RhshfWebhookPayloadBuilder.BuildDecision(profile, profile.DecidedAt ?? profile.UpdatedAt);

        // Same derivation as the webhook, so polling and the push can never tell the portal two
        // different things. "REVIEW_OFFER" only while an offer is awaiting the FAC's response (§6
        // #10); "CORRECT_PROFILE" only while a return is unanswered.
        var offer = await _offerRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        var state = RhshfPortalStateBuilder.Build(
            profile, offer, _urls.OfferUrl(profile.Reference), _urls.ProfilingUrl(profile.Reference));

        var dto = new RhshfCaseStatusDto(
            Reference: profile.Reference,
            SubmissionId: profile.SubmissionId,
            Status: profile.Status.ToExternalStatus(),
            Stage: stage,
            Decision: decision,
            UpdatedAt: profile.UpdatedAt,
            ActionRequired: state.ActionRequired,
            ActionUrl: state.ActionUrl,
            CreditStatus: state.CreditStatus,
            StateVersion: state.StateVersion,
            Offer: state.Offer,
            Correction: state.Correction,
            DecisionBasis: state.DecisionBasis,
            EventType: (await _updates.GetByProfileAsync(profile.Id, ct))
                .OrderByDescending(a => a.EventOccurredAt)
                .FirstOrDefault()?.EventType.ToExternalEventType(),
            ActionDeadline: state.ActionDeadline);

        return ApplicationResult<RhshfCaseStatusDto>.Success(dto);
    }
}
