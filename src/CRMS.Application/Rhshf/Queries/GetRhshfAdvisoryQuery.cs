using CRMS.Application.Common;
using CRMS.Application.Rhshf.Commands;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfAdvisoryQuery(string Reference) : IRequest<ApplicationResult<RhshfAdvisoryDto?>>;

public class GetRhshfAdvisoryHandler : IRequestHandler<GetRhshfAdvisoryQuery, ApplicationResult<RhshfAdvisoryDto?>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfAdvisoryRepository _advisoryRepo;

    public GetRhshfAdvisoryHandler(IRhshfCreditProfileRepository repo, IRhshfAdvisoryRepository advisoryRepo)
    {
        _repo = repo;
        _advisoryRepo = advisoryRepo;
    }

    public async Task<ApplicationResult<RhshfAdvisoryDto?>> Handle(GetRhshfAdvisoryQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfAdvisoryDto?>.Failure("Case not found.");

        var advisory = await _advisoryRepo.GetByRhshfCreditProfileIdAsync(profile.Id, ct);
        return ApplicationResult<RhshfAdvisoryDto?>.Success(advisory is null ? null : GenerateRhshfAdvisoryHandler.MapToDto(advisory));
    }
}
