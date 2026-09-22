using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>Disbursement Officer clearing the Pre-Deployment gate — advances to Disbursement.</summary>
public record CompleteRhshfPreDeploymentVerificationCommand(string Reference, Guid UserId, string? Note)
    : IRequest<ApplicationResult>;

public class CompleteRhshfPreDeploymentVerificationHandler : IRequestHandler<CompleteRhshfPreDeploymentVerificationCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUnitOfWork _uow;

    public CompleteRhshfPreDeploymentVerificationHandler(IRhshfCreditProfileRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(CompleteRhshfPreDeploymentVerificationCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var result = profile.CompletePreDeploymentVerification(request.UserId, request.Note);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
