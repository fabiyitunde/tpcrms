using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// The single "confirm and continue" action behind every one of the 5 profiling stages (§4) —
/// including the final ReviewAndSubmit, which completes profiling (external status → UnderReview).
/// ExpectedCurrentStage guards against skipping/replaying a stage via direct requests.
/// </summary>
public record AdvanceRhshfProfilingStageCommand(string Reference, RhshfProfilingStage ExpectedCurrentStage)
    : IRequest<ApplicationResult>;

public class AdvanceRhshfProfilingStageHandler : IRequestHandler<AdvanceRhshfProfilingStageCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUnitOfWork _uow;

    public AdvanceRhshfProfilingStageHandler(IRhshfCreditProfileRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(AdvanceRhshfProfilingStageCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var result = profile.AdvanceStage(request.ExpectedCurrentStage);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
