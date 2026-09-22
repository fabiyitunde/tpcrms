using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfPreDeployTemplatesQuery : IRequest<ApplicationResult<List<RhshfPreDeploymentChecklistTemplateDto>>>;

public class GetRhshfPreDeployTemplatesHandler
    : IRequestHandler<GetRhshfPreDeployTemplatesQuery, ApplicationResult<List<RhshfPreDeploymentChecklistTemplateDto>>>
{
    private readonly IRhshfPreDeploymentChecklistTemplateRepository _repo;

    public GetRhshfPreDeployTemplatesHandler(IRhshfPreDeploymentChecklistTemplateRepository repo) => _repo = repo;

    public async Task<ApplicationResult<List<RhshfPreDeploymentChecklistTemplateDto>>> Handle(
        GetRhshfPreDeployTemplatesQuery request, CancellationToken ct = default)
    {
        var templates = await _repo.GetAllAsync(ct);
        var dtos = templates.Select(t => new RhshfPreDeploymentChecklistTemplateDto(
            t.Id, t.Title, t.Description, t.IsMandatory, t.SortOrder, t.IsActive)).ToList();

        return ApplicationResult<List<RhshfPreDeploymentChecklistTemplateDto>>.Success(dtos);
    }
}
