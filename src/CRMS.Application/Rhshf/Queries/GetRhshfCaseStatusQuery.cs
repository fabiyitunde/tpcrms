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
    string? ActionUrl);

public class GetRhshfCaseStatusHandler : IRequestHandler<GetRhshfCaseStatusQuery, ApplicationResult<RhshfCaseStatusDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfOfferRepository _offerRepo;
    private readonly IRhshfPublicUrlProvider _urls;

    public GetRhshfCaseStatusHandler(
        IRhshfCreditProfileRepository repo, IRhshfOfferRepository offerRepo, IRhshfPublicUrlProvider urls)
    {
        _repo = repo;
        _offerRepo = offerRepo;
        _urls = urls;
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

        // "REVIEW_OFFER" only while an offer is awaiting the FAC's response (§6 #10) — not before
        // (no offer generated yet) and not after (already accepted/rejected).
        string? actionRequired = null;
        string? actionUrl = null;
        if (profile.InternalStage == RhshfInternalStage.AwaitingOfferAcceptance)
        {
            var offer = await _offerRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
            if (offer is not null && offer.Status == RhshfOfferStatus.Generated)
            {
                actionRequired = "REVIEW_OFFER";
                // The profiling URL the portal already holds points at a completed wizard with no
                // route onward, so without this the FAC has nowhere to go and the case stalls.
                actionUrl = _urls.OfferUrl(profile.Reference);
            }
        }

        var dto = new RhshfCaseStatusDto(
            Reference: profile.Reference,
            SubmissionId: profile.SubmissionId,
            Status: profile.Status.ToExternalStatus(),
            Stage: stage,
            Decision: decision,
            UpdatedAt: profile.UpdatedAt,
            ActionRequired: actionRequired,
            ActionUrl: actionUrl);

        return ApplicationResult<RhshfCaseStatusDto>.Success(dto);
    }
}
