using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>Records one collateral instrument (S/N 16 — Bank Guarantee, NIRSAL CRG, or Legal
/// Mortgage) for the case's current cycle — recorded by the Legal Officer as part of Legal
/// Clearance. A case may carry more than one instrument, so this can be called repeatedly.</summary>
public record RecordRhshfCollateralCommand(
    string Reference, Guid RecordedBy, RhshfCollateralType Type, string? Notes,
    string? ReferenceNumber, DateTime? IssuedDate, DateTime? ExpiryDate,
    string? GuarantorBankName, decimal? GuaranteeAmount, bool? IsUnconditional,
    decimal? CrgCoveragePercentage,
    string? PropertyDescription, decimal? PropertyValue, string? TitleReferenceNumber,
    string? RegistrationAuthority, RhshfCollateralPerfectionStatus? PerfectionStatus) : IRequest<ApplicationResult>;

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
            request.RegistrationAuthority, request.PerfectionStatus);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _collateralRepo.AddAsync(result.Value, ct);
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
