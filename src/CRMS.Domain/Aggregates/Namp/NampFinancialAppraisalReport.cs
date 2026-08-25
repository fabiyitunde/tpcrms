using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Namp;

/// <summary>
/// Structured financial appraisal report prepared by the Credit Officer.
/// Standalone entity — one per NAMP application.
/// </summary>
public class NampFinancialAppraisalReport : Entity
{
    public Guid NampApplicationId { get; private set; }
    public Guid PreparedByUserId { get; private set; }
    public DateTime SavedAt { get; private set; }

    // Income & Capacity
    public decimal? MonthlyDisposableIncome { get; private set; }
    public decimal? DebtServiceCoverageRatio { get; private set; }
    public decimal? LoanToValueRatio { get; private set; }
    public NampRepaymentCapacityRating RepaymentCapacityRating { get; private set; }
    public string? EquityAssessmentNote { get; private set; }

    // Credit Bureau
    public string? CreditBureauSummary { get; private set; }

    // Recommendation
    public NampCreditRecommendation CreditOfficerRecommendation { get; private set; }
    public string? SummaryNotes { get; private set; }

    // Repayment source (rental model fields — kept for backward compat, not shown in new UI)
    public NampRepaymentSource RepaymentSource { get; private set; }
    public decimal? ProjectedMonthlyRentalRevenue { get; private set; }
    public decimal? UtilisationRateAssumption { get; private set; }
    public string? DemandEvidenceNote { get; private set; }

    // Viability calculator inputs (officer-collected from applicant call)
    public decimal? HectaresPerMonth { get; private set; }
    public decimal? RatePerHectare { get; private set; }
    public decimal? MonthlyFuelCost { get; private set; }
    public decimal? MonthlyMaintenanceCost { get; private set; }
    public decimal? MonthlyOperatorWage { get; private set; }

    // Viability calculator computed metrics (stored at save time for AI advisory and audit trail)
    public decimal? NetPresentValue { get; private set; }
    public decimal? BenefitCostRatio { get; private set; }
    public decimal? InternalRateOfReturn { get; private set; }
    public decimal? ProfitabilityIndex { get; private set; }

    private NampFinancialAppraisalReport() { }

    public static Result<NampFinancialAppraisalReport> Create(
        Guid nampApplicationId,
        Guid preparedByUserId,
        decimal? monthlyDisposableIncome,
        decimal? debtServiceCoverageRatio,
        decimal? loanToValueRatio,
        NampRepaymentCapacityRating repaymentCapacityRating,
        string? equityAssessmentNote,
        string? creditBureauSummary,
        NampCreditRecommendation creditOfficerRecommendation,
        string? summaryNotes,
        NampRepaymentSource repaymentSource = NampRepaymentSource.PrimaryIncome,
        decimal? projectedMonthlyRentalRevenue = null,
        decimal? utilisationRateAssumption = null,
        string? demandEvidenceNote = null,
        decimal? hectaresPerMonth = null,
        decimal? ratePerHectare = null,
        decimal? monthlyFuelCost = null,
        decimal? monthlyMaintenanceCost = null,
        decimal? monthlyOperatorWage = null,
        decimal? netPresentValue = null,
        decimal? benefitCostRatio = null,
        decimal? internalRateOfReturn = null,
        decimal? profitabilityIndex = null)
    {
        return Result.Success(new NampFinancialAppraisalReport
        {
            NampApplicationId = nampApplicationId,
            PreparedByUserId = preparedByUserId,
            SavedAt = DateTime.UtcNow,
            MonthlyDisposableIncome = monthlyDisposableIncome,
            DebtServiceCoverageRatio = debtServiceCoverageRatio,
            LoanToValueRatio = loanToValueRatio,
            RepaymentCapacityRating = repaymentCapacityRating,
            EquityAssessmentNote = equityAssessmentNote,
            CreditBureauSummary = creditBureauSummary,
            CreditOfficerRecommendation = creditOfficerRecommendation,
            SummaryNotes = summaryNotes,
            RepaymentSource = repaymentSource,
            ProjectedMonthlyRentalRevenue = projectedMonthlyRentalRevenue,
            UtilisationRateAssumption = utilisationRateAssumption,
            DemandEvidenceNote = demandEvidenceNote,
            HectaresPerMonth = hectaresPerMonth,
            RatePerHectare = ratePerHectare,
            MonthlyFuelCost = monthlyFuelCost,
            MonthlyMaintenanceCost = monthlyMaintenanceCost,
            MonthlyOperatorWage = monthlyOperatorWage,
            NetPresentValue = netPresentValue,
            BenefitCostRatio = benefitCostRatio,
            InternalRateOfReturn = internalRateOfReturn,
            ProfitabilityIndex = profitabilityIndex,
        });
    }

    public void Update(
        Guid updatedByUserId,
        decimal? monthlyDisposableIncome,
        decimal? debtServiceCoverageRatio,
        decimal? loanToValueRatio,
        NampRepaymentCapacityRating repaymentCapacityRating,
        string? equityAssessmentNote,
        string? creditBureauSummary,
        NampCreditRecommendation creditOfficerRecommendation,
        string? summaryNotes,
        NampRepaymentSource repaymentSource = NampRepaymentSource.PrimaryIncome,
        decimal? projectedMonthlyRentalRevenue = null,
        decimal? utilisationRateAssumption = null,
        string? demandEvidenceNote = null,
        decimal? hectaresPerMonth = null,
        decimal? ratePerHectare = null,
        decimal? monthlyFuelCost = null,
        decimal? monthlyMaintenanceCost = null,
        decimal? monthlyOperatorWage = null,
        decimal? netPresentValue = null,
        decimal? benefitCostRatio = null,
        decimal? internalRateOfReturn = null,
        decimal? profitabilityIndex = null)
    {
        PreparedByUserId = updatedByUserId;
        SavedAt = DateTime.UtcNow;
        MonthlyDisposableIncome = monthlyDisposableIncome;
        DebtServiceCoverageRatio = debtServiceCoverageRatio;
        LoanToValueRatio = loanToValueRatio;
        RepaymentCapacityRating = repaymentCapacityRating;
        EquityAssessmentNote = equityAssessmentNote;
        CreditBureauSummary = creditBureauSummary;
        CreditOfficerRecommendation = creditOfficerRecommendation;
        SummaryNotes = summaryNotes;
        RepaymentSource = repaymentSource;
        ProjectedMonthlyRentalRevenue = projectedMonthlyRentalRevenue;
        UtilisationRateAssumption = utilisationRateAssumption;
        DemandEvidenceNote = demandEvidenceNote;
        HectaresPerMonth = hectaresPerMonth;
        RatePerHectare = ratePerHectare;
        MonthlyFuelCost = monthlyFuelCost;
        MonthlyMaintenanceCost = monthlyMaintenanceCost;
        MonthlyOperatorWage = monthlyOperatorWage;
        NetPresentValue = netPresentValue;
        BenefitCostRatio = benefitCostRatio;
        InternalRateOfReturn = internalRateOfReturn;
        ProfitabilityIndex = profitabilityIndex;
    }
}
