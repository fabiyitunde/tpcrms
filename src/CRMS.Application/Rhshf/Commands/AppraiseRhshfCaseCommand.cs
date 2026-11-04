using CRMS.Application.Common;
using CRMS.Application.Rhshf.Services;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>Credit Officer's appraisal (design doc §3.6, Phase 4). Role check (who may call this)
/// happens at the caller — see docs/rhshf resources/ Phase 4 for the confirmed role mapping.</summary>
public record AppraiseRhshfCaseCommand(
    string Reference, Guid CreditOfficerId, RhshfAppraisalOutcome Outcome, string? Notes, RhshfProfilingStage? ReturnToStage,
    /// <summary>The reason picked for a return or a decline (CRMSQA#1); OTHER when none is given.</summary>
    string? ReasonCode = null)
    : IRequest<ApplicationResult>;

public class AppraiseRhshfCaseHandler : IRequestHandler<AppraiseRhshfCaseCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IRhshfReasonCodeRepository? _reasonCodes;

    public AppraiseRhshfCaseHandler(IRhshfCreditProfileRepository repo, IUnitOfWork uow,
        IRhshfReasonCodeRepository? reasonCodes = null)
    {
        _repo = repo;
        _uow = uow;
        _reasonCodes = reasonCodes;
    }

    public async Task<ApplicationResult> Handle(AppraiseRhshfCaseCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        string? reasonCode = null;
        if (request.Outcome is RhshfAppraisalOutcome.ReturnToFac or RhshfAppraisalOutcome.Decline)
        {
            var reason = await RhshfReasonCodeRules.ResolveAsync(_reasonCodes, request.ReasonCode,
                request.Outcome == RhshfAppraisalOutcome.Decline ? RhshfReasonCodeType.Decline : RhshfReasonCodeType.Correction, ct);
            if (reason.IsFailure)
                return ApplicationResult.Failure(reason.Error);
            reasonCode = reason.Value;
        }

        var result = profile.Appraise(request.CreditOfficerId, request.Outcome, request.Notes, request.ReturnToStage, reasonCode);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
