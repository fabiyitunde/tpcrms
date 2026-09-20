using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfRoutingConfigsQuery : IRequest<ApplicationResult<List<RhshfRoutingConfigDto>>>;

public class GetRhshfRoutingConfigsHandler
    : IRequestHandler<GetRhshfRoutingConfigsQuery, ApplicationResult<List<RhshfRoutingConfigDto>>>
{
    private readonly IRhshfRoutingConfigRepository _repo;

    public GetRhshfRoutingConfigsHandler(IRhshfRoutingConfigRepository repo) => _repo = repo;

    public async Task<ApplicationResult<List<RhshfRoutingConfigDto>>> Handle(
        GetRhshfRoutingConfigsQuery request, CancellationToken ct = default)
    {
        var configs = await _repo.GetAllAsync(ct);
        var dtos = configs.Select(c => new RhshfRoutingConfigDto(
            c.Id, c.Tier.ToString(), c.MinEopValue, c.MaxEopValue, c.Priority, c.IsActive, c.CreatedAt
        )).ToList();

        return ApplicationResult<List<RhshfRoutingConfigDto>>.Success(dtos);
    }
}
