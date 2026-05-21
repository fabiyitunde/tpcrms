using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Namp.Commands;

public record UpdateNampDocumentCommand(
    Guid DocumentId,
    Guid UserId,
    string Category,
    string? Description
) : IRequest<ApplicationResult>;

public class UpdateNampDocumentHandler : IRequestHandler<UpdateNampDocumentCommand, ApplicationResult>
{
    private readonly INampApplicationRepository _repo;
    private readonly IUnitOfWork _uow;

    public UpdateNampDocumentHandler(INampApplicationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(UpdateNampDocumentCommand request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<NampDocumentCategory>(request.Category, ignoreCase: true, out var category))
            return ApplicationResult.Failure($"Unknown document category: '{request.Category}'.");

        var doc = await _repo.GetNampDocumentByIdAsync(request.DocumentId, ct);
        if (doc == null)
            return ApplicationResult.Failure("Document not found.");

        doc.UpdateMetadata(category, request.Description);
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
