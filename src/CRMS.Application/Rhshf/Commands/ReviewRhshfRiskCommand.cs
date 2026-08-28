using CRMS.Application.Common;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>Risk Officer's review (design doc §3.6, Phase 4) — the distinct-actor check against
/// that cycle's Credit Officer happens inside RhshfCreditProfile.ReviewRisk, not here. A Cleared
/// outcome automatically circulates the case to committee (Phase 5) — creates the
/// RhshfCommitteeReview for this cycle right away, no separate manual "circulate" step.</summary>
public record ReviewRhshfRiskCommand(
    string Reference, Guid RiskOfficerId, RhshfRiskReviewOutcome Outcome, string? Notes, RhshfProfilingStage? ReturnToStage)
    : IRequest<ApplicationResult>;

public class ReviewRhshfRiskHandler : IRequestHandler<ReviewRhshfRiskCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCommitteeReviewRepository _committeeRepo;
    private readonly IRhshfCommitteeConfig _committeeConfig;
    private readonly IUnitOfWork _uow;

    public ReviewRhshfRiskHandler(
        IRhshfCreditProfileRepository repo,
        IRhshfCommitteeReviewRepository committeeRepo,
        IRhshfCommitteeConfig committeeConfig,
        IUnitOfWork uow)
    {
        _repo = repo;
        _committeeRepo = committeeRepo;
        _committeeConfig = committeeConfig;
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
            var committeeResult = RhshfCommitteeReview.Create(
                profile.Id, cycleNumber, _committeeConfig.RequiredVotes, _committeeConfig.MinimumApprovalVotes);
            if (committeeResult.IsFailure)
                return ApplicationResult.Failure(committeeResult.Error);

            await _committeeRepo.AddAsync(committeeResult.Value, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
