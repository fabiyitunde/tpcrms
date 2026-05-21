using CRMS.Application.Common;
using CRMS.Application.Namp.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Namp.Queries;

public record GetNampRoutingConfigsQuery : IRequest<ApplicationResult<List<NampRoutingConfigDto>>>;

public class GetNampRoutingConfigsHandler
    : IRequestHandler<GetNampRoutingConfigsQuery, ApplicationResult<List<NampRoutingConfigDto>>>
{
    private readonly INampRoutingConfigRepository _repo;

    public GetNampRoutingConfigsHandler(INampRoutingConfigRepository repo) => _repo = repo;

    public async Task<ApplicationResult<List<NampRoutingConfigDto>>> Handle(
        GetNampRoutingConfigsQuery request, CancellationToken ct = default)
    {
        var configs = await _repo.GetAllAsync(ct);
        var dtos = configs.Select(c => new NampRoutingConfigDto(
            c.Id,
            c.ApplicantCategory.ToString(),
            c.CommitteeTier.ToString(),
            c.MinEquipmentValue,
            c.MaxEquipmentValue,
            c.Priority,
            c.IsActive,
            c.CreatedAt
        )).ToList();

        return ApplicationResult<List<NampRoutingConfigDto>>.Success(dtos);
    }
}

public record GetNampWorkflowConfigsQuery : IRequest<ApplicationResult<List<NampWorkflowConfigDto>>>;

public class GetNampWorkflowConfigsHandler
    : IRequestHandler<GetNampWorkflowConfigsQuery, ApplicationResult<List<NampWorkflowConfigDto>>>
{
    private readonly INampWorkflowConfigRepository _repo;

    public GetNampWorkflowConfigsHandler(INampWorkflowConfigRepository repo) => _repo = repo;

    public async Task<ApplicationResult<List<NampWorkflowConfigDto>>> Handle(
        GetNampWorkflowConfigsQuery request, CancellationToken ct = default)
    {
        var configs = await _repo.GetAllAsync(ct);
        var dtos = configs.OrderBy(c => c.SortOrder).Select(c => new NampWorkflowConfigDto(
            c.Id,
            c.Status.ToString(),
            c.DisplayName,
            c.Description,
            c.AssignedRole,
            c.SlaHours,
            c.SortOrder,
            c.IsTerminal
        )).ToList();

        return ApplicationResult<List<NampWorkflowConfigDto>>.Success(dtos);
    }
}
