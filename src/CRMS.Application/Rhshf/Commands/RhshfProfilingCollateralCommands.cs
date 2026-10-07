using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// FAC-side collateral capture during profiling — the FAC declares each instrument it is offering
/// (bank guarantee / NIRSAL CRG / legal mortgage). The staff-side RecordRhshfCollateralCommand is
/// restricted to UnderReview (the Legal Officer at Legal Clearance), so profiling needs its own entry
/// path. Filed under the profiling TARGET cycle so it lines up with the cycle the case is reviewed as;
/// perfection status is deliberately left for the Legal Officer to set.
/// </summary>
public record AddRhshfProfilingCollateralCommand(
    string Reference, RhshfCollateralType Type, string? ReferenceNumber, string? Notes,
    string? GuarantorBankName, decimal? GuaranteeAmount, bool? IsUnconditional,
    decimal? CrgCoveragePercentage,
    string? PropertyDescription, decimal? PropertyValue, string? TitleReferenceNumber) : IRequest<ApplicationResult>;

public class AddRhshfProfilingCollateralHandler : IRequestHandler<AddRhshfProfilingCollateralCommand, ApplicationResult>
{
    // No individual FAC user exists during profiling (token auth against the case); a fixed sentinel
    // satisfies the domain's "recordedBy required" rule and marks the record as FAC-originated.
    private static readonly Guid FacActor = new("0facfac0-0000-0000-0000-000000000000");

    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCollateralRepository _collateralRepo;
    private readonly IUnitOfWork _uow;

    public AddRhshfProfilingCollateralHandler(
        IRhshfCreditProfileRepository repo, IRhshfCollateralRepository collateralRepo, IUnitOfWork uow)
    {
        _repo = repo;
        _collateralRepo = collateralRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(AddRhshfProfilingCollateralCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");
        if (profile.Status is not (RhshfCaseStatus.ProfilingPending or RhshfCaseStatus.ProfilingInProgress))
            return ApplicationResult.Failure("Collateral can only be declared while profiling is in progress.");

        var result = RhshfCollateral.Create(
            profile.Id, profile.ProfilingTargetCycleNumber, request.Type, FacActor, request.Notes,
            request.ReferenceNumber, issuedDate: null, expiryDate: null,
            request.GuarantorBankName, request.GuaranteeAmount, request.IsUnconditional,
            request.CrgCoveragePercentage,
            request.PropertyDescription, request.PropertyValue, request.TitleReferenceNumber,
            registrationAuthority: null, perfectionStatus: null);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _collateralRepo.AddAsync(result.Value, ct);
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

public record RemoveRhshfProfilingCollateralCommand(string Reference, Guid CollateralId) : IRequest<ApplicationResult>;

public class RemoveRhshfProfilingCollateralHandler : IRequestHandler<RemoveRhshfProfilingCollateralCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCollateralRepository _collateralRepo;
    private readonly IUnitOfWork _uow;

    public RemoveRhshfProfilingCollateralHandler(
        IRhshfCreditProfileRepository repo, IRhshfCollateralRepository collateralRepo, IUnitOfWork uow)
    {
        _repo = repo;
        _collateralRepo = collateralRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(RemoveRhshfProfilingCollateralCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var collateral = await _collateralRepo.GetByIdAsync(request.CollateralId, ct);
        if (collateral is null || collateral.RhshfCreditProfileId != profile.Id)
            return ApplicationResult.Failure("Collateral not found.");

        _collateralRepo.Remove(collateral);
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
