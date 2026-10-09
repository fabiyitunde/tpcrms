using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

// ── Admin: manage Pre-Deployment gate checklist templates ──────────────────

public record CreateRhshfPreDeployTemplateCommand(
    string Title, string? Description, bool IsMandatory, int SortOrder,
    RhshfPreDeploymentVerificationKind Kind = RhshfPreDeploymentVerificationKind.Manual)
    : IRequest<ApplicationResult<RhshfPreDeploymentChecklistTemplateDto>>;

public class CreateRhshfPreDeployTemplateHandler
    : IRequestHandler<CreateRhshfPreDeployTemplateCommand, ApplicationResult<RhshfPreDeploymentChecklistTemplateDto>>
{
    private readonly IRhshfPreDeploymentChecklistTemplateRepository _repo;
    private readonly IUnitOfWork _uow;

    public CreateRhshfPreDeployTemplateHandler(IRhshfPreDeploymentChecklistTemplateRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<RhshfPreDeploymentChecklistTemplateDto>> Handle(
        CreateRhshfPreDeployTemplateCommand request, CancellationToken ct = default)
    {
        var result = RhshfPreDeploymentChecklistTemplate.Create(request.Title, request.Description, request.IsMandatory, request.SortOrder, request.Kind);
        if (result.IsFailure)
            return ApplicationResult<RhshfPreDeploymentChecklistTemplateDto>.Failure(result.Error);

        await _repo.AddAsync(result.Value, ct);
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<RhshfPreDeploymentChecklistTemplateDto>.Success(MapToDto(result.Value));
    }

    internal static RhshfPreDeploymentChecklistTemplateDto MapToDto(RhshfPreDeploymentChecklistTemplate t) => new(
        t.Id, t.Title, t.Description, t.IsMandatory, t.Kind, t.SortOrder, t.IsActive);
}

public record UpdateRhshfPreDeployTemplateCommand(
    Guid Id, string Title, string? Description, bool IsMandatory, int SortOrder,
    RhshfPreDeploymentVerificationKind Kind = RhshfPreDeploymentVerificationKind.Manual)
    : IRequest<ApplicationResult<RhshfPreDeploymentChecklistTemplateDto>>;

public class UpdateRhshfPreDeployTemplateHandler
    : IRequestHandler<UpdateRhshfPreDeployTemplateCommand, ApplicationResult<RhshfPreDeploymentChecklistTemplateDto>>
{
    private readonly IRhshfPreDeploymentChecklistTemplateRepository _repo;
    private readonly IUnitOfWork _uow;

    public UpdateRhshfPreDeployTemplateHandler(IRhshfPreDeploymentChecklistTemplateRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<RhshfPreDeploymentChecklistTemplateDto>> Handle(
        UpdateRhshfPreDeployTemplateCommand request, CancellationToken ct = default)
    {
        var template = await _repo.GetByIdAsync(request.Id, ct);
        if (template is null)
            return ApplicationResult<RhshfPreDeploymentChecklistTemplateDto>.Failure("Checklist template not found.");

        var result = template.Update(request.Title, request.Description, request.IsMandatory, request.SortOrder, request.Kind);
        if (result.IsFailure)
            return ApplicationResult<RhshfPreDeploymentChecklistTemplateDto>.Failure(result.Error);

        _repo.Update(template);
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<RhshfPreDeploymentChecklistTemplateDto>.Success(CreateRhshfPreDeployTemplateHandler.MapToDto(template));
    }
}

public record ToggleRhshfPreDeployTemplateCommand(Guid Id, bool Activate)
    : IRequest<ApplicationResult<RhshfPreDeploymentChecklistTemplateDto>>;

public class ToggleRhshfPreDeployTemplateHandler
    : IRequestHandler<ToggleRhshfPreDeployTemplateCommand, ApplicationResult<RhshfPreDeploymentChecklistTemplateDto>>
{
    private readonly IRhshfPreDeploymentChecklistTemplateRepository _repo;
    private readonly IUnitOfWork _uow;

    public ToggleRhshfPreDeployTemplateHandler(IRhshfPreDeploymentChecklistTemplateRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<RhshfPreDeploymentChecklistTemplateDto>> Handle(
        ToggleRhshfPreDeployTemplateCommand request, CancellationToken ct = default)
    {
        var template = await _repo.GetByIdAsync(request.Id, ct);
        if (template is null)
            return ApplicationResult<RhshfPreDeploymentChecklistTemplateDto>.Failure("Checklist template not found.");

        if (request.Activate) template.Activate(); else template.Deactivate();
        _repo.Update(template);
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<RhshfPreDeploymentChecklistTemplateDto>.Success(CreateRhshfPreDeployTemplateHandler.MapToDto(template));
    }
}
