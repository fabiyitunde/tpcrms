using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfPreDeploymentChecklistQuery(string Reference) : IRequest<ApplicationResult<List<RhshfPreDeploymentChecklistItemDto>>>;

public class GetRhshfPreDeploymentChecklistHandler
    : IRequestHandler<GetRhshfPreDeploymentChecklistQuery, ApplicationResult<List<RhshfPreDeploymentChecklistItemDto>>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUserNameResolver _names;

    public GetRhshfPreDeploymentChecklistHandler(IRhshfCreditProfileRepository repo, IUserNameResolver names)
    {
        _repo = repo;
        _names = names;
    }

    public async Task<ApplicationResult<List<RhshfPreDeploymentChecklistItemDto>>> Handle(
        GetRhshfPreDeploymentChecklistQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<List<RhshfPreDeploymentChecklistItemDto>>.Failure("Case not found.");

        var items = profile.PreDeploymentChecklist
            .Where(i => i.CycleNumber == profile.CurrentCycleNumber)
            .OrderBy(i => i.SortOrder)
            .ToList();

        var names = await _names.ResolveManyAsync(
            items.Where(i => i.ConfirmedByUserId.HasValue).Select(i => i.ConfirmedByUserId!.Value), ct);

        var dtos = items
            .Select(i => new RhshfPreDeploymentChecklistItemDto(
                i.Id, i.Title, i.Description, i.IsMandatory, i.IsConfirmed, i.ConfirmedByUserId,
                i.ConfirmedByUserId.HasValue && names.TryGetValue(i.ConfirmedByUserId.Value, out var n) ? n : null,
                i.ConfirmedAt, i.Notes))
            .ToList();

        return ApplicationResult<List<RhshfPreDeploymentChecklistItemDto>>.Success(dtos);
    }
}
