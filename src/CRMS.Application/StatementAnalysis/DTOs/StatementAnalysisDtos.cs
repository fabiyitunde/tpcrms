namespace CRMS.Application.StatementAnalysis.DTOs;

public record BankStatementDto(
    Guid Id,
    Guid? LoanApplicationId,
    string AccountNumber,
    string AccountName,
    string BankName,
    string Currency,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal OpeningBalance,
    decimal ClosingBalance,
    string Format,
    string Source,
    string AnalysisStatus,
    string? OriginalFileName,
    int TransactionCount,
    DateTime CreatedAt,
    CashflowSummaryDto? CashflowSummary
);

public record BankStatementSummaryDto(
    Guid Id,
    string AccountNumber,
    string AccountName,
    string BankName,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal OpeningBalance,
    decimal ClosingBalance,
    string Source,
    string AnalysisStatus,
    string VerificationStatus,
    decimal TrustWeight,
    bool IsInternal,
    int MonthsCovered,
    int TransactionCount,
    string? OriginalFileName,
    DateTime CreatedAt,
    CashflowSummaryDto? CashflowSummary
);

public record StatementTransactionDto(
    Guid Id,
    DateTime Date,
    string Description,
    decimal Amount,
    string Type,
    decimal RunningBalance,
    string? Reference,
    string Category,
    decimal CategoryConfidence,
    bool IsRecurring
);

public record CashflowSummaryDto(
    // Period Info
    int PeriodMonths,
    DateTime PeriodStart,
    DateTime PeriodEnd,

    // Totals
    decimal TotalCredits,
    decimal TotalDebits,
    decimal NetCashflow,
    int TotalTransactionCount,

    // Monthly Averages
    decimal AverageMonthlyCredits,
    decimal AverageMonthlyDebits,
    decimal AverageMonthlyBalance,

    // Salary Detection
    decimal? DetectedMonthlySalary,
    bool HasRegularSalary,
    int? SalaryPayDay,
    string? SalarySource,

    // Obligations
    decimal TotalMonthlyObligations,
    decimal DetectedLoanRepayments,
    decimal DetectedRentPayments,
    decimal DetectedUtilityPayments,

    // Risk Indicators
    decimal GamblingTransactionsTotal,
    int GamblingTransactionCount,
    int BouncedTransactionCount,
    int DaysWithNegativeBalance,
    decimal LowestBalance,
    decimal HighestBalance,

    // Ratios
    decimal BalanceVolatility,
    decimal IncomeVolatility,
    decimal CreditToDebitRatio,
    decimal DebtServiceCoverageRatio,
    decimal DisposableIncomeRatio
);

public record TransactionCategorySummaryDto(
    string Category,
    int Count,
    decimal TotalAmount,
    decimal Percentage
);

public record StatementAnalysisResultDto(
    BankStatementDto Statement,
    List<TransactionCategorySummaryDto> CategoryBreakdown,
    List<string> RedFlags,
    List<string> PositiveIndicators
);
