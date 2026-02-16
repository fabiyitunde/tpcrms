using CRMS.Domain.Aggregates.StatementAnalysis;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Services;

public class CashflowAnalysisService
{
    public CashflowSummary AnalyzeStatement(BankStatement statement)
    {
        var transactions = statement.Transactions.OrderBy(t => t.Date).ToList();
        
        if (transactions.Count == 0)
        {
            return CreateEmptySummary(statement);
        }

        var periodMonths = statement.GetStatementMonths();
        
        // Basic totals
        var credits = transactions.Where(t => t.IsCredit).ToList();
        var debits = transactions.Where(t => t.IsDebit).ToList();
        var totalCredits = credits.Sum(t => t.Amount);
        var totalDebits = debits.Sum(t => t.Amount);

        // Salary detection
        var (detectedSalary, hasRegularSalary, salaryPayDay, salarySource) = DetectSalary(transactions, periodMonths);

        // Recurring obligations
        var loanRepayments = transactions
            .Where(t => t.Category == TransactionCategory.LoanRepayment)
            .Sum(t => t.Amount);
        var rentPayments = transactions
            .Where(t => t.Category == TransactionCategory.RentOrMortgage)
            .Sum(t => t.Amount);
        var utilityPayments = transactions
            .Where(t => t.Category == TransactionCategory.Utilities)
            .Sum(t => t.Amount);
        var totalObligations = (loanRepayments + rentPayments + utilityPayments) / Math.Max(1, periodMonths);

        // Gambling detection (red flag)
        var gamblingTransactions = transactions.Where(t => t.Category == TransactionCategory.Gambling).ToList();
        var gamblingTotal = gamblingTransactions.Sum(t => t.Amount);
        var gamblingCount = gamblingTransactions.Count;

        // Bounced transactions (look for reversal patterns)
        var bouncedCount = transactions.Count(t => 
            t.Description.Contains("BOUNCE", StringComparison.OrdinalIgnoreCase) ||
            t.Description.Contains("INSUFFICIENT", StringComparison.OrdinalIgnoreCase) ||
            t.Description.Contains("FAILED", StringComparison.OrdinalIgnoreCase));

        // Balance analysis
        var balances = transactions.Select(t => t.RunningBalance).ToList();
        var lowestBalance = balances.Min();
        var highestBalance = balances.Max();
        var averageBalance = balances.Average();
        var daysNegative = CalculateDaysWithNegativeBalance(transactions);

        // Volatility calculations
        var balanceVolatility = CalculateVolatility(balances);
        var monthlyIncomes = CalculateMonthlyTotals(credits);
        var incomeVolatility = CalculateVolatility(monthlyIncomes);

        return new CashflowSummary(
            periodMonths: periodMonths,
            periodStart: statement.PeriodStart,
            periodEnd: statement.PeriodEnd,
            totalCredits: totalCredits,
            totalDebits: totalDebits,
            creditCount: credits.Count,
            debitCount: debits.Count,
            averageMonthlyBalance: averageBalance,
            detectedMonthlySalary: detectedSalary,
            hasRegularSalary: hasRegularSalary,
            salaryPayDay: salaryPayDay,
            salarySource: salarySource,
            totalMonthlyObligations: totalObligations,
            detectedLoanRepayments: loanRepayments / Math.Max(1, periodMonths),
            detectedRentPayments: rentPayments / Math.Max(1, periodMonths),
            detectedUtilityPayments: utilityPayments / Math.Max(1, periodMonths),
            gamblingTransactionsTotal: gamblingTotal,
            gamblingTransactionCount: gamblingCount,
            bouncedTransactionCount: bouncedCount,
            daysWithNegativeBalance: daysNegative,
            lowestBalance: lowestBalance,
            highestBalance: highestBalance,
            balanceVolatility: balanceVolatility,
            incomeVolatility: incomeVolatility
        );
    }

    private (decimal? Salary, bool IsRegular, int? PayDay, string? Source) DetectSalary(
        List<StatementTransaction> transactions, int periodMonths)
    {
        var salaryTransactions = transactions
            .Where(t => t.Category == TransactionCategory.Salary && t.IsCredit)
            .OrderBy(t => t.Date)
            .ToList();

        if (salaryTransactions.Count == 0)
            return (null, false, null, null);

        // Group by month to check regularity
        var monthlyGroups = salaryTransactions
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .ToList();

        // Check if salary appears in most months
        var hasRegularSalary = monthlyGroups.Count >= (periodMonths * 0.7); // 70% of months

        // Calculate average salary
        var averageSalary = salaryTransactions.Average(t => t.Amount);

        // Detect common pay day
        var payDays = salaryTransactions.Select(t => t.Date.Day).ToList();
        var mostCommonPayDay = payDays
            .GroupBy(d => d)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;

        // Extract source from description
        var mostCommonSource = salaryTransactions
            .GroupBy(t => ExtractEmployerName(t.Description))
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;

        return (averageSalary, hasRegularSalary, mostCommonPayDay, mostCommonSource);
    }

    private static string? ExtractEmployerName(string description)
    {
        // Simple extraction - take first meaningful part after SALARY keyword
        var upper = description.ToUpperInvariant();
        var salaryIndex = upper.IndexOf("SALARY");
        if (salaryIndex >= 0 && salaryIndex + 7 < description.Length)
        {
            var remainder = description[(salaryIndex + 7)..].Trim();
            var parts = remainder.Split(new[] { '/', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.FirstOrDefault()?.Trim();
        }
        return null;
    }

    private static int CalculateDaysWithNegativeBalance(List<StatementTransaction> transactions)
    {
        var negativeDays = new HashSet<DateTime>();
        foreach (var t in transactions)
        {
            if (t.RunningBalance < 0)
                negativeDays.Add(t.Date.Date);
        }
        return negativeDays.Count;
    }

    private static decimal CalculateVolatility(List<decimal> values)
    {
        if (values.Count < 2) return 0;

        var mean = values.Average();
        if (mean == 0) return 0;

        var sumSquares = values.Sum(v => Math.Pow((double)(v - mean), 2));
        var stdDev = (decimal)Math.Sqrt(sumSquares / values.Count);
        
        return stdDev / Math.Abs(mean); // Coefficient of variation
    }

    private static List<decimal> CalculateMonthlyTotals(List<StatementTransaction> transactions)
    {
        return transactions
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .Select(g => g.Sum(t => t.Amount))
            .ToList();
    }

    private static CashflowSummary CreateEmptySummary(BankStatement statement)
    {
        return new CashflowSummary(
            periodMonths: statement.GetStatementMonths(),
            periodStart: statement.PeriodStart,
            periodEnd: statement.PeriodEnd,
            totalCredits: 0,
            totalDebits: 0,
            creditCount: 0,
            debitCount: 0,
            averageMonthlyBalance: 0,
            detectedMonthlySalary: null,
            hasRegularSalary: false,
            salaryPayDay: null,
            salarySource: null,
            totalMonthlyObligations: 0,
            detectedLoanRepayments: 0,
            detectedRentPayments: 0,
            detectedUtilityPayments: 0,
            gamblingTransactionsTotal: 0,
            gamblingTransactionCount: 0,
            bouncedTransactionCount: 0,
            daysWithNegativeBalance: 0,
            lowestBalance: statement.OpeningBalance,
            highestBalance: statement.ClosingBalance,
            balanceVolatility: 0,
            incomeVolatility: 0
        );
    }
}
