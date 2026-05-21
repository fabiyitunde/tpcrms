using CRMS.Application.Common;
using CRMS.Application.Namp.DTOs;
using CRMS.Application.Namp.Queries;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Namp.Commands;

public record UploadNampDocumentCommand(
    Guid NampApplicationId,
    Guid UserId,
    string Stage,
    string FileName,
    string ContentType,
    long FileSize,
    string StoragePath,
    string Category = "General",
    string? Description = null
) : IRequest<ApplicationResult<NampApplicationDto>>;

public class UploadNampDocumentHandler
    : IRequestHandler<UploadNampDocumentCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly IUnitOfWork _uow;

    public UploadNampDocumentHandler(INampApplicationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        UploadNampDocumentCommand request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<NampDocumentStage>(request.Stage, ignoreCase: true, out var stage))
            return ApplicationResult<NampApplicationDto>.Failure($"Unknown document stage: '{request.Stage}'.");

        if (!Enum.TryParse<NampDocumentCategory>(request.Category, ignoreCase: true, out var category))
            category = NampDocumentCategory.General;

        var app = await _repo.GetByIdAsync(request.NampApplicationId, ct);
        if (app is null) return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        // UploadDocument returns the new entity so we can explicitly Add it via DbSet,
        // bypassing the EF navigation-discovery tracking issue (Modified instead of Added).
        var doc = app.UploadDocument(stage, request.FileName, request.ContentType, request.FileSize,
            request.StoragePath, request.UserId, category, request.Description);
        await _repo.AddNampDocumentAsync(doc, ct);
        await _uow.SaveChangesAsync(ct);

        var full = await _repo.GetByIdWithDetailsAsync(app.Id, ct);
        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(full!));
    }
}
