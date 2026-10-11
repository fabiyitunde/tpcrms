using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// The Legal Officer's perfection assertion on one declared instrument, made at legal clearance —
/// which now runs before the committee votes, so this is what the committee's "is the security
/// actually in place?" question reads. Nobody can assert it at declaration time: the status left
/// RhshfCollateral.Create entirely, and only this command (stage-gated to LegalClearance) sets it.
/// A title reference and registration authority established during perfection can be recorded with
/// the assertion, since Legal is usually the first to know them.
/// </summary>
public record AssertRhshfCollateralPerfectionCommand(
    string Reference, Guid CollateralId, Guid LegalOfficerId, RhshfCollateralPerfectionStatus Status,
    string? TitleReferenceNumber = null, string? RegistrationAuthority = null) : IRequest<ApplicationResult>;

public class AssertRhshfCollateralPerfectionHandler : IRequestHandler<AssertRhshfCollateralPerfectionCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCollateralRepository _collateralRepo;
    private readonly IUnitOfWork _uow;

    public AssertRhshfCollateralPerfectionHandler(
        IRhshfCreditProfileRepository repo, IRhshfCollateralRepository collateralRepo, IUnitOfWork uow)
    {
        _repo = repo;
        _collateralRepo = collateralRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(AssertRhshfCollateralPerfectionCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        // Perfection is settled at legal clearance, before circulation — not re-opened afterwards,
        // which would change the basis the committee voted on.
        if (profile.Status != RhshfCaseStatus.UnderReview || profile.InternalStage != RhshfInternalStage.LegalClearance)
            return ApplicationResult.Failure("Perfection can only be recorded while the case is at legal clearance.");

        var collateral = await _collateralRepo.GetByIdAsync(request.CollateralId, ct);
        if (collateral is null || collateral.RhshfCreditProfileId != profile.Id)
            return ApplicationResult.Failure("Security instrument not found on this case.");
        if (collateral.CycleNumber != profile.CurrentCycleNumber)
            return ApplicationResult.Failure("That instrument belongs to an earlier review cycle.");

        var result = collateral.AssertPerfection(
            request.LegalOfficerId, request.Status, request.TitleReferenceNumber, request.RegistrationAuthority);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
