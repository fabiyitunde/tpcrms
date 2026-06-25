using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Namp.Commands;

// ── Query ─────────────────────────────────────────────────────────────────────

public record GetNampDocumentTemplateQuery(NampDocumentType DocumentType)
    : IRequest<ApplicationResult<NampDocumentTemplateDto>>;

public class GetNampDocumentTemplateHandler
    : IRequestHandler<GetNampDocumentTemplateQuery, ApplicationResult<NampDocumentTemplateDto>>
{
    private readonly INampDocumentTemplateRepository _repo;

    public GetNampDocumentTemplateHandler(INampDocumentTemplateRepository repo) => _repo = repo;

    public async Task<ApplicationResult<NampDocumentTemplateDto>> Handle(
        GetNampDocumentTemplateQuery request, CancellationToken ct = default)
    {
        var template = await _repo.GetByTypeAsync(request.DocumentType, ct);
        if (template is null)
            return ApplicationResult<NampDocumentTemplateDto>.Failure("Template not found.");

        return ApplicationResult<NampDocumentTemplateDto>.Success(MapToDto(template));
    }

    internal static NampDocumentTemplateDto MapToDto(NampDocumentTemplate t) => new(
        t.Id, t.DocumentType, t.Title, t.BodyContent, t.ConditionsContent,
        t.Version, t.IsActive, t.LastModifiedAt);
}

public record GetAllNampDocumentTemplatesQuery : IRequest<ApplicationResult<List<NampDocumentTemplateDto>>>;

public class GetAllNampDocumentTemplatesHandler
    : IRequestHandler<GetAllNampDocumentTemplatesQuery, ApplicationResult<List<NampDocumentTemplateDto>>>
{
    private readonly INampDocumentTemplateRepository _repo;

    public GetAllNampDocumentTemplatesHandler(INampDocumentTemplateRepository repo) => _repo = repo;

    public async Task<ApplicationResult<List<NampDocumentTemplateDto>>> Handle(
        GetAllNampDocumentTemplatesQuery request, CancellationToken ct = default)
    {
        var templates = await _repo.GetAllAsync(ct);
        return ApplicationResult<List<NampDocumentTemplateDto>>.Success(
            templates.Select(GetNampDocumentTemplateHandler.MapToDto).ToList());
    }
}

// ── Command ───────────────────────────────────────────────────────────────────

public record UpsertNampDocumentTemplateCommand(
    NampDocumentType DocumentType,
    string Title,
    string BodyContent,
    Guid UserId,
    string? ConditionsContent = null
) : IRequest<ApplicationResult<NampDocumentTemplateDto>>;

public class UpsertNampDocumentTemplateHandler
    : IRequestHandler<UpsertNampDocumentTemplateCommand, ApplicationResult<NampDocumentTemplateDto>>
{
    private readonly INampDocumentTemplateRepository _repo;
    private readonly IUnitOfWork _uow;

    public UpsertNampDocumentTemplateHandler(INampDocumentTemplateRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampDocumentTemplateDto>> Handle(
        UpsertNampDocumentTemplateCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return ApplicationResult<NampDocumentTemplateDto>.Failure("Title is required.");
        if (string.IsNullOrWhiteSpace(request.BodyContent))
            return ApplicationResult<NampDocumentTemplateDto>.Failure("Body content is required.");

        var existing = await _repo.GetByTypeAsync(request.DocumentType, ct);

        if (existing is null)
        {
            var created = NampDocumentTemplate.Create(
                request.DocumentType, request.Title, request.BodyContent,
                request.UserId, request.ConditionsContent);
            await _repo.AddAsync(created, ct);
            await _uow.SaveChangesAsync(ct);
            return ApplicationResult<NampDocumentTemplateDto>.Success(
                GetNampDocumentTemplateHandler.MapToDto(created));
        }

        var updateResult = existing.Update(
            request.Title, request.BodyContent, request.UserId, request.ConditionsContent);
        if (updateResult.IsFailure)
            return ApplicationResult<NampDocumentTemplateDto>.Failure(updateResult.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult<NampDocumentTemplateDto>.Success(
            GetNampDocumentTemplateHandler.MapToDto(existing));
    }
}

// ── DTO ───────────────────────────────────────────────────────────────────────

public record NampDocumentTemplateDto(
    Guid Id,
    NampDocumentType DocumentType,
    string Title,
    string BodyContent,
    string? ConditionsContent,
    int Version,
    bool IsActive,
    DateTime? LastModifiedAt
);
