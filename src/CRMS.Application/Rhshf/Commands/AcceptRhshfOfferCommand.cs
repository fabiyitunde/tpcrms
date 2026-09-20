using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// FAC accepts the offer (design doc §3.6/§6 #10). RhshfOffer.Accept() enforces the signed-document
/// precondition; on success, propagates to RhshfCreditProfile.AdvanceToLegalClearance() — both
/// aggregates save in one transaction, same cross-aggregate pattern as Phase 5/6.
/// </summary>
public record AcceptRhshfOfferCommand(string Reference, string? Notes) : IRequest<ApplicationResult>;

public class AcceptRhshfOfferHandler : IRequestHandler<AcceptRhshfOfferCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfOfferRepository _offerRepo;
    private readonly IUnitOfWork _uow;

    public AcceptRhshfOfferHandler(IRhshfCreditProfileRepository repo, IRhshfOfferRepository offerRepo, IUnitOfWork uow)
    {
        _repo = repo;
        _offerRepo = offerRepo;
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

        var profileResult = profile.AdvanceToLegalClearance();
        if (profileResult.IsFailure)
            return ApplicationResult.Failure(profileResult.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
