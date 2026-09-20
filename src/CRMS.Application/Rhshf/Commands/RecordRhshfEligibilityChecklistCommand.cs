using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>Records the RH-SHF programme's fixed 11-item eligibility checklist (S/N 15) for the
/// case's current cycle, all in one submission — recorded by the Credit Officer alongside Appraisal.
/// No hard gate on Appraise() itself in this build; the two are recorded independently.</summary>
public record RhshfEligibilityCriterionInput(RhshfEligibilityCriterion Criterion, bool IsSatisfied, string? Notes);

public record RecordRhshfEligibilityChecklistCommand(
    string Reference, Guid VerifiedBy, List<RhshfEligibilityCriterionInput> Criteria) : IRequest<ApplicationResult>;

public class RecordRhshfEligibilityChecklistHandler : IRequestHandler<RecordRhshfEligibilityChecklistCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfEligibilityCheckRepository _eligibilityRepo;
    private readonly IUnitOfWork _uow;

    public RecordRhshfEligibilityChecklistHandler(
        IRhshfCreditProfileRepository repo, IRhshfEligibilityCheckRepository eligibilityRepo, IUnitOfWork uow)
    {
        _repo = repo;
        _eligibilityRepo = eligibilityRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(RecordRhshfEligibilityChecklistCommand request, CancellationToken ct = default)
    {
        if (request.Criteria.Count == 0)
            return ApplicationResult.Failure("At least one criterion is required.");

        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");
        if (profile.Status != RhshfCaseStatus.UnderReview)
            return ApplicationResult.Failure("Case is not under review.");

        var existing = await _eligibilityRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        if (existing.Count > 0)
            return ApplicationResult.Failure("The eligibility checklist has already been recorded for this cycle.");

        var checks = new List<RhshfEligibilityCheck>();
        foreach (var input in request.Criteria)
        {
            var result = RhshfEligibilityCheck.Create(
                profile.Id, profile.CurrentCycleNumber, input.Criterion, input.IsSatisfied, input.Notes, request.VerifiedBy);
            if (result.IsFailure)
                return ApplicationResult.Failure(result.Error);
            checks.Add(result.Value);
        }

        foreach (var check in checks)
            await _eligibilityRepo.AddAsync(check, ct);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
