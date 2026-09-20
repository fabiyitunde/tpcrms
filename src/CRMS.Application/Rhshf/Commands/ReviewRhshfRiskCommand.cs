using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>Risk Officer's review (design doc §3.6, Phase 4) — the distinct-actor check against
/// that cycle's Credit Officer happens inside RhshfCreditProfile.ReviewRisk, not here. A Cleared
/// outcome automatically circulates the case to committee (Phase 5) — resolves the committee tier
/// from TotalEopValue (RhshfRoutingConfig, superseding the original v1 flat-committee design),
/// resolves that tier's roster/quorum from the shared StandingCommittee module (the same one NAMP
/// uses — RH-SHF's tiered committees deliberately share NAMP's exact rosters, not separate ones),
/// and creates the RhshfCommitteeReview for this cycle right away, no separate manual "circulate"
/// step.</summary>
public record ReviewRhshfRiskCommand(
    string Reference, Guid RiskOfficerId, RhshfRiskReviewOutcome Outcome, string? Notes, RhshfProfilingStage? ReturnToStage)
    : IRequest<ApplicationResult>;

public class ReviewRhshfRiskHandler : IRequestHandler<ReviewRhshfRiskCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCommitteeReviewRepository _committeeRepo;
    private readonly IRhshfRoutingConfigRepository _routingRepo;
    private readonly IStandingCommitteeRepository _standingCommitteeRepo;
    private readonly IUnitOfWork _uow;

    public ReviewRhshfRiskHandler(
        IRhshfCreditProfileRepository repo,
        IRhshfCommitteeReviewRepository committeeRepo,
        IRhshfRoutingConfigRepository routingRepo,
        IStandingCommitteeRepository standingCommitteeRepo,
        IUnitOfWork uow)
    {
        _repo = repo;
        _committeeRepo = committeeRepo;
        _routingRepo = routingRepo;
        _standingCommitteeRepo = standingCommitteeRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(ReviewRhshfRiskCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var cycleNumber = profile.CurrentCycleNumber;
        var result = profile.ReviewRisk(request.RiskOfficerId, request.Outcome, request.Notes, request.ReturnToStage);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        if (request.Outcome == RhshfRiskReviewOutcome.Cleared)
        {
            var routingConfig = await _routingRepo.ResolveAsync(profile.TotalEopValue, ct);
            if (routingConfig is null)
                return ApplicationResult.Failure(
                    $"No active routing config found for EOP value {profile.TotalEopValue:N2}. Please configure routing rules in Admin > RH-SHF Routing Config.");

            var standingCommittee = await _standingCommitteeRepo.GetByCommitteeTypeAndLocationAsync(
                routingConfig.Tier, profile.ResolvedBranchId, ct);
            if (standingCommittee is null)
                return ApplicationResult.Failure(
                    $"No active standing committee configured for the {routingConfig.Tier} tier. Please configure it in Admin > Committees first.");

            var committeeResult = RhshfCommitteeReview.Create(
                profile.Id, cycleNumber, standingCommittee.RequiredVotes, standingCommittee.MinimumApprovalVotes,
                routingConfig.Tier, profile.ResolvedBranchId);
            if (committeeResult.IsFailure)
                return ApplicationResult.Failure(committeeResult.Error);

            await _committeeRepo.AddAsync(committeeResult.Value, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
