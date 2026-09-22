using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfFinancialAppraisalQuery(string Reference) : IRequest<ApplicationResult<RhshfFinancialAppraisalDto>>;

/// <summary>Farm plans, the saved appraisal (if any), and the thresholds in force — one round trip,
/// because the tab renders all three together.</summary>
public record RhshfFinancialAppraisalDto(
    List<RhshfFarmPlanDto> FarmPlans,
    RhshfAppraisalReportDto? Report,
    RhshfAppraisalThresholdsDto Thresholds,
    decimal TotalEopValue,
    string Currency);

public record RhshfFarmPlanDto(
    Guid Id, string Crop, decimal Hectares, decimal ExpectedYieldKgPerHectare,
    decimal ExpectedPricePerKg, decimal ExpectedOutputKg, decimal ExpectedRevenue,
    Guid RecordedBy, string RecordedByName, DateTime RecordedAt);

public record RhshfAppraisalThresholdsDto(
    decimal MinDscr, decimal MinGrossMarginPercent, decimal HurdleRatePercent,
    decimal MinYieldHeadroomPercent, decimal MinPriceHeadroomPercent);

public record RhshfAppraisalReportDto(
    Guid Id,
    int CycleNumber,
    Guid PreparedByUserId,
    string PreparedByName,
    DateTime SavedAt,
    // Captured
    decimal OwnProductionCost, decimal HarvestAndLogisticsCost, int CycleMonths, decimal InterestRatePercent,
    string? AssumptionBasisNote,
    // Computed
    decimal GrossRevenue, decimal FinancedInputCost, decimal OwnCashCosts, decimal InterestCharge,
    decimal TotalCost, decimal AmountDueAtHarvest, decimal CashAvailableForDebtService,
    decimal GrossMargin, decimal GrossMarginPercent, decimal Dscr, decimal NetReturnToFarmer,
    decimal? Irr, decimal NetPresentValue,
    decimal TotalHectares, decimal BlendedYieldKgPerHectare, decimal BlendedPricePerKg,
    decimal? BreakEvenYieldKgPerHectare, decimal? BreakEvenPricePerKg,
    decimal? YieldHeadroomPercent, decimal? PriceHeadroomPercent,
    // Gates
    bool DscrPass, bool GrossMarginPass, bool IrrPass, bool YieldHeadroomPass, bool PriceHeadroomPass,
    bool AllGatesPass, int GatesPassed,
    // Conclusion
    RhshfRepaymentCapacityRating RepaymentCapacityRating,
    RhshfCreditRecommendation CreditOfficerRecommendation,
    string? SummaryNotes,
    string? OverrideJustification);

public class GetRhshfFinancialAppraisalHandler
    : IRequestHandler<GetRhshfFinancialAppraisalQuery, ApplicationResult<RhshfFinancialAppraisalDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfAppraisalThresholdsRepository _thresholdsRepo;
    private readonly IUserNameResolver _names;

    public GetRhshfFinancialAppraisalHandler(
        IRhshfCreditProfileRepository repo, IRhshfAppraisalThresholdsRepository thresholdsRepo, IUserNameResolver names)
    {
        _repo = repo;
        _thresholdsRepo = thresholdsRepo;
        _names = names;
    }

    public async Task<ApplicationResult<RhshfFinancialAppraisalDto>> Handle(
        GetRhshfFinancialAppraisalQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfFinancialAppraisalDto>.Failure("Case not found.");

        var thresholds = await _thresholdsRepo.GetActiveAsync(ct);
        if (thresholds is null)
            return ApplicationResult<RhshfFinancialAppraisalDto>.Failure(
                "No active appraisal thresholds are configured. Set them in Admin > RH-SHF Appraisal Thresholds first.");

        var plans = profile.GetCurrentCycleFarmPlans();
        var report = profile.GetCurrentCycleFinancialAppraisal();

        var actorIds = plans.Select(p => p.RecordedBy).ToList();
        if (report is not null) actorIds.Add(report.PreparedByUserId);
        var names = await _names.ResolveManyAsync(actorIds, ct);
        string Name(Guid id) => names.TryGetValue(id, out var n) ? n : "—";

        var dto = new RhshfFinancialAppraisalDto(
            FarmPlans: plans.Select(p => new RhshfFarmPlanDto(
                p.Id, p.Crop, p.Hectares, p.ExpectedYieldKgPerHectare, p.ExpectedPricePerKg,
                p.ExpectedOutputKg, p.ExpectedRevenue, p.RecordedBy, Name(p.RecordedBy), p.RecordedAt)).ToList(),
            Report: report is null ? null : new RhshfAppraisalReportDto(
                report.Id, report.CycleNumber, report.PreparedByUserId, Name(report.PreparedByUserId), report.SavedAt,
                report.OwnProductionCost, report.HarvestAndLogisticsCost, report.CycleMonths, report.InterestRatePercent,
                report.AssumptionBasisNote,
                report.GrossRevenue, report.FinancedInputCost, report.OwnCashCosts, report.InterestCharge,
                report.TotalCost, report.AmountDueAtHarvest, report.CashAvailableForDebtService,
                report.GrossMargin, report.GrossMarginPercent, report.Dscr, report.NetReturnToFarmer,
                report.Irr, report.NetPresentValue,
                report.TotalHectares, report.BlendedYieldKgPerHectare, report.BlendedPricePerKg,
                report.BreakEvenYieldKgPerHectare, report.BreakEvenPricePerKg,
                report.YieldHeadroomPercent, report.PriceHeadroomPercent,
                report.DscrPass, report.GrossMarginPass, report.IrrPass, report.YieldHeadroomPass, report.PriceHeadroomPass,
                report.AllGatesPass, report.GatesPassed,
                report.RepaymentCapacityRating, report.CreditOfficerRecommendation,
                report.SummaryNotes, report.OverrideJustification),
            Thresholds: new RhshfAppraisalThresholdsDto(
                thresholds.MinDscr, thresholds.MinGrossMarginPercent, thresholds.HurdleRatePercent,
                thresholds.MinYieldHeadroomPercent, thresholds.MinPriceHeadroomPercent),
            TotalEopValue: profile.TotalEopValue,
            Currency: profile.Currency);

        return ApplicationResult<RhshfFinancialAppraisalDto>.Success(dto);
    }
}
