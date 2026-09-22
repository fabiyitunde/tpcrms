using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfDocumentRequirementsQuery(bool ActiveOnly = false)
    : IRequest<ApplicationResult<List<RhshfDocumentRequirementDto>>>;

public class GetRhshfDocumentRequirementsHandler
    : IRequestHandler<GetRhshfDocumentRequirementsQuery, ApplicationResult<List<RhshfDocumentRequirementDto>>>
{
    private readonly IRhshfDocumentRequirementRepository _repo;

    public GetRhshfDocumentRequirementsHandler(IRhshfDocumentRequirementRepository repo) => _repo = repo;

    public async Task<ApplicationResult<List<RhshfDocumentRequirementDto>>> Handle(
        GetRhshfDocumentRequirementsQuery request, CancellationToken ct = default)
    {
        var requirements = request.ActiveOnly
            ? await _repo.GetActiveAsync(ct)
            : await _repo.GetAllAsync(ct);

        var dtos = requirements
            .Select(r => new RhshfDocumentRequirementDto(
                r.Id, r.Category, r.Title, r.Description, r.IsMandatory, r.SortOrder, r.IsActive))
            .ToList();

        return ApplicationResult<List<RhshfDocumentRequirementDto>>.Success(dtos);
    }
}
