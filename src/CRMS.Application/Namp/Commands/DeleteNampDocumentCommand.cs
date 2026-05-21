using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Namp.Commands;

public record DeleteNampDocumentCommand(
    Guid NampApplicationId,
    Guid DocumentId,
    Guid UserId
) : IRequest<ApplicationResult>;

public class DeleteNampDocumentHandler : IRequestHandler<DeleteNampDocumentCommand, ApplicationResult>
{
    private readonly INampApplicationRepository _repo;
    private readonly IUnitOfWork _uow;

    public DeleteNampDocumentHandler(INampApplicationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(DeleteNampDocumentCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app == null)
            return ApplicationResult.Failure("Application not found.");

        var result = app.RemoveDocument(request.DocumentId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
