using CRMS.Application.Common;
using CRMS.Application.FinancialAnalysis.Commands;
using CRMS.Application.FinancialAnalysis.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.FinancialAnalysis.Queries;

public record GetFinancialStatementByIdQuery(Guid Id) : IRequest<ApplicationResult<FinancialStatementDto>>;

public class GetFinancialStatementByIdHandler : IRequestHandler<GetFinancialStatementByIdQuery, ApplicationResult<FinancialStatementDto>>
{
    private readonly IFinancialStatementRepository _repository;

    public GetFinancialStatementByIdHandler(IFinancialStatementRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<FinancialStatementDto>> Handle(GetFinancialStatementByIdQuery request, CancellationToken ct = default)
    {
        var statement = await _repository.GetByIdWithDetailsAsync(request.Id, ct);
        if (statement == null)
            return ApplicationResult<FinancialStatementDto>.Failure("Financial statement not found");

        return ApplicationResult<FinancialStatementDto>.Success(FinancialStatementMapper.ToDto(statement));
    }
}

public record GetFinancialStatementsByLoanApplicationQuery(Guid LoanApplicationId) : IRequest<ApplicationResult<List<FinancialStatementSummaryDto>>>;

public class GetFinancialStatementsByLoanApplicationHandler : IRequestHandler<GetFinancialStatementsByLoanApplicationQuery, ApplicationResult<List<FinancialStatementSummaryDto>>>
{
    private readonly IFinancialStatementRepository _repository;

    public GetFinancialStatementsByLoanApplicationHandler(IFinancialStatementRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<FinancialStatementSummaryDto>>> Handle(GetFinancialStatementsByLoanApplicationQuery request, CancellationToken ct = default)
    {
        var statements = await _repository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);

        var dtos = statements.Select(fs => new FinancialStatementSummaryDto(
            fs.Id,
            fs.FinancialYear,
            fs.YearType.ToString(),
            fs.Status.ToString(),
            fs.BalanceSheet?.TotalAssets,
            fs.IncomeStatement?.TotalRevenue,
            fs.IncomeStatement?.NetProfit,
            fs.CalculatedRatios?.GetOverallAssessment(),
            fs.SubmittedAt
        )).OrderByDescending(x => x.FinancialYear).ToList();

        return ApplicationResult<List<FinancialStatementSummaryDto>>.Success(dtos);
    }
}

public record GetFinancialRatiosTrendQuery(Guid LoanApplicationId) : IRequest<ApplicationResult<FinancialRatiosTrendDto>>;

public record FinancialRatiosTrendDto(
    Guid LoanApplicationId,
    List<YearlyRatiosDto> YearlyData,
    TrendAnalysisDto TrendAnalysis
);

public record YearlyRatiosDto(
    int Year,
    string YearType,
    FinancialRatiosDto? Ratios
);

public record TrendAnalysisDto(
    string RevenueTrend,
    string ProfitabilityTrend,
    string LiquidityTrend,
    string LeverageTrend,
    string OverallTrend
);

public class GetFinancialRatiosTrendHandler : IRequestHandler<GetFinancialRatiosTrendQuery, ApplicationResult<FinancialRatiosTrendDto>>
{
    private readonly IFinancialStatementRepository _repository;

