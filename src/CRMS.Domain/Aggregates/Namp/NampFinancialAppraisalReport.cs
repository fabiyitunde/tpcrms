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
        string? summaryNotes)
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
        string? summaryNotes)
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
    }
}
