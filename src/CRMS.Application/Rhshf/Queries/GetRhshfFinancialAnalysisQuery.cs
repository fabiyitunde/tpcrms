using CRMS.Application.Common;
using CRMS.Domain.Aggregates.FinancialStatement;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfFinancialAnalysisQuery(string Reference) : IRequest<ApplicationResult<RhshfFinancialAnalysisDto>>;

/// <summary>The FAC's financial statements across the captured years plus the derived analysis. Ratios
/// are computed here in memory from the shared FinancialRatios engine, so they match Corporate exactly
/// without persisting Corporate's sub-statement entities.</summary>
public record RhshfFinancialAnalysisDto(
    List<RhshfFinancialStatementDto> Years,
    int RequiredYears,
    bool RequirementMet,
    decimal TotalTurnover,
    decimal LatestRevenue,
    decimal? RevenueCagrPercent,
    decimal AverageNetMarginPercent,
    // Business-rule readiness: the requested facility against the captured turnover. The threshold
    // (e.g. loan <= 1.5x turnover) is pending the bank's confirmation, so this is surfaced as the raw
    // data point the forthcoming rule will act on — not yet enforced.
    decimal FacilityAmount,
    decimal? LoanToTurnoverCoverage);

public record RhshfFinancialStatementDto(
    Guid Id,
    int FinancialYear,
    string? YearEndDate,
    string YearType,
    string InputMethod,
    string Status,
    string Currency,
    // Derived headline figures (from the shared value objects)
    decimal TotalRevenue,
    decimal GrossProfit,
    decimal OperatingProfit,
    decimal Ebitda,
    decimal NetProfit,
    decimal TotalAssets,
    decimal TotalCurrentAssets,
    decimal TotalLiabilities,
    decimal TotalCurrentLiabilities,
    decimal TotalEquity,
    decimal TotalDebt,
    decimal WorkingCapital,
    bool IsBalanced,
    RhshfFinancialRatiosDto Ratios,
    // Raw line items for the edit form (reuses the domain carrier)
    RhshfFinancialStatementFigures Figures,
    string? OriginalFileName,
    Guid? VerifiedByUserId,
    string? VerifiedByName,
    DateTime? VerifiedAt,
    string? VerificationNotes,
    string? RejectionReason,
    DateTime SubmittedAt);

public record RhshfFinancialRatiosDto(
    decimal CurrentRatio, decimal QuickRatio, decimal CashRatio,
    decimal DebtToEquityRatio, decimal DebtToAssetsRatio, decimal InterestCoverageRatio, decimal DebtServiceCoverageRatio,
    decimal GrossMarginPercent, decimal OperatingMarginPercent, decimal NetProfitMarginPercent, decimal EbitdaMarginPercent,
    decimal ReturnOnAssets, decimal ReturnOnEquity,
    decimal AssetTurnover, decimal InventoryTurnover, decimal ReceivablesDays, decimal PayablesDays, decimal CashConversionCycle,
    decimal WorkingCapital, decimal NetWorth, decimal TotalDebt,
    bool IsLiquidityHealthy, bool IsLeverageHealthy, bool IsProfitable, bool HasStrongCashGeneration,
    string LiquidityAssessment, string LeverageAssessment, string ProfitabilityAssessment, string OverallAssessment);

public class GetRhshfFinancialAnalysisHandler : IRequestHandler<GetRhshfFinancialAnalysisQuery, ApplicationResult<RhshfFinancialAnalysisDto>>
{
    // The bank requires three years of statements. Kept here as the single default until the business
    // rules are confirmed and this becomes a configurable value.
    private const int DefaultRequiredYears = 3;

    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfFinancialStatementRepository _statements;
    private readonly IUserNameResolver _names;

    public GetRhshfFinancialAnalysisHandler(
        IRhshfCreditProfileRepository repo, IRhshfFinancialStatementRepository statements, IUserNameResolver names)
    {
        _repo = repo;
        _statements = statements;
        _names = names;
    }