    public GetFinancialRatiosTrendHandler(IFinancialStatementRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<FinancialRatiosTrendDto>> Handle(GetFinancialRatiosTrendQuery request, CancellationToken ct = default)
    {
        var statements = await _repository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
        
        if (statements.Count == 0)
            return ApplicationResult<FinancialRatiosTrendDto>.Failure("No financial statements found");

        var yearlyData = statements
            .OrderBy(s => s.FinancialYear)
            .Select(fs => new YearlyRatiosDto(
                fs.FinancialYear,
                fs.YearType.ToString(),
                fs.CalculatedRatios != null ? MapRatios(fs.CalculatedRatios) : null
            ))
            .ToList();

        var trendAnalysis = AnalyzeTrends(statements.OrderBy(s => s.FinancialYear).ToList());

        return ApplicationResult<FinancialRatiosTrendDto>.Success(new FinancialRatiosTrendDto(
            request.LoanApplicationId,
            yearlyData,
            trendAnalysis
        ));
    }

    private static TrendAnalysisDto AnalyzeTrends(List<Domain.Aggregates.FinancialStatement.FinancialStatement> statements)
    {
        if (statements.Count < 2)
        {
            return new TrendAnalysisDto("Insufficient Data", "Insufficient Data", "Insufficient Data", "Insufficient Data", "Insufficient Data");
        }

        var revenues = statements
            .Where(s => s.IncomeStatement != null)
            .Select(s => s.IncomeStatement!.TotalRevenue)
            .ToList();

        var profits = statements
            .Where(s => s.IncomeStatement != null)
            .Select(s => s.IncomeStatement!.NetProfit)
            .ToList();

        var currentRatios = statements
            .Where(s => s.CalculatedRatios != null)
            .Select(s => s.CalculatedRatios!.CurrentRatio)
            .ToList();

        var debtRatios = statements
            .Where(s => s.CalculatedRatios != null)
            .Select(s => s.CalculatedRatios!.DebtToEquityRatio)
            .ToList();

        return new TrendAnalysisDto(
            CalculateTrend(revenues),
            CalculateTrend(profits),
            CalculateTrend(currentRatios),
            CalculateTrendInverse(debtRatios), // Lower is better for debt
            CalculateOverallTrend(revenues, profits, currentRatios, debtRatios)
        );
    }

    private static string CalculateTrend(List<decimal> values)
    {
        if (values.Count < 2) return "Insufficient Data";

        var growth = values.Last() - values.First();
        var percentChange = values.First() != 0 ? (growth / values.First()) * 100 : 0;

        return percentChange switch
        {
            > 20 => "Strong Growth",
            > 5 => "Growing",
            > -5 => "Stable",
            > -20 => "Declining",
            _ => "Significant Decline"
        };
    }

    private static string CalculateTrendInverse(List<decimal> values)
    {
        if (values.Count < 2) return "Insufficient Data";

        var change = values.Last() - values.First();

        return change switch
        {
            < -0.5m => "Improving Significantly",
            < -0.1m => "Improving",
            < 0.1m => "Stable",
            < 0.5m => "Deteriorating",
            _ => "Deteriorating Significantly"
        };
    }

    private static string CalculateOverallTrend(List<decimal> revenues, List<decimal> profits, List<decimal> liquidity, List<decimal> leverage)
    {
        var revTrend = CalculateTrend(revenues);
        var profTrend = CalculateTrend(profits);
        var liqTrend = CalculateTrend(liquidity);
        var levTrend = CalculateTrendInverse(leverage);

        var positive = new[] { "Strong Growth", "Growing", "Improving Significantly", "Improving" };
        var negative = new[] { "Significant Decline", "Declining", "Deteriorating Significantly", "Deteriorating" };

        var positiveCount = new[] { revTrend, profTrend, liqTrend, levTrend }.Count(t => positive.Contains(t));
        var negativeCount = new[] { revTrend, profTrend, liqTrend, levTrend }.Count(t => negative.Contains(t));

        if (positiveCount >= 3) return "Positive";
        if (negativeCount >= 3) return "Negative";
        if (positiveCount >= 2) return "Moderately Positive";
        if (negativeCount >= 2) return "Moderately Negative";
        return "Mixed";
    }

    private static FinancialRatiosDto MapRatios(Domain.Aggregates.FinancialStatement.FinancialRatios r) => new(
        r.CurrentRatio, r.QuickRatio, r.CashRatio, r.GetLiquidityAssessment(),
        r.DebtToEquityRatio, r.DebtToAssetsRatio, r.InterestCoverageRatio, r.DebtServiceCoverageRatio, r.GetLeverageAssessment(),
        r.GrossMarginPercent, r.OperatingMarginPercent, r.NetProfitMarginPercent, r.EBITDAMarginPercent,
        r.ReturnOnAssets, r.ReturnOnEquity, r.GetProfitabilityAssessment(),
        r.AssetTurnover, r.InventoryTurnover, r.ReceivablesDays, r.PayablesDays, r.CashConversionCycle,
        r.WorkingCapital, r.NetWorth, r.TotalDebt, r.GetOverallAssessment()
    );
}
