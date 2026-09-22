using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Legal Officer's clearance (design doc §3.6, Phase 8) — fifth stage of the post-profiling
/// pipeline. Granted advances to Disbursement (Phase 9); Returned routes back to Ratification for
/// the same cycle (design doc §6 #12); Declined is terminal.
/// </summary>
public record ClearRhshfLegalCommand(
    string Reference, Guid LegalOfficerId, RhshfLegalClearanceOutcome Outcome, string? Comments) : IRequest<ApplicationResult>;

public class ClearRhshfLegalHandler : IRequestHandler<ClearRhshfLegalCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfLegalClearanceRepository _legalRepo;
    private readonly IRhshfPreDeploymentChecklistTemplateRepository _templateRepo;
    private readonly IUnitOfWork _uow;

    public ClearRhshfLegalHandler(
        IRhshfCreditProfileRepository repo, IRhshfLegalClearanceRepository legalRepo,
        IRhshfPreDeploymentChecklistTemplateRepository templateRepo, IUnitOfWork uow)
    {
        _repo = repo;
        _legalRepo = legalRepo;
        _templateRepo = templateRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(ClearRhshfLegalCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var finalApproverId = profile.GetCurrentCycleFinalApproverId();
        if (finalApproverId is null)
            return ApplicationResult.Failure("This cycle has not been ratified yet.");

        var clearanceResult = RhshfLegalClearance.Create(
            profile.Id, profile.CurrentCycleNumber, request.LegalOfficerId, finalApproverId.Value, request.Outcome, request.Comments);
        if (clearanceResult.IsFailure)
            return ApplicationResult.Failure(clearanceResult.Error);

        var transitionResult = request.Outcome switch
        {
            RhshfLegalClearanceOutcome.Granted => profile.AdvanceToDisbursement(request.LegalOfficerId, request.Comments),
            RhshfLegalClearanceOutcome.Returned => profile.ReturnToRatificationFromLegal(request.LegalOfficerId, request.Comments),
            RhshfLegalClearanceOutcome.Declined => profile.DeclineAtLegalClearance(request.Comments, request.LegalOfficerId),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Outcome)),
        };
        if (transitionResult.IsFailure)
            return ApplicationResult.Failure(transitionResult.Error);

        // Granted lands the case at PreDeploymentVerification (not straight at Disbursement) — seed
        // the gate checklist from the active templates right away, same moment NAMP seeds its own.
        if (request.Outcome == RhshfLegalClearanceOutcome.Granted)
        {
            var templates = await _templateRepo.GetActiveAsync(ct);
            profile.SeedPreDeploymentChecklist(templates);
        }

        await _legalRepo.AddAsync(clearanceResult.Value, ct);
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
