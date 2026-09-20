using CRMS.Application.Common;
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
    string? ActionRequired);

public class GetRhshfCaseStatusHandler : IRequestHandler<GetRhshfCaseStatusQuery, ApplicationResult<RhshfCaseStatusDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfOfferRepository _offerRepo;

    public GetRhshfCaseStatusHandler(IRhshfCreditProfileRepository repo, IRhshfOfferRepository offerRepo)
    {
        _repo = repo;
        _offerRepo = offerRepo;
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
        if (profile.InternalStage == RhshfInternalStage.AwaitingOfferAcceptance)
        {
            var offer = await _offerRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
            if (offer is not null && offer.Status == RhshfOfferStatus.Generated)
                actionRequired = "REVIEW_OFFER";
        }

        var dto = new RhshfCaseStatusDto(
            Reference: profile.Reference,
            SubmissionId: profile.SubmissionId,
            Status: profile.Status.ToExternalStatus(),
            Stage: stage,
            Decision: decision,
            UpdatedAt: profile.UpdatedAt,
            ActionRequired: actionRequired);

        return ApplicationResult<RhshfCaseStatusDto>.Success(dto);
    }
}
