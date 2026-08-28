using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>Credit Officer's appraisal (design doc §3.6, Phase 4). Role check (who may call this)
/// happens at the caller — see docs/rhshf resources/ Phase 4 for the confirmed role mapping.</summary>
public record AppraiseRhshfCaseCommand(
    string Reference, Guid CreditOfficerId, RhshfAppraisalOutcome Outcome, string? Notes, RhshfProfilingStage? ReturnToStage)
    : IRequest<ApplicationResult>;

public class AppraiseRhshfCaseHandler : IRequestHandler<AppraiseRhshfCaseCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUnitOfWork _uow;

    public AppraiseRhshfCaseHandler(IRhshfCreditProfileRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(AppraiseRhshfCaseCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var result = profile.Appraise(request.CreditOfficerId, request.Outcome, request.Notes, request.ReturnToStage);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
