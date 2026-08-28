using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfOfferQuery(string Reference) : IRequest<ApplicationResult<RhshfOfferDto>>;

public record RhshfOfferDto(int CycleNumber, DateTime GeneratedAt, string OfferDocumentPath, RhshfOfferStatus Status);

public class GetRhshfOfferHandler : IRequestHandler<GetRhshfOfferQuery, ApplicationResult<RhshfOfferDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfOfferRepository _offerRepo;

    public GetRhshfOfferHandler(IRhshfCreditProfileRepository repo, IRhshfOfferRepository offerRepo)
    {
        _repo = repo;
        _offerRepo = offerRepo;
    }

    public async Task<ApplicationResult<RhshfOfferDto>> Handle(GetRhshfOfferQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfOfferDto>.Failure("Case not found.");

        var offer = await _offerRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        if (offer is null)
            return ApplicationResult<RhshfOfferDto>.Failure("No offer has been generated for this case's current cycle.");

        return ApplicationResult<RhshfOfferDto>.Success(
            new RhshfOfferDto(offer.CycleNumber, offer.GeneratedAt, offer.OfferDocumentPath, offer.Status));
    }
}