    public async Task<ApplicationResult<RhshfFinancialAnalysisDto>> Handle(GetRhshfFinancialAnalysisQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfFinancialAnalysisDto>.Failure("Case not found.");

        var statements = await _statements.GetByProfileIdAsync(profile.Id, ct);
        var verifierNames = await _names.ResolveManyAsync(
            statements.Where(s => s.VerifiedByUserId.HasValue).Select(s => s.VerifiedByUserId!.Value), ct);

        var years = statements
            .OrderByDescending(s => s.FinancialYear)
            .Select(s => MapStatement(s, verifierNames))
            .ToList();

        // Turnover across the captured years — the base for the forthcoming loan-to-turnover rule.
        var totalTurnover = years.Sum(y => y.TotalRevenue);
        var latest = years.OrderByDescending(y => y.FinancialYear).FirstOrDefault();
        var earliest = years.OrderBy(y => y.FinancialYear).FirstOrDefault();

        decimal? cagr = null;
        if (latest is not null && earliest is not null && latest.FinancialYear != earliest.FinancialYear
            && earliest.TotalRevenue > 0)
        {
            var periods = latest.FinancialYear - earliest.FinancialYear;
            cagr = (decimal)(Math.Pow((double)(latest.TotalRevenue / earliest.TotalRevenue), 1.0 / periods) - 1) * 100m;
        }

        var avgNetMargin = years.Count > 0 ? years.Average(y => y.Ratios.NetProfitMarginPercent) : 0m;

        // The facility is the requested EOP value; coverage = facility ÷ turnover is the raw number the
        // forthcoming loan-to-turnover rule will threshold.
        var facility = profile.TotalEopValue;
        decimal? coverage = totalTurnover > 0 ? facility / totalTurnover : null;

        var dto = new RhshfFinancialAnalysisDto(
            years,
            DefaultRequiredYears,
            RequirementMet: years.Count(y => y.Status != nameof(Domain.Enums.RhshfFinancialStatementStatus.Rejected)) >= DefaultRequiredYears,
            totalTurnover,
            latest?.TotalRevenue ?? 0,
            cagr,
            avgNetMargin,
            facility,
            coverage);

        return ApplicationResult<RhshfFinancialAnalysisDto>.Success(dto);
    }

