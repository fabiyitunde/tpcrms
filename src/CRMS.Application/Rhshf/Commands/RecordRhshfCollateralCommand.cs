using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Records one collateral instrument (S/N 16 — Bank Guarantee, NIRSAL CRG, or Legal Mortgage) for the
/// case's current cycle.
/// NO UI PATH as of 2026-10-11, deliberately: the FAC declares its own security on the profiling form
/// (AddRhshfProfilingCollateralCommand) and staff verify it — they do not enter it on the applicant's
/// behalf. An instrument the FAC never declared is a Return to FAC. Kept for an admin/API caller, but
/// do not wire it back into the staff UI without revisiting that decision.
/// </summary>
public record RecordRhshfCollateralCommand(
    string Reference, Guid RecordedBy, RhshfCollateralType Type, string? Notes,
    string? ReferenceNumber, DateTime? IssuedDate, DateTime? ExpiryDate,
    string? GuarantorBankName, decimal? GuaranteeAmount, bool? IsUnconditional,
    decimal? CrgCoveragePercentage,
    string? PropertyDescription, decimal? PropertyValue, string? TitleReferenceNumber,
    string? RegistrationAuthority) : IRequest<ApplicationResult>;

public class RecordRhshfCollateralHandler : IRequestHandler<RecordRhshfCollateralCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCollateralRepository _collateralRepo;
    private readonly IUnitOfWork _uow;

    public RecordRhshfCollateralHandler(IRhshfCreditProfileRepository repo, IRhshfCollateralRepository collateralRepo, IUnitOfWork uow)
    {
        _repo = repo;
        _collateralRepo = collateralRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(RecordRhshfCollateralCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");
        if (profile.Status != RhshfCaseStatus.UnderReview)
            return ApplicationResult.Failure("Case is not under review.");

        var result = RhshfCollateral.Create(
            profile.Id, profile.CurrentCycleNumber, request.Type, request.RecordedBy, request.Notes,
            request.ReferenceNumber, request.IssuedDate, request.ExpiryDate,
            request.GuarantorBankName, request.GuaranteeAmount, request.IsUnconditional,
            request.CrgCoveragePercentage,
            request.PropertyDescription, request.PropertyValue, request.TitleReferenceNumber,
            request.RegistrationAuthority);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _collateralRepo.AddAsync(result.Value, ct);
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
