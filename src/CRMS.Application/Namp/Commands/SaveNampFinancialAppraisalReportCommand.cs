using CRMS.Application.Common;
using CRMS.Application.Namp.DTOs;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Namp.Commands;

public record SaveNampFinancialAppraisalReportCommand(
    Guid NampApplicationId,
    Guid UserId,
    decimal? MonthlyDisposableIncome,
    decimal? DebtServiceCoverageRatio,
    decimal? LoanToValueRatio,
    string RepaymentCapacityRating,
    string? EquityAssessmentNote,
    string? CreditBureauSummary,
    string CreditOfficerRecommendation,
    string? SummaryNotes,
    string RepaymentSource,
    decimal? ProjectedMonthlyRentalRevenue,
    decimal? UtilisationRateAssumption,
    string? DemandEvidenceNote,
    decimal? HectaresPerMonth = null,
    decimal? RatePerHectare = null,
    decimal? MonthlyFuelCost = null,
    decimal? MonthlyMaintenanceCost = null,
    decimal? MonthlyOperatorWage = null,
    decimal? NetPresentValue = null,
    decimal? BenefitCostRatio = null,
    decimal? InternalRateOfReturn = null,
    decimal? ProfitabilityIndex = null
) : IRequest<ApplicationResult<NampFinancialAppraisalReportDto>>;

public class SaveNampFinancialAppraisalReportHandler
    : IRequestHandler<SaveNampFinancialAppraisalReportCommand, ApplicationResult<NampFinancialAppraisalReportDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly IUnitOfWork _uow;

    public SaveNampFinancialAppraisalReportHandler(INampApplicationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampFinancialAppraisalReportDto>> Handle(
        SaveNampFinancialAppraisalReportCommand request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<NampRepaymentCapacityRating>(request.RepaymentCapacityRating, ignoreCase: true, out var capacity))
            return ApplicationResult<NampFinancialAppraisalReportDto>.Failure($"Unknown repayment capacity rating: '{request.RepaymentCapacityRating}'.");
        if (!Enum.TryParse<NampCreditRecommendation>(request.CreditOfficerRecommendation, ignoreCase: true, out var creditRec))
            return ApplicationResult<NampFinancialAppraisalReportDto>.Failure($"Unknown credit recommendation: '{request.CreditOfficerRecommendation}'.");
        if (!Enum.TryParse<NampRepaymentSource>(request.RepaymentSource, ignoreCase: true, out var repaymentSource))
            return ApplicationResult<NampFinancialAppraisalReportDto>.Failure($"Unknown repayment source: '{request.RepaymentSource}'.");

        var existing = await _repo.GetFinancialAppraisalReportAsync(request.NampApplicationId, ct);

        if (existing is null)
        {
            var createResult = NampFinancialAppraisalReport.Create(
                request.NampApplicationId, request.UserId,
                request.MonthlyDisposableIncome, request.DebtServiceCoverageRatio, request.LoanToValueRatio,
                capacity, request.EquityAssessmentNote, request.CreditBureauSummary,
                creditRec, request.SummaryNotes,
                repaymentSource, request.ProjectedMonthlyRentalRevenue,
                request.UtilisationRateAssumption, request.DemandEvidenceNote,
                request.HectaresPerMonth, request.RatePerHectare,
                request.MonthlyFuelCost, request.MonthlyMaintenanceCost, request.MonthlyOperatorWage,
                request.NetPresentValue, request.BenefitCostRatio,
                request.InternalRateOfReturn, request.ProfitabilityIndex);

            if (createResult.IsFailure)
                return ApplicationResult<NampFinancialAppraisalReportDto>.Failure(createResult.Error);

            await _repo.AddFinancialAppraisalReportAsync(createResult.Value, ct);
            await _uow.SaveChangesAsync(ct);
            return ApplicationResult<NampFinancialAppraisalReportDto>.Success(MapToDto(createResult.Value));
        }
        else
        {
            existing.Update(
                request.UserId,
                request.MonthlyDisposableIncome, request.DebtServiceCoverageRatio, request.LoanToValueRatio,
                capacity, request.EquityAssessmentNote, request.CreditBureauSummary,
                creditRec, request.SummaryNotes,
                repaymentSource, request.ProjectedMonthlyRentalRevenue,
                request.UtilisationRateAssumption, request.DemandEvidenceNote,
                request.HectaresPerMonth, request.RatePerHectare,
                request.MonthlyFuelCost, request.MonthlyMaintenanceCost, request.MonthlyOperatorWage,
                request.NetPresentValue, request.BenefitCostRatio,
                request.InternalRateOfReturn, request.ProfitabilityIndex);

            await _uow.SaveChangesAsync(ct);
            return ApplicationResult<NampFinancialAppraisalReportDto>.Success(MapToDto(existing));
        }
    }

    internal static NampFinancialAppraisalReportDto MapToDto(NampFinancialAppraisalReport r) => new(
        r.Id, r.NampApplicationId, r.PreparedByUserId, r.SavedAt,
        r.MonthlyDisposableIncome, r.DebtServiceCoverageRatio, r.LoanToValueRatio,
        r.RepaymentCapacityRating.ToString(), r.EquityAssessmentNote,
        r.CreditBureauSummary, r.CreditOfficerRecommendation.ToString(), r.SummaryNotes,
        r.RepaymentSource.ToString(), r.ProjectedMonthlyRentalRevenue,
        r.UtilisationRateAssumption, r.DemandEvidenceNote,
        r.HectaresPerMonth, r.RatePerHectare,
        r.MonthlyFuelCost, r.MonthlyMaintenanceCost, r.MonthlyOperatorWage,
        r.NetPresentValue, r.BenefitCostRatio, r.InternalRateOfReturn, r.ProfitabilityIndex);
}
