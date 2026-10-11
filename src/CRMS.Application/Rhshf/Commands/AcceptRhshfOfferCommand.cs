using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// FAC accepts the offer (design doc §3.6/§6 #10). RhshfOffer.Accept() enforces the signed-document
/// precondition; on success the case goes straight to pre-deployment verification — legal clearance
/// now happens before committee voting, so there is no legal stage left after acceptance. This is
/// therefore also where the pre-deployment gate checklist is seeded (it used to be seeded when legal
/// granted clearance). Both aggregates save in one transaction, same cross-aggregate pattern as
/// Phase 5/6.
/// </summary>
public record AcceptRhshfOfferCommand(string Reference, string? Notes) : IRequest<ApplicationResult>;

public class AcceptRhshfOfferHandler : IRequestHandler<AcceptRhshfOfferCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfOfferRepository _offerRepo;
    private readonly IRhshfPreDeploymentChecklistTemplateRepository _templateRepo;
    private readonly IRhshfCommitteeReviewRepository _committeeRepo;
    private readonly IUnitOfWork _uow;

    public AcceptRhshfOfferHandler(
        IRhshfCreditProfileRepository repo, IRhshfOfferRepository offerRepo,
        IRhshfPreDeploymentChecklistTemplateRepository templateRepo, IRhshfCommitteeReviewRepository committeeRepo,
        IUnitOfWork uow)
    {
        _repo = repo;
        _offerRepo = offerRepo;
        _templateRepo = templateRepo;
        _committeeRepo = committeeRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(AcceptRhshfOfferCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var offer = await _offerRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        if (offer is null)
            return ApplicationResult.Failure("No offer has been generated for this case's current cycle.");

        var offerResult = offer.Accept(request.Notes);
        if (offerResult.IsFailure)
            return ApplicationResult.Failure(offerResult.Error);

        var profileResult = profile.AdvanceToPreDeploymentFromOffer();
        if (profileResult.IsFailure)
            return ApplicationResult.Failure(profileResult.Error);

        var templates = await _templateRepo.GetActiveAsync(ct);
        profile.SeedPreDeploymentChecklist(templates);

        // If the committee approved on the basis that perfection would follow, that promise becomes a
        // mandatory gate item here — otherwise the deferral would be a note nobody enforced and the
        // case could disburse against unperfected security.
        var review = await _committeeRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        if (review?.PerfectionDisposition == RhshfPerfectionDisposition.Deferred)
            profile.AddPerfectionConditionToPreDeploymentChecklist();

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
