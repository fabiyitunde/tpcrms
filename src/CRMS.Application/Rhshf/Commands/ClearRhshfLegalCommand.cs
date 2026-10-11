using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Legal Officer's clearance — the second internal stage, between Appraisal and Risk Review, so the
/// security is settled before the committee votes. Granted advances to Risk Review; Returned sends the
/// case back to the FAC to fix the security it declared (ReturnToStage picks where it lands);
/// Declined is terminal, for defects no correction fixes (falsification and the like).
/// </summary>
public record ClearRhshfLegalCommand(
    string Reference, Guid LegalOfficerId, RhshfLegalClearanceOutcome Outcome, string? Comments,
    RhshfProfilingStage? ReturnToStage = null) : IRequest<ApplicationResult>;

public class ClearRhshfLegalHandler : IRequestHandler<ClearRhshfLegalCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfLegalClearanceRepository _legalRepo;
    private readonly IUnitOfWork _uow;

    public ClearRhshfLegalHandler(
        IRhshfCreditProfileRepository repo, IRhshfLegalClearanceRepository legalRepo, IUnitOfWork uow)
    {
        _repo = repo;
        _legalRepo = legalRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(ClearRhshfLegalCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var clearanceResult = RhshfLegalClearance.Create(
            profile.Id, profile.CurrentCycleNumber, request.LegalOfficerId, request.Outcome, request.Comments);
        if (clearanceResult.IsFailure)
            return ApplicationResult.Failure(clearanceResult.Error);

        var transitionResult = request.Outcome switch
        {
            // Both forward outcomes go to Risk. GrantedWithCaveat differs only in what it says: the
            // defect rides on this clearance record, Risk reads it, and the committee then has to
            // answer for it explicitly when it votes (defer perfection, or waive it).
            RhshfLegalClearanceOutcome.Granted or RhshfLegalClearanceOutcome.GrantedWithCaveat
                => profile.AdvanceToRiskReviewFromLegal(request.LegalOfficerId, request.Comments),
            RhshfLegalClearanceOutcome.Returned => profile.ReturnToFacFromLegal(request.ReturnToStage, request.LegalOfficerId, request.Comments),
            RhshfLegalClearanceOutcome.Declined => profile.DeclineAtLegalClearance(request.Comments, request.LegalOfficerId),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Outcome)),
        };
        if (transitionResult.IsFailure)
            return ApplicationResult.Failure(transitionResult.Error);

        await _legalRepo.AddAsync(clearanceResult.Value, ct);
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
