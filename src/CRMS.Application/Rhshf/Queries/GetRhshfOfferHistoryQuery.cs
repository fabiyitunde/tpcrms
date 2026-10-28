using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

/// <summary>Every offer issued on a case, newest first. The Offer tab otherwise shows only the
/// current one, so an officer could not see that an earlier version had been accepted and replaced,
/// or open the FAC's signed copy of it (CRMSQA#2).</summary>
public record GetRhshfOfferHistoryQuery(string Reference) : IRequest<ApplicationResult<List<RhshfOfferHistoryItemDto>>>;

/// <param name="Version">1 for the first offer on the case, counting up with every one issued after
/// it — the number printed on each letter.</param>
public record RhshfOfferHistoryItemDto(
    Guid Id,
    int Version,
    int CycleNumber,
    DateTime GeneratedAt,
    RhshfOfferStatus Status,
    DateTime? FacRespondedAt,
    bool IsCurrent,
    List<RhshfOfferDocumentDto> SignedDocuments);

public class GetRhshfOfferHistoryHandler : IRequestHandler<GetRhshfOfferHistoryQuery, ApplicationResult<List<RhshfOfferHistoryItemDto>>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfOfferRepository _offerRepo;

    public GetRhshfOfferHistoryHandler(IRhshfCreditProfileRepository repo, IRhshfOfferRepository offerRepo)
    {
        _repo = repo;
        _offerRepo = offerRepo;
    }

    public async Task<ApplicationResult<List<RhshfOfferHistoryItemDto>>> Handle(GetRhshfOfferHistoryQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<List<RhshfOfferHistoryItemDto>>.Failure("Case not found.");

        var offers = await _offerRepo.GetAllByProfileAsync(profile.Id, ct);
        var current = offers.LastOrDefault();

        // Every offer row is created by exactly one ratification or one correction, so its position
        // in issue order is the version it was printed with.
        var items = offers
            .Select((o, i) => new RhshfOfferHistoryItemDto(
                o.Id, i + 1, o.CycleNumber, o.GeneratedAt, o.Status, o.FacRespondedAt,
                IsCurrent: o.Id == current?.Id,
                o.Documents.Select(d => new RhshfOfferDocumentDto(d.Id, d.FileName, d.SizeBytes, d.UploadedAt, d.Kind)).ToList()))
            .Reverse()
            .ToList();

        return ApplicationResult<List<RhshfOfferHistoryItemDto>>.Success(items);
    }
}
