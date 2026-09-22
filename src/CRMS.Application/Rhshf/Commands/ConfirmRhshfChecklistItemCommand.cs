using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>Disbursement Officer confirming (or unconfirming) one Pre-Deployment gate item.</summary>
public record ConfirmRhshfChecklistItemCommand(string Reference, Guid ItemId, Guid UserId, bool? IsConfirmed, string? Notes)
    : IRequest<ApplicationResult>;

public class ConfirmRhshfChecklistItemHandler : IRequestHandler<ConfirmRhshfChecklistItemCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUnitOfWork _uow;

    public ConfirmRhshfChecklistItemHandler(IRhshfCreditProfileRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(ConfirmRhshfChecklistItemCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var result = profile.ConfirmPreDeploymentChecklistItem(request.ItemId, request.UserId, request.IsConfirmed, request.Notes);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
