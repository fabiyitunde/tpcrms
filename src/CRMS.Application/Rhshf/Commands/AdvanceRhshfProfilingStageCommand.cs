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
    private readonly IRhshfGuarantorRepository _guarantorRepo;
    private readonly IRhshfCollateralRepository _collateralRepo;
    private readonly IUnitOfWork _uow;

    public AdvanceRhshfProfilingStageHandler(
        IRhshfCreditProfileRepository repo, IRhshfDocumentRequirementRepository requirementRepo,
        IRhshfGuarantorRepository guarantorRepo, IRhshfCollateralRepository collateralRepo, IUnitOfWork uow)
    {
        _repo = repo;
        _requirementRepo = requirementRepo;
        _guarantorRepo = guarantorRepo;
        _collateralRepo = collateralRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(AdvanceRhshfProfilingStageCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        // Individual guarantors are credit-checked by BVN (companies by RC), so at submit every
        // individual guarantor must carry one — the guarantor counterpart to the director-BVN gate
        // in RhshfCreditProfile.AdvanceStage. Enforced here because guarantors are a separate
        // aggregate the profile doesn't own; checked before AdvanceStage so nothing is persisted.
        if (request.ExpectedCurrentStage == RhshfProfilingStage.ReviewAndSubmit)
        {
            var guarantors = await _guarantorRepo.GetByProfileIdAsync(profile.Id, ct);
            if (guarantors.Any(g => g.GuarantorType == RhshfGuarantorType.Individual && string.IsNullOrWhiteSpace(g.Bvn)))
                return ApplicationResult.Failure("Every individual guarantor must have a BVN before submitting — the bank runs a credit check on each one.");

            // Security is now settled before the committee votes, which only works if there is
            // something to settle: the Legal Officer sights, opines on and perfects what the FAC
            // declared here, between appraisal and risk review. A case with no instrument declared
            // would reach that stage with nothing to clear. Collateral is its own aggregate, so this
            // is checked here rather than in AdvanceStage — same as the guarantor gate above.
            var collateral = await _collateralRepo.GetByProfileAndCycleAsync(profile.Id, profile.ProfilingTargetCycleNumber, ct);
            if (collateral.Count == 0)
                return ApplicationResult.Failure(
                    "At least one security instrument must be declared before submitting — the Bank's Legal Officer verifies and perfects it before the case goes for approval.");

            // And each one needs its evidence attached: the Legal Officer sights the guarantee letter,
            // CRG certificate or title deed itself. An instrument with no document would bounce straight
            // back from legal clearance and cost the FAC a whole cycle.
            var undocumented = collateral.Where(c => c.Documents.Count == 0).ToList();
            if (undocumented.Count > 0)
                return ApplicationResult.Failure(
                    $"Attach the supporting document to every security instrument before submitting — {undocumented.Count} of {collateral.Count} " +
                    $"{(undocumented.Count == 1 ? "has" : "have")} none (guarantee letter, CRG certificate or title deed).");
        }

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
