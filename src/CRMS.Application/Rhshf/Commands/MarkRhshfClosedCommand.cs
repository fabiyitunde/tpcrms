using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>Disbursement Officer's manual attestation that the loan is fully repaid — mirrors NAMP's
/// trivial Close() command, no Fineract call.</summary>
public record MarkRhshfClosedCommand(string Reference, Guid UserId) : IRequest<ApplicationResult>;

public class MarkRhshfClosedHandler : IRequestHandler<MarkRhshfClosedCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUnitOfWork _uow;

    public MarkRhshfClosedHandler(IRhshfCreditProfileRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(MarkRhshfClosedCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var result = profile.MarkClosed(request.UserId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
