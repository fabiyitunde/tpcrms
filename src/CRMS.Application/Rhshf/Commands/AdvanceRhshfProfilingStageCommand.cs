using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// The single "confirm and continue" action behind every one of the 5 profiling stages (§4) —
/// including the final ReviewAndSubmit, which completes profiling (external status → UnderReview).
/// ExpectedCurrentStage guards against skipping/replaying a stage via direct requests.
/// </summary>
public record AdvanceRhshfProfilingStageCommand(
    string Reference, RhshfProfilingStage ExpectedCurrentStage,
    string? IpAddress = null, string? UserAgent = null)
    : IRequest<ApplicationResult>;

public class AdvanceRhshfProfilingStageHandler : IRequestHandler<AdvanceRhshfProfilingStageCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfDocumentRequirementRepository _requirementRepo;
    private readonly IUnitOfWork _uow;

    public AdvanceRhshfProfilingStageHandler(
        IRhshfCreditProfileRepository repo, IRhshfDocumentRequirementRepository requirementRepo, IUnitOfWork uow)
    {
        _repo = repo;
        _requirementRepo = requirementRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(AdvanceRhshfProfilingStageCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        // Loaded for the two stages whose transition the domain gates on documents: leaving
        // SupportingDocuments, and final submit (ReviewAndSubmit) — the latter re-checks in case a
        // required document was removed after a back-navigation. Null elsewhere skips the check.
        var requirements = request.ExpectedCurrentStage is RhshfProfilingStage.SupportingDocuments
            or RhshfProfilingStage.ReviewAndSubmit
            ? await _requirementRepo.GetActiveAsync(ct)
            : null;

        var result = profile.AdvanceStage(request.ExpectedCurrentStage, requirements, request.IpAddress, request.UserAgent);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
