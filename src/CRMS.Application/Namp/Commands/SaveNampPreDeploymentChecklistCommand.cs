using CRMS.Application.Common;
using CRMS.Application.Namp.DTOs;
using CRMS.Application.Namp.Queries;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Namp.Commands;

// ── Admin: manage checklist templates ─────────────────────────────────────

public record AddNampChecklistTemplateItemCommand(
    string Title,
    string? Description,
    bool RequiresDocumentUpload,
    string? DocumentCategory,
    bool IsMandatory,
    int SortOrder
) : IRequest<ApplicationResult<NampPreDeploymentChecklistTemplateDto>>;

public class AddNampChecklistTemplateItemHandler
    : IRequestHandler<AddNampChecklistTemplateItemCommand, ApplicationResult<NampPreDeploymentChecklistTemplateDto>>
{
    private readonly INampPreDeploymentChecklistTemplateRepository _repo;
    private readonly IUnitOfWork _uow;

    public AddNampChecklistTemplateItemHandler(INampPreDeploymentChecklistTemplateRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampPreDeploymentChecklistTemplateDto>> Handle(
        AddNampChecklistTemplateItemCommand request, CancellationToken ct = default)
    {
        NampDocumentCategory? category = null;
        if (!string.IsNullOrWhiteSpace(request.DocumentCategory) &&
            Enum.TryParse<NampDocumentCategory>(request.DocumentCategory, out var parsed))
            category = parsed;

        var result = NampPreDeploymentChecklistTemplate.Create(
            request.Title, request.Description,
            request.RequiresDocumentUpload, category,
            request.IsMandatory, request.SortOrder);

        if (result.IsFailure)
            return ApplicationResult<NampPreDeploymentChecklistTemplateDto>.Failure(result.Error);

        await _repo.AddAsync(result.Value, ct);
        await _uow.SaveChangesAsync(ct);

        var dto = new NampPreDeploymentChecklistTemplateDto(
            result.Value.Id, result.Value.Title, result.Value.Description,
            result.Value.RequiresDocumentUpload, result.Value.DocumentCategory?.ToString(),
            result.Value.IsMandatory, result.Value.SortOrder, result.Value.IsActive, result.Value.CreatedAt);

        return ApplicationResult<NampPreDeploymentChecklistTemplateDto>.Success(dto);
    }
}

public record UpdateNampChecklistTemplateItemCommand(
    Guid Id,
    string Title,
    string? Description,
    bool RequiresDocumentUpload,
    string? DocumentCategory,
    bool IsMandatory,
    int SortOrder,
    bool IsActive
) : IRequest<ApplicationResult<NampPreDeploymentChecklistTemplateDto>>;

public class UpdateNampChecklistTemplateItemHandler
    : IRequestHandler<UpdateNampChecklistTemplateItemCommand, ApplicationResult<NampPreDeploymentChecklistTemplateDto>>
{
    private readonly INampPreDeploymentChecklistTemplateRepository _repo;
    private readonly IUnitOfWork _uow;

    public UpdateNampChecklistTemplateItemHandler(INampPreDeploymentChecklistTemplateRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampPreDeploymentChecklistTemplateDto>> Handle(
        UpdateNampChecklistTemplateItemCommand request, CancellationToken ct = default)
    {
        var template = await _repo.GetByIdAsync(request.Id, ct);
        if (template is null)
            return ApplicationResult<NampPreDeploymentChecklistTemplateDto>.Failure("Checklist template item not found.");

        NampDocumentCategory? category = null;
        if (!string.IsNullOrWhiteSpace(request.DocumentCategory) &&
            Enum.TryParse<NampDocumentCategory>(request.DocumentCategory, out var parsed))
            category = parsed;

        var result = template.Update(
            request.Title, request.Description,
            request.RequiresDocumentUpload, category,
            request.IsMandatory, request.SortOrder);

        if (result.IsFailure)
            return ApplicationResult<NampPreDeploymentChecklistTemplateDto>.Failure(result.Error);

        if (request.IsActive) template.Activate(); else template.Deactivate();

        await _uow.SaveChangesAsync(ct);

        var dto = new NampPreDeploymentChecklistTemplateDto(
            template.Id, template.Title, template.Description,
            template.RequiresDocumentUpload, template.DocumentCategory?.ToString(),
            template.IsMandatory, template.SortOrder, template.IsActive, template.CreatedAt);

        return ApplicationResult<NampPreDeploymentChecklistTemplateDto>.Success(dto);
    }
}

// ── Application: confirm a checklist item ─────────────────────────────────

public record ConfirmNampChecklistItemCommand(
    Guid NampApplicationId,
    Guid ChecklistItemId,
    Guid UserId,
    bool? IsConfirmed,
    string? Notes
) : IRequest<ApplicationResult<NampApplicationDto>>;

public class ConfirmNampChecklistItemHandler
    : IRequestHandler<ConfirmNampChecklistItemCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly IUnitOfWork _uow;

    public ConfirmNampChecklistItemHandler(INampApplicationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        ConfirmNampChecklistItemCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null)
            return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        var result = app.ConfirmChecklistItem(
            request.ChecklistItemId, request.UserId, request.IsConfirmed, request.Notes);

        if (result.IsFailure)
            return ApplicationResult<NampApplicationDto>.Failure(result.Error);

        app.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(app));
    }
}
