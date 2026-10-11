using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>Committee member's vote (design doc §3.6, Phase 5). Propagates the tally result to the
/// RhshfCreditProfile the moment quorum is reached — both aggregates save in one transaction since
/// they share the same DbContext instance for this request.
/// PerfectionDisposition is required on an approving vote when the Legal Officer did not perfect the
/// security: the voter says whether perfection must still happen before disbursement (Deferred) or
/// is being waived.</summary>
public record CastRhshfCommitteeVoteCommand(
    string Reference, Guid UserId, RhshfCommitteeVoteChoice Vote, string? Comment,
    RhshfPerfectionDisposition? PerfectionDisposition = null)
    : IRequest<ApplicationResult>;

public class CastRhshfCommitteeVoteHandler : IRequestHandler<CastRhshfCommitteeVoteCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCommitteeReviewRepository _committeeRepo;
    private readonly IStandingCommitteeRepository _standingCommitteeRepo;
    private readonly IRhshfLegalClearanceRepository _legalRepo;
    private readonly IRhshfCollateralRepository _collateralRepo;
    private readonly IUnitOfWork _uow;

    public CastRhshfCommitteeVoteHandler(
        IRhshfCreditProfileRepository repo, IRhshfCommitteeReviewRepository committeeRepo,
        IStandingCommitteeRepository standingCommitteeRepo, IRhshfLegalClearanceRepository legalRepo,
        IRhshfCollateralRepository collateralRepo, IUnitOfWork uow)
    {
        _repo = repo;
        _committeeRepo = committeeRepo;
        _standingCommitteeRepo = standingCommitteeRepo;
        _legalRepo = legalRepo;
        _collateralRepo = collateralRepo;
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

        // Roster is resolved live (not snapshotted on the review) — same tier+branch lookup used to
        // size the review at creation time, shared with NAMP's StandingCommittee module.
        var standingCommittee = await _standingCommitteeRepo.GetByCommitteeTypeAndLocationAsync(review.Tier, review.BranchId, ct);
        if (standingCommittee is null || standingCommittee.Members.All(m => m.UserId != request.UserId))
            return ApplicationResult.Failure("You are not a member of the committee this case is routed to.");

        // Appraiser + risk officer come off the profile; the Legal Officer who cleared the security
        // before circulation is in a separate aggregate and is excluded too — a voter must not be
        // reviewing their own work on any of the three internal stages.
        var excludedActorIds = profile.GetCurrentCycleAppraisalAndRiskActorIds().ToList();
        var clearances = await _legalRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        excludedActorIds.AddRange(clearances.Select(c => c.LegalOfficerId));

        // Whether the committee has to answer the perfection question at all: Legal either perfected
        // everything before circulating, or forwarded with a caveat. Collateral is a separate
        // aggregate, so the state is resolved here and handed to the review.
        var collateral = await _collateralRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        var perfectionRequired = collateral.Any(c => c.PerfectionStatus != RhshfCollateralPerfectionStatus.Perfected);

        var voteResult = review.CastVote(
            request.UserId, request.Vote, request.Comment, excludedActorIds,
            perfectionRequired, request.PerfectionDisposition);
        if (voteResult.IsFailure)
            return ApplicationResult.Failure(voteResult.Error);

        if (review.IsDecided)
        {
            var propagateResult = review.FinalDecision switch
            {
                RhshfCommitteeDecision.Approved => profile.AdvanceToRatification(request.UserId,
                    $"Committee vote: {review.Votes.Count(v => v.Vote == RhshfCommitteeVoteChoice.Approve)}/{review.Votes.Count} approved."),
                RhshfCommitteeDecision.Rejected => profile.DeclineAtCommittee(
                    $"Committee vote: {review.Votes.Count(v => v.Vote == RhshfCommitteeVoteChoice.Approve)}/{review.Votes.Count} approved.",
                    request.UserId),
                _ => Domain.Common.Result.Failure($"Unexpected committee decision '{review.FinalDecision}' from a vote tally."),
            };

            if (propagateResult.IsFailure)
                return ApplicationResult.Failure(propagateResult.Error);
        }

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
