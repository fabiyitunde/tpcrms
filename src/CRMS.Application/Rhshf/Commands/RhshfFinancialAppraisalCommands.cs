using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

// ── Farm plans ─────────────────────────────────────────────────────────────

public record AddRhshfFarmPlanCommand(
    string Reference, string Crop, decimal Hectares, decimal ExpectedYieldKgPerHectare,
    decimal ExpectedPricePerKg, Guid UserId) : IRequest<ApplicationResult>;

public class AddRhshfFarmPlanHandler : IRequestHandler<AddRhshfFarmPlanCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUnitOfWork _uow;

    public AddRhshfFarmPlanHandler(IRhshfCreditProfileRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(AddRhshfFarmPlanCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var planResult = RhshfFarmPlan.Create(
            profile.Id, profile.CurrentCycleNumber, request.Crop,
            request.Hectares, request.ExpectedYieldKgPerHectare, request.ExpectedPricePerKg, request.UserId);
        if (planResult.IsFailure)
            return ApplicationResult.Failure(planResult.Error);

        var addResult = profile.AddFarmPlan(planResult.Value);
        if (addResult.IsFailure)
            return ApplicationResult.Failure(addResult.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

public record UpdateRhshfFarmPlanCommand(
    string Reference, Guid PlanId, decimal Hectares, decimal ExpectedYieldKgPerHectare,
    decimal ExpectedPricePerKg, Guid UserId) : IRequest<ApplicationResult>;

public class UpdateRhshfFarmPlanHandler : IRequestHandler<UpdateRhshfFarmPlanCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUnitOfWork _uow;

    public UpdateRhshfFarmPlanHandler(IRhshfCreditProfileRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(UpdateRhshfFarmPlanCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var result = profile.UpdateFarmPlan(
            request.PlanId, request.Hectares, request.ExpectedYieldKgPerHectare, request.ExpectedPricePerKg);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

public record RemoveRhshfFarmPlanCommand(string Reference, Guid PlanId, Guid UserId) : IRequest<ApplicationResult>;

public class RemoveRhshfFarmPlanHandler : IRequestHandler<RemoveRhshfFarmPlanCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUnitOfWork _uow;

    public RemoveRhshfFarmPlanHandler(IRhshfCreditProfileRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(RemoveRhshfFarmPlanCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var result = profile.RemoveFarmPlan(request.PlanId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

// ── Financial appraisal ────────────────────────────────────────────────────

/// <summary>Saves (or re-saves) the cycle's financial appraisal. Every computed figure is derived
/// inside the domain from these inputs — the caller supplies assumptions only.</summary>
public record SaveRhshfFinancialAppraisalCommand(
    string Reference, Guid UserId,
    decimal OwnProductionCost, decimal HarvestAndLogisticsCost, int CycleMonths, decimal InterestRatePercent,
    string? AssumptionBasisNote, string Recommendation, string? SummaryNotes, string? OverrideJustification)
    : IRequest<ApplicationResult>;

public class SaveRhshfFinancialAppraisalHandler : IRequestHandler<SaveRhshfFinancialAppraisalCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfAppraisalThresholdsRepository _thresholdsRepo;
    private readonly IUnitOfWork _uow;

    public SaveRhshfFinancialAppraisalHandler(
        IRhshfCreditProfileRepository repo, IRhshfAppraisalThresholdsRepository thresholdsRepo, IUnitOfWork uow)
    {
        _repo = repo;
        _thresholdsRepo = thresholdsRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(SaveRhshfFinancialAppraisalCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        if (!Enum.TryParse<RhshfCreditRecommendation>(request.Recommendation, ignoreCase: true, out var recommendation))
            return ApplicationResult.Failure($"Unknown recommendation: '{request.Recommendation}'.");

        // Deliberately not falling back to hard-coded defaults — if credit policy is unconfigured,
        // that is a blocking error, not something to paper over with numbers invented here.
        var thresholds = await _thresholdsRepo.GetActiveAsync(ct);
        if (thresholds is null)
            return ApplicationResult.Failure(
                "No active appraisal thresholds are configured. Set them in Admin > RH-SHF Appraisal Thresholds first.");

        var result = profile.SaveFinancialAppraisal(
            request.UserId, request.OwnProductionCost, request.HarvestAndLogisticsCost,
            request.CycleMonths, request.InterestRatePercent, request.AssumptionBasisNote,
            thresholds, recommendation, request.SummaryNotes, request.OverrideJustification);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