    private static RhshfFinancialStatementDto MapStatement(RhshfFinancialStatement s, IReadOnlyDictionary<Guid, string> verifierNames)
    {
        var inc = IncomeStatement.Create(s.Id,
            s.Revenue ?? 0, s.OtherOperatingIncome ?? 0, s.CostOfSales ?? 0, s.SellingExpenses ?? 0,
            s.AdministrativeExpenses ?? 0, s.DepreciationAmortization ?? 0, s.OtherOperatingExpenses ?? 0,
            s.InterestIncome ?? 0, s.InterestExpense ?? 0, s.OtherFinanceCosts ?? 0, s.IncomeTaxExpense ?? 0,
            s.DividendsDeclared ?? 0);

        var bs = BalanceSheet.Create(s.Id,
            s.CashAndCashEquivalents ?? 0, s.TradeReceivables ?? 0, s.Inventory ?? 0, s.PrepaidExpenses ?? 0,
            s.OtherCurrentAssets ?? 0, s.PropertyPlantEquipment ?? 0, s.IntangibleAssets ?? 0, s.LongTermInvestments ?? 0,
            s.DeferredTaxAssets ?? 0, s.OtherNonCurrentAssets ?? 0, s.TradePayables ?? 0, s.ShortTermBorrowings ?? 0,
            s.CurrentPortionLongTermDebt ?? 0, s.AccruedExpenses ?? 0, s.TaxPayable ?? 0, s.OtherCurrentLiabilities ?? 0,
            s.LongTermDebt ?? 0, s.DeferredTaxLiabilities ?? 0, s.Provisions ?? 0, s.OtherNonCurrentLiabilities ?? 0,
            s.ShareCapital ?? 0, s.SharePremium ?? 0, s.RetainedEarnings ?? 0, s.OtherReserves ?? 0);

        var cf = CashFlowStatement.Create(s.Id,
            s.CfProfitBeforeTax ?? 0, s.CfDepreciationAmortization ?? 0, s.CfInterestExpenseAddBack ?? 0,
            s.CfChangesInWorkingCapital ?? 0, s.CfTaxPaid ?? 0, s.CfOtherOperatingAdjustments ?? 0,
            s.CfPurchaseOfPpe ?? 0, s.CfSaleOfPpe ?? 0, s.CfPurchaseOfInvestments ?? 0, s.CfSaleOfInvestments ?? 0,
            s.CfInterestReceived ?? 0, s.CfDividendsReceived ?? 0, s.CfOtherInvestingActivities ?? 0,
            s.CfProceedsFromBorrowings ?? 0, s.CfRepaymentOfBorrowings ?? 0, s.CfInterestPaid ?? 0,
            s.CfDividendsPaid ?? 0, s.CfProceedsFromShareIssue ?? 0, s.CfOtherFinancingActivities ?? 0,
            s.CfOpeningCashBalance ?? 0);

        var r = FinancialRatios.Calculate(bs, inc, cf);

        var ratios = new RhshfFinancialRatiosDto(
            r.CurrentRatio, r.QuickRatio, r.CashRatio,
            r.DebtToEquityRatio, r.DebtToAssetsRatio, r.InterestCoverageRatio, r.DebtServiceCoverageRatio,
            r.GrossMarginPercent, r.OperatingMarginPercent, r.NetProfitMarginPercent, r.EBITDAMarginPercent,
            r.ReturnOnAssets, r.ReturnOnEquity,
            r.AssetTurnover, r.InventoryTurnover, r.ReceivablesDays, r.PayablesDays, r.CashConversionCycle,
            r.WorkingCapital, r.NetWorth, r.TotalDebt,
            r.IsLiquidityHealthy, r.IsLeverageHealthy, r.IsProfitable, r.HasStrongCashGeneration,
            r.GetLiquidityAssessment(), r.GetLeverageAssessment(), r.GetProfitabilityAssessment(), r.GetOverallAssessment());

        var figures = new RhshfFinancialStatementFigures(
            s.Revenue, s.OtherOperatingIncome, s.CostOfSales, s.SellingExpenses, s.AdministrativeExpenses,
            s.DepreciationAmortization, s.OtherOperatingExpenses, s.InterestIncome, s.InterestExpense, s.OtherFinanceCosts,
            s.IncomeTaxExpense, s.DividendsDeclared,
            s.CashAndCashEquivalents, s.TradeReceivables, s.Inventory, s.PrepaidExpenses, s.OtherCurrentAssets,
            s.PropertyPlantEquipment, s.IntangibleAssets, s.LongTermInvestments, s.DeferredTaxAssets, s.OtherNonCurrentAssets,
            s.TradePayables, s.ShortTermBorrowings, s.CurrentPortionLongTermDebt, s.AccruedExpenses, s.TaxPayable,
            s.OtherCurrentLiabilities, s.LongTermDebt, s.DeferredTaxLiabilities, s.Provisions, s.OtherNonCurrentLiabilities,
            s.ShareCapital, s.SharePremium, s.RetainedEarnings, s.OtherReserves,
            s.CfProfitBeforeTax, s.CfDepreciationAmortization, s.CfInterestExpenseAddBack, s.CfChangesInWorkingCapital,
            s.CfTaxPaid, s.CfOtherOperatingAdjustments, s.CfPurchaseOfPpe, s.CfSaleOfPpe, s.CfPurchaseOfInvestments,
            s.CfSaleOfInvestments, s.CfInterestReceived, s.CfDividendsReceived, s.CfOtherInvestingActivities,
            s.CfProceedsFromBorrowings, s.CfRepaymentOfBorrowings, s.CfInterestPaid, s.CfDividendsPaid,
            s.CfProceedsFromShareIssue, s.CfOtherFinancingActivities, s.CfOpeningCashBalance,
            s.AuditorName, s.AuditorFirm, s.AuditDate, s.AuditOpinion);

        return new RhshfFinancialStatementDto(
            s.Id, s.FinancialYear, s.YearEndDate, s.YearType.ToString(), s.InputMethod.ToString(), s.Status.ToString(), s.Currency,
            inc.TotalRevenue, inc.GrossProfit, inc.OperatingProfit, inc.EBITDA, inc.NetProfit,
            bs.TotalAssets, bs.TotalCurrentAssets, bs.TotalLiabilities, bs.TotalCurrentLiabilities, bs.TotalEquity,
            bs.TotalDebt, bs.WorkingCapital, bs.IsBalanced(),
            ratios, figures,
            s.OriginalFileName,
            s.VerifiedByUserId,
            s.VerifiedByUserId is Guid id && verifierNames.TryGetValue(id, out var n) ? n : null,
            s.VerifiedAt, s.VerificationNotes, s.RejectionReason, s.SubmittedAt);
    }
}
