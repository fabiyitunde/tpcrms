using CRMS.Application.Common;
using CRMS.Application.Namp.Commands;
using CRMS.Application.Namp.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Namp.Queries;

public record GetNampViabilityScoreConfigsQuery
    : IRequest<ApplicationResult<List<NampViabilityScoreConfigDto>>>;

public class GetNampViabilityScoreConfigsHandler
    : IRequestHandler<GetNampViabilityScoreConfigsQuery, ApplicationResult<List<NampViabilityScoreConfigDto>>>
{
    private readonly INampViabilityScoreConfigRepository _repo;

    public GetNampViabilityScoreConfigsHandler(INampViabilityScoreConfigRepository repo)
    {
        _repo = repo;
    }

    public async Task<ApplicationResult<List<NampViabilityScoreConfigDto>>> Handle(
        GetNampViabilityScoreConfigsQuery request, CancellationToken ct = default)
    {
        var configs = await _repo.GetAllAsync(ct);
        var dtos = configs
            .OrderBy(c => c.ViabilityRating)
            .Select(UpdateNampViabilityScoreConfigHandler.MapToDto)
            .ToList();

        return ApplicationResult<List<NampViabilityScoreConfigDto>>.Success(dtos);
    }
}
