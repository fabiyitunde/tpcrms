using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Backward/forward navigation to an already-reached profiling stage so the FAC can revisit and
/// correct a record. Unlike AdvanceStage this attests nothing and runs no gate; the domain's
/// GoToStage enforces that the target is within the high-water mark, so a direct POST cannot use it
/// to skip ahead past a stage the FAC has not legitimately reached.
/// </summary>
public record GoToRhshfProfilingStageCommand(string Reference, RhshfProfilingStage TargetStage)
    : IRequest<ApplicationResult>;

public class GoToRhshfProfilingStageHandler : IRequestHandler<GoToRhshfProfilingStageCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUnitOfWork _uow;

    public GoToRhshfProfilingStageHandler(IRhshfCreditProfileRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(GoToRhshfProfilingStageCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var result = profile.GoToStage(request.TargetStage);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
