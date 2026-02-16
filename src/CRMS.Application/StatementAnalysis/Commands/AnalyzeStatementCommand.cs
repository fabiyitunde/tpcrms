using CRMS.Application.Common;
using CRMS.Application.StatementAnalysis.DTOs;
using CRMS.Domain.Aggregates.StatementAnalysis;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Services;

namespace CRMS.Application.StatementAnalysis.Commands;

public record AnalyzeStatementCommand(Guid StatementId) : IRequest<ApplicationResult<StatementAnalysisResultDto>>;

public class AnalyzeStatementHandler : IRequestHandler<AnalyzeStatementCommand, ApplicationResult<StatementAnalysisResultDto>>
{
    private readonly IBankStatementRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TransactionCategorizationService _categorizationService;
    private readonly CashflowAnalysisService _cashflowService;

    public AnalyzeStatementHandler(
        IBankStatementRepository repository,
        IUnitOfWork unitOfWork,
        TransactionCategorizationService categorizationService,
        CashflowAnalysisService cashflowService)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _categorizationService = categorizationService;
        _cashflowService = cashflowService;
    }

    public async Task<ApplicationResult<StatementAnalysisResultDto>> Handle(AnalyzeStatementCommand request, CancellationToken ct = default)
    {
        var statement = await _repository.GetByIdWithTransactionsAsync(request.StatementId, ct);
        if (statement == null)
            return ApplicationResult<StatementAnalysisResultDto>.Failure("Statement not found");

        if (statement.Transactions.Count == 0)
            return ApplicationResult<StatementAnalysisResultDto>.Failure("Statement has no transactions to analyze");

        statement.MarkProcessing();

        // Step 1: Categorize all transactions
        _categorizationService.CategorizeAllTransactions(statement);

        // Step 2: Generate cashflow summary
        var cashflowSummary = _cashflowService.AnalyzeStatement(statement);

        // Step 3: Complete analysis
        statement.CompleteAnalysis(cashflowSummary);

        _repository.Update(statement);
        await _unitOfWork.SaveChangesAsync(ct);

        // Build result
        var categoryBreakdown = BuildCategoryBreakdown(statement);
        var redFlags = IdentifyRedFlags(cashflowSummary);
        var positiveIndicators = IdentifyPositiveIndicators(cashflowSummary);

        var result = new StatementAnalysisResultDto(
            MapToDto(statement),
            categoryBreakdown,
            redFlags,
            positiveIndicators
        );

        return ApplicationResult<StatementAnalysisResultDto>.Success(result);
    }

    private static List<TransactionCategorySummaryDto> BuildCategoryBreakdown(BankStatement statement)
    {
        var totalAmount = statement.Transactions.Sum(t => t.Amount);
        
        return statement.Transactions
            .GroupBy(t => t.Category)
            .Select(g => new TransactionCategorySummaryDto(
                g.Key.ToString(),
                g.Count(),
                g.Sum(t => t.Amount),
                totalAmount > 0 ? Math.Round(g.Sum(t => t.Amount) / totalAmount * 100, 2) : 0
            ))
            .OrderByDescending(c => c.TotalAmount)
            .ToList();
    }

    private static List<string> IdentifyRedFlags(CashflowSummary summary)
    {
        var flags = new List<string>();

        if (summary.GamblingTransactionCount > 0)
            flags.Add($"Gambling activity detected: {summary.GamblingTransactionCount} transactions totaling {summary.GamblingTransactionsTotal:N0}");

        if (summary.DaysWithNegativeBalance > 5)
            flags.Add($"Frequent negative balance: {summary.DaysWithNegativeBalance} days");

        if (summary.BouncedTransactionCount > 0)
            flags.Add($"Bounced/failed transactions: {summary.BouncedTransactionCount}");

        if (summary.IncomeVolatility > 0.5m)
            flags.Add($"High income volatility: {summary.IncomeVolatility:P0}");

        if (summary.BalanceVolatility > 1.0m)
            flags.Add($"High balance volatility: {summary.BalanceVolatility:P0}");

        if (summary.DebtServiceCoverageRatio > 0 && summary.DebtServiceCoverageRatio < 1.2m)
            flags.Add($"Low debt service coverage ratio: {summary.DebtServiceCoverageRatio:F2}");

        if (summary.DisposableIncomeRatio < 0.2m)
            flags.Add($"Low disposable income ratio: {summary.DisposableIncomeRatio:P0}");

        return flags;
    }

    private static List<string> IdentifyPositiveIndicators(CashflowSummary summary)
    {
        var indicators = new List<string>();

        if (summary.HasRegularSalary && summary.DetectedMonthlySalary.HasValue)
            indicators.Add($"Regular salary detected: {summary.DetectedMonthlySalary:N0}/month on day {summary.SalaryPayDay}");

        if (summary.DaysWithNegativeBalance == 0)
            indicators.Add("No days with negative balance");

        if (summary.NetCashflow > 0)
            indicators.Add($"Positive net cashflow: {summary.NetCashflow:N0}");

        if (summary.DebtServiceCoverageRatio >= 2.0m)
            indicators.Add($"Strong debt service coverage: {summary.DebtServiceCoverageRatio:F2}x");

        if (summary.CreditToDebitRatio > 1.1m)
            indicators.Add($"Healthy credit-to-debit ratio: {summary.CreditToDebitRatio:F2}");

        if (summary.IncomeVolatility < 0.2m && summary.PeriodMonths >= 3)
            indicators.Add("Stable income pattern");

        return indicators;
    }

    private static BankStatementDto MapToDto(BankStatement s)
    {
        CashflowSummaryDto? summaryDto = null;
        if (s.CashflowSummary != null)
        {
            var cs = s.CashflowSummary;
            summaryDto = new CashflowSummaryDto(
                cs.PeriodMonths, cs.PeriodStart, cs.PeriodEnd,
                cs.TotalCredits, cs.TotalDebits, cs.NetCashflow, cs.TotalTransactionCount,
                cs.AverageMonthlyCredits, cs.AverageMonthlyDebits, cs.AverageMonthlyBalance,
                cs.DetectedMonthlySalary, cs.HasRegularSalary, cs.SalaryPayDay, cs.SalarySource,
                cs.TotalMonthlyObligations, cs.DetectedLoanRepayments, cs.DetectedRentPayments, cs.DetectedUtilityPayments,
                cs.GamblingTransactionsTotal, cs.GamblingTransactionCount, cs.BouncedTransactionCount,
                cs.DaysWithNegativeBalance, cs.LowestBalance, cs.HighestBalance,
                cs.BalanceVolatility, cs.IncomeVolatility, cs.CreditToDebitRatio,
                cs.DebtServiceCoverageRatio, cs.DisposableIncomeRatio
            );
        }

        return new BankStatementDto(
            s.Id, s.LoanApplicationId, s.AccountNumber, s.AccountName, s.BankName, s.Currency,
            s.PeriodStart, s.PeriodEnd, s.OpeningBalance, s.ClosingBalance,
            s.Format.ToString(), s.Source.ToString(), s.AnalysisStatus.ToString(),
            s.OriginalFileName, s.Transactions.Count, s.CreatedAt, summaryDto
        );
    }
}
