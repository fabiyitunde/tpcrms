using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfPreDeploymentChecklistQuery(string Reference) : IRequest<ApplicationResult<List<RhshfPreDeploymentChecklistItemDto>>>;

public class GetRhshfPreDeploymentChecklistHandler
    : IRequestHandler<GetRhshfPreDeploymentChecklistQuery, ApplicationResult<List<RhshfPreDeploymentChecklistItemDto>>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfOfferRepository _offerRepo;
    private readonly IUserNameResolver _names;

    public GetRhshfPreDeploymentChecklistHandler(
        IRhshfCreditProfileRepository repo, IRhshfOfferRepository offerRepo, IUserNameResolver names)
    {
        _repo = repo;
        _offerRepo = offerRepo;
        _names = names;
    }

    public async Task<ApplicationResult<List<RhshfPreDeploymentChecklistItemDto>>> Handle(
        GetRhshfPreDeploymentChecklistQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<List<RhshfPreDeploymentChecklistItemDto>>.Failure("Case not found.");

        var items = profile.PreDeploymentChecklist
            .Where(i => i.CycleNumber == profile.CurrentCycleNumber)
            .OrderBy(i => i.SortOrder)
            .ToList();

        // Derive the auto (offer-documents) items against the live offer so the gate shows reality, not a
        // stale tick — computed for display only (no mutation on a read; the complete handler persists it).
        var offer = await _offerRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        var offerDocsOnFile = offer?.HasRequiredSignedDocuments ?? false;

        var names = await _names.ResolveManyAsync(
            items.Where(i => i.ConfirmedByUserId.HasValue).Select(i => i.ConfirmedByUserId!.Value), ct);

        var dtos = items
            .Select(i =>
            {
                if (i.Kind == RhshfPreDeploymentVerificationKind.OfferDocuments)
                    return new RhshfPreDeploymentChecklistItemDto(
                        i.Id, i.Title, i.Description, i.IsMandatory, i.Kind,
                        offerDocsOnFile, null, null, null,
                        offerDocsOnFile
                            ? "Verified automatically — signed offer documents on file."
                            : "Awaiting the FAC's signed offer documents.");

                return new RhshfPreDeploymentChecklistItemDto(
                    i.Id, i.Title, i.Description, i.IsMandatory, i.Kind, i.IsConfirmed, i.ConfirmedByUserId,
                    i.ConfirmedByUserId.HasValue && names.TryGetValue(i.ConfirmedByUserId.Value, out var n) ? n : null,
                    i.ConfirmedAt, i.Notes);
            })
            .ToList();

        return ApplicationResult<List<RhshfPreDeploymentChecklistItemDto>>.Success(dtos);
    }
}
