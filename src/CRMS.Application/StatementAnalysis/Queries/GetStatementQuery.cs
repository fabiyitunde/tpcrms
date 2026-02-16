using CRMS.Application.Common;
using CRMS.Application.StatementAnalysis.DTOs;
using CRMS.Domain.Aggregates.StatementAnalysis;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.StatementAnalysis.Queries;

public record GetStatementByIdQuery(Guid Id) : IRequest<ApplicationResult<BankStatementDto>>;

public class GetStatementByIdHandler : IRequestHandler<GetStatementByIdQuery, ApplicationResult<BankStatementDto>>
{
    private readonly IBankStatementRepository _repository;

    public GetStatementByIdHandler(IBankStatementRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<BankStatementDto>> Handle(GetStatementByIdQuery request, CancellationToken ct = default)
    {
        var statement = await _repository.GetByIdWithTransactionsAsync(request.Id, ct);
        if (statement == null)
            return ApplicationResult<BankStatementDto>.Failure("Statement not found");

        return ApplicationResult<BankStatementDto>.Success(MapToDto(statement));
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

public record GetStatementTransactionsQuery(Guid StatementId) : IRequest<ApplicationResult<List<StatementTransactionDto>>>;

public class GetStatementTransactionsHandler : IRequestHandler<GetStatementTransactionsQuery, ApplicationResult<List<StatementTransactionDto>>>
{
    private readonly IBankStatementRepository _repository;

    public GetStatementTransactionsHandler(IBankStatementRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<StatementTransactionDto>>> Handle(GetStatementTransactionsQuery request, CancellationToken ct = default)
    {
        var statement = await _repository.GetByIdWithTransactionsAsync(request.StatementId, ct);
        if (statement == null)
            return ApplicationResult<List<StatementTransactionDto>>.Failure("Statement not found");

        var dtos = statement.Transactions
            .OrderBy(t => t.Date)
            .Select(t => new StatementTransactionDto(
                t.Id, t.Date, t.Description, t.Amount, t.Type.ToString(),
                t.RunningBalance, t.Reference, t.Category.ToString(),
                t.CategoryConfidence, t.IsRecurring
            ))
            .ToList();

        return ApplicationResult<List<StatementTransactionDto>>.Success(dtos);
    }
}

public record GetStatementsByLoanApplicationQuery(Guid LoanApplicationId) : IRequest<ApplicationResult<List<BankStatementSummaryDto>>>;

public class GetStatementsByLoanApplicationHandler : IRequestHandler<GetStatementsByLoanApplicationQuery, ApplicationResult<List<BankStatementSummaryDto>>>
{
    private readonly IBankStatementRepository _repository;

    public GetStatementsByLoanApplicationHandler(IBankStatementRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<BankStatementSummaryDto>>> Handle(GetStatementsByLoanApplicationQuery request, CancellationToken ct = default)
    {
        var statements = await _repository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);

        var dtos = statements.Select(s => new BankStatementSummaryDto(
            s.Id, s.AccountNumber, s.BankName, s.PeriodStart, s.PeriodEnd,
            s.AnalysisStatus.ToString(), s.Transactions.Count, s.CreatedAt
        )).ToList();

        return ApplicationResult<List<BankStatementSummaryDto>>.Success(dtos);
    }
}
