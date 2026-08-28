using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>An explicit chair-level action (not a vote) for sending the case back to the FAC before
/// committee reaches a tally — design doc §3.6, Phase 5.</summary>
public record ReturnRhshfCommitteeToFacCommand(string Reference, string? Notes, RhshfProfilingStage? ReturnToStage)
    : IRequest<ApplicationResult>;

public class ReturnRhshfCommitteeToFacHandler : IRequestHandler<ReturnRhshfCommitteeToFacCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCommitteeReviewRepository _committeeRepo;
    private readonly IUnitOfWork _uow;

    public ReturnRhshfCommitteeToFacHandler(
        IRhshfCreditProfileRepository repo, IRhshfCommitteeReviewRepository committeeRepo, IUnitOfWork uow)
    {
        _repo = repo;
        _committeeRepo = committeeRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(ReturnRhshfCommitteeToFacCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");
        if (profile.InternalStage != RhshfInternalStage.CommitteeVoting)
            return ApplicationResult.Failure("Case is not at the committee voting stage.");

        var review = await _committeeRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        if (review is null)
            return ApplicationResult.Failure("No committee review found for this case's current cycle.");

        var reviewResult = review.ReturnToFac(request.Notes);
        if (reviewResult.IsFailure)
            return ApplicationResult.Failure(reviewResult.Error);

        var profileResult = profile.ReturnToFacFromCommittee(request.ReturnToStage);
        if (profileResult.IsFailure)
            return ApplicationResult.Failure(profileResult.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
