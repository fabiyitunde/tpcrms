using CRMS.Application.Common;
using CRMS.Application.Namp.Commands;
using CRMS.Application.Namp.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Namp.Queries;

public record GetNampAdvisoryQuery(Guid NampApplicationId)
    : IRequest<ApplicationResult<NampAdvisoryDto>>;

public class GetNampAdvisoryHandler
    : IRequestHandler<GetNampAdvisoryQuery, ApplicationResult<NampAdvisoryDto>>
{
    private readonly INampAdvisoryRepository _repo;

    public GetNampAdvisoryHandler(INampAdvisoryRepository repo)
    {
        _repo = repo;
    }

    public async Task<ApplicationResult<NampAdvisoryDto>> Handle(
        GetNampAdvisoryQuery request, CancellationToken ct = default)
    {
        var advisory = await _repo.GetByNampApplicationIdAsync(request.NampApplicationId, ct);
        if (advisory is null)
            return ApplicationResult<NampAdvisoryDto>.Failure("No advisory found for this NAMP application.");

        return ApplicationResult<NampAdvisoryDto>.Success(
            GenerateNampAdvisoryHandler.MapToDto(advisory));
    }
}
