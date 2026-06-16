using CRMS.Application.Common;
using CRMS.Application.Namp.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Namp.Commands;

public record UpdateNampWorkflowConfigCommand(
    Guid Id,
    string AssignedRole,
    int SlaHours,
    Guid UpdatedByUserId
) : IRequest<ApplicationResult<NampWorkflowConfigDto>>;

public class UpdateNampWorkflowConfigHandler
    : IRequestHandler<UpdateNampWorkflowConfigCommand, ApplicationResult<NampWorkflowConfigDto>>
{
    private readonly INampWorkflowConfigRepository _repo;
    private readonly IUnitOfWork _uow;

    public UpdateNampWorkflowConfigHandler(INampWorkflowConfigRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampWorkflowConfigDto>> Handle(
        UpdateNampWorkflowConfigCommand request, CancellationToken ct = default)
    {
        var config = await _repo.GetByIdAsync(request.Id, ct);
        if (config is null) return ApplicationResult<NampWorkflowConfigDto>.Failure("Workflow stage config not found.");

        var result = config.Update(request.AssignedRole, request.SlaHours);
        if (result.IsFailure) return ApplicationResult<NampWorkflowConfigDto>.Failure(result.Error);

        config.SetAuditInfo(request.UpdatedByUserId.ToString());
        _repo.Update(config);
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampWorkflowConfigDto>.Success(new NampWorkflowConfigDto(
            config.Id,
            config.Status.ToString(),
            config.DisplayName,
            config.Description,
            config.AssignedRole,
            config.SlaHours,
            config.SortOrder,
            config.IsTerminal));
    }
}
