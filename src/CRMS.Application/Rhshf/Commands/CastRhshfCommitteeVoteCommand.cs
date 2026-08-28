using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>Committee member's vote (design doc §3.6, Phase 5). Propagates the tally result to the
/// RhshfCreditProfile the moment quorum is reached — both aggregates save in one transaction since
/// they share the same DbContext instance for this request.</summary>
public record CastRhshfCommitteeVoteCommand(string Reference, Guid UserId, RhshfCommitteeVoteChoice Vote, string? Comment)
    : IRequest<ApplicationResult>;

public class CastRhshfCommitteeVoteHandler : IRequestHandler<CastRhshfCommitteeVoteCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCommitteeReviewRepository _committeeRepo;
    private readonly IUnitOfWork _uow;

    public CastRhshfCommitteeVoteHandler(
        IRhshfCreditProfileRepository repo, IRhshfCommitteeReviewRepository committeeRepo, IUnitOfWork uow)
    {
        _repo = repo;
        _committeeRepo = committeeRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(CastRhshfCommitteeVoteCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");
        if (profile.InternalStage != RhshfInternalStage.CommitteeVoting)
            return ApplicationResult.Failure("Case is not at the committee voting stage.");

        var review = await _committeeRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        if (review is null)
            return ApplicationResult.Failure("No committee review found for this case's current cycle.");

        var excludedActorIds = profile.GetCurrentCycleAppraisalAndRiskActorIds();
        var voteResult = review.CastVote(request.UserId, request.Vote, request.Comment, excludedActorIds);
        if (voteResult.IsFailure)
            return ApplicationResult.Failure(voteResult.Error);

        if (review.IsDecided)
        {
            var propagateResult = review.FinalDecision switch
            {
                RhshfCommitteeDecision.Approved => profile.AdvanceToRatification(),
                RhshfCommitteeDecision.Rejected => profile.DeclineAtCommittee(
                    $"Committee vote: {review.Votes.Count(v => v.Vote == RhshfCommitteeVoteChoice.Approve)}/{review.Votes.Count} approved."),
                _ => Domain.Common.Result.Failure($"Unexpected committee decision '{review.FinalDecision}' from a vote tally."),
            };

            if (propagateResult.IsFailure)
                return ApplicationResult.Failure(propagateResult.Error);
        }

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
