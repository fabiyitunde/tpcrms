using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.StatementAnalysis;

public class CashflowSummary : ValueObject
{
    // Period Info
    public int PeriodMonths { get; }
    public DateTime PeriodStart { get; }
    public DateTime PeriodEnd { get; }

    // Total Flows
    public decimal TotalCredits { get; }
    public decimal TotalDebits { get; }
    public decimal NetCashflow { get; }
    public int TotalTransactionCount { get; }
    public int CreditCount { get; }
    public int DebitCount { get; }

    // Monthly Averages
    public decimal AverageMonthlyCredits { get; }
    public decimal AverageMonthlyDebits { get; }
    public decimal AverageMonthlyBalance { get; }
    public decimal AverageTransactionSize { get; }

    // Salary/Income Detection
    public decimal? DetectedMonthlySalary { get; }
    public bool HasRegularSalary { get; }
    public int? SalaryPayDay { get; }
    public string? SalarySource { get; }

    // Recurring Obligations
    public decimal TotalMonthlyObligations { get; }
    public decimal DetectedLoanRepayments { get; }
    public decimal DetectedRentPayments { get; }
    public decimal DetectedUtilityPayments { get; }

    // Risk Indicators
    public decimal GamblingTransactionsTotal { get; }
    public int GamblingTransactionCount { get; }
    public int BouncedTransactionCount { get; }
    public int DaysWithNegativeBalance { get; }
    public decimal LowestBalance { get; }
    public decimal HighestBalance { get; }

    // Volatility Metrics
    public decimal BalanceVolatility { get; }
    public decimal IncomeVolatility { get; }
    public decimal CreditToDebitRatio { get; }

    // Calculated Ratios
    public decimal DebtServiceCoverageRatio { get; }
    public decimal DisposableIncomeRatio { get; }

    private CashflowSummary() { }

    public CashflowSummary(
        int periodMonths,
        DateTime periodStart,
        DateTime periodEnd,
        decimal totalCredits,
        decimal totalDebits,
        int creditCount,
        int debitCount,
        decimal averageMonthlyBalance,
        decimal? detectedMonthlySalary,
        bool hasRegularSalary,
        int? salaryPayDay,
        string? salarySource,
        decimal totalMonthlyObligations,
        decimal detectedLoanRepayments,
        decimal detectedRentPayments,
        decimal detectedUtilityPayments,
        decimal gamblingTransactionsTotal,
        int gamblingTransactionCount,
        int bouncedTransactionCount,
        int daysWithNegativeBalance,
        decimal lowestBalance,
        decimal highestBalance,
        decimal balanceVolatility,
        decimal incomeVolatility)
    {
        PeriodMonths = periodMonths;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        TotalCredits = totalCredits;
        TotalDebits = totalDebits;
        NetCashflow = totalCredits - totalDebits;
        CreditCount = creditCount;
        DebitCount = debitCount;
        TotalTransactionCount = creditCount + debitCount;

        AverageMonthlyCredits = periodMonths > 0 ? totalCredits / periodMonths : 0;
        AverageMonthlyDebits = periodMonths > 0 ? totalDebits / periodMonths : 0;
        AverageMonthlyBalance = averageMonthlyBalance;
        AverageTransactionSize = TotalTransactionCount > 0 ? (totalCredits + totalDebits) / TotalTransactionCount : 0;

        DetectedMonthlySalary = detectedMonthlySalary;
        HasRegularSalary = hasRegularSalary;
        SalaryPayDay = salaryPayDay;
        SalarySource = salarySource;

        TotalMonthlyObligations = totalMonthlyObligations;
        DetectedLoanRepayments = detectedLoanRepayments;
        DetectedRentPayments = detectedRentPayments;
        DetectedUtilityPayments = detectedUtilityPayments;

        GamblingTransactionsTotal = gamblingTransactionsTotal;
        GamblingTransactionCount = gamblingTransactionCount;
        BouncedTransactionCount = bouncedTransactionCount;
        DaysWithNegativeBalance = daysWithNegativeBalance;
        LowestBalance = lowestBalance;
        HighestBalance = highestBalance;

        BalanceVolatility = balanceVolatility;
        IncomeVolatility = incomeVolatility;
        CreditToDebitRatio = totalDebits > 0 ? totalCredits / totalDebits : 0;

        // DSCR = (Monthly Income - Expenses excl debt) / Debt Payments
        var monthlyIncome = AverageMonthlyCredits;
        var monthlyExpensesExclDebt = AverageMonthlyDebits - detectedLoanRepayments;
        DebtServiceCoverageRatio = detectedLoanRepayments > 0 
            ? (monthlyIncome - monthlyExpensesExclDebt) / detectedLoanRepayments 
            : 0;

        // Disposable = (Income - Obligations) / Income
        DisposableIncomeRatio = monthlyIncome > 0 
            ? (monthlyIncome - totalMonthlyObligations) / monthlyIncome 
            : 0;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return PeriodStart;
        yield return PeriodEnd;
        yield return TotalCredits;
        yield return TotalDebits;
        yield return DetectedMonthlySalary;
    }
}
