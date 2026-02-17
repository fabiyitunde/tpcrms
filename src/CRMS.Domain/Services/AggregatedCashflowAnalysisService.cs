using CRMS.Domain.Aggregates.StatementAnalysis;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Services;

/// <summary>
/// Service for analyzing cashflow across multiple bank statements (internal + external)
/// with trust-weighted aggregation.
/// </summary>
public class AggregatedCashflowAnalysisService
{
    private readonly CashflowAnalysisService _singleStatementService = new();

    /// <summary>
    /// Analyze multiple statements and produce a weighted aggregate summary.
    /// </summary>
    public AggregatedCashflowResult AnalyzeMultipleStatements(IEnumerable<BankStatement> statements)
    {
        var statementList = statements.Where(s => s.IsReadyForAnalysis).ToList();
        
        if (statementList.Count == 0)
        {
            return AggregatedCashflowResult.Empty("No statements ready for analysis");
        }

        var internalStatements = statementList.Where(s => s.IsInternal).ToList();
        var externalStatements = statementList.Where(s => s.IsExternal).ToList();

        // Analyze each statement
        var analyses = new List<(BankStatement Statement, CashflowSummary Summary)>();
        foreach (var statement in statementList)
        {
            if (statement.CashflowSummary == null)
            {
                var summary = _singleStatementService.AnalyzeStatement(statement);
                analyses.Add((statement, summary));
            }
            else
            {
                analyses.Add((statement, statement.CashflowSummary));
            }
        }

        // Calculate weighted aggregates
        var totalTrustWeight = analyses.Sum(a => a.Statement.TrustWeight);
        
        var weightedTotalCredits = analyses.Sum(a => a.Summary.TotalCredits * a.Statement.TrustWeight);
        var weightedTotalDebits = analyses.Sum(a => a.Summary.TotalDebits * a.Statement.TrustWeight);
        var weightedAvgBalance = analyses.Sum(a => a.Summary.AverageMonthlyBalance * a.Statement.TrustWeight);
        var weightedMonthlyObligations = analyses.Sum(a => a.Summary.TotalMonthlyObligations * a.Statement.TrustWeight);

        // Normalize by total weight
        var normalizedTotalCredits = totalTrustWeight > 0 ? weightedTotalCredits / totalTrustWeight : 0;
        var normalizedTotalDebits = totalTrustWeight > 0 ? weightedTotalDebits / totalTrustWeight : 0;
        var normalizedAvgBalance = totalTrustWeight > 0 ? weightedAvgBalance / totalTrustWeight : 0;
        var normalizedMonthlyObligations = totalTrustWeight > 0 ? weightedMonthlyObligations / totalTrustWeight : 0;

        // Aggregate non-weighted metrics (these are flags/counts)
        var totalGamblingAmount = analyses.Sum(a => a.Summary.GamblingTransactionsTotal);
        var totalGamblingCount = analyses.Sum(a => a.Summary.GamblingTransactionCount);
        var totalBouncedCount = analyses.Sum(a => a.Summary.BouncedTransactionCount);
        var maxDaysNegative = analyses.Max(a => a.Summary.DaysWithNegativeBalance);

        // Salary detection - prefer internal statement data
        var internalWithSalary = analyses
            .Where(a => a.Statement.IsInternal && a.Summary.HasRegularSalary)
            .FirstOrDefault();
        
        var externalWithSalary = analyses
            .Where(a => a.Statement.IsExternal && a.Summary.HasRegularSalary)
            .FirstOrDefault();

        var primarySalarySource = internalWithSalary.Summary ?? externalWithSalary.Summary;
        var detectedSalary = primarySalarySource?.DetectedMonthlySalary;
        var hasRegularSalary = primarySalarySource?.HasRegularSalary ?? false;
        var salarySource = primarySalarySource?.SalarySource;

        // Calculate overall volatility (weighted average)
        var weightedBalanceVolatility = analyses.Sum(a => a.Summary.BalanceVolatility * a.Statement.TrustWeight);
        var normalizedBalanceVolatility = totalTrustWeight > 0 ? weightedBalanceVolatility / totalTrustWeight : 0;

        var weightedIncomeVolatility = analyses.Sum(a => a.Summary.IncomeVolatility * a.Statement.TrustWeight);
        var normalizedIncomeVolatility = totalTrustWeight > 0 ? weightedIncomeVolatility / totalTrustWeight : 0;

        // Determine period coverage
        var earliestStart = analyses.Min(a => a.Summary.PeriodStart);
        var latestEnd = analyses.Max(a => a.Summary.PeriodEnd);
        var totalMonthsCovered = (int)Math.Ceiling((latestEnd - earliestStart).TotalDays / 30.0);

        // Build result
        return new AggregatedCashflowResult
        {
            IsSuccess = true,
            TotalStatementsAnalyzed = statementList.Count,
            InternalStatementsCount = internalStatements.Count,
            ExternalStatementsCount = externalStatements.Count,
            
            PeriodStart = earliestStart,
            PeriodEnd = latestEnd,
            TotalMonthsCovered = totalMonthsCovered,
            
            // Weighted metrics
            WeightedTotalCredits = normalizedTotalCredits,
            WeightedTotalDebits = normalizedTotalDebits,
            WeightedAverageMonthlyBalance = normalizedAvgBalance,
            WeightedMonthlyObligations = normalizedMonthlyObligations,
            WeightedNetMonthlyCashflow = (normalizedTotalCredits - normalizedTotalDebits) / Math.Max(1, totalMonthsCovered),
            
            // Salary info
            DetectedMonthlySalary = detectedSalary,
            HasRegularSalary = hasRegularSalary,
            SalarySource = salarySource,
            SalaryFromInternalStatement = internalWithSalary.Summary != null,
            
            // Risk indicators
            TotalGamblingAmount = totalGamblingAmount,
            TotalGamblingTransactions = totalGamblingCount,
            TotalBouncedTransactions = totalBouncedCount,
            MaxDaysWithNegativeBalance = maxDaysNegative,
            
            // Volatility
            WeightedBalanceVolatility = normalizedBalanceVolatility,
            WeightedIncomeVolatility = normalizedIncomeVolatility,
            
            // Trust assessment
            OverallTrustScore = CalculateOverallTrustScore(internalStatements.Count, externalStatements.Count, analyses),
            HasInternalStatement = internalStatements.Any(),
            AllExternalStatementsVerified = externalStatements.All(s => s.VerificationStatus == StatementVerificationStatus.Verified),
            
            // Individual statement summaries for detailed view
            StatementSummaries = analyses.Select(a => new StatementSummaryWithTrust
            {
                StatementId = a.Statement.Id,
                AccountNumber = a.Statement.AccountNumber,
                BankName = a.Statement.BankName,
                Source = a.Statement.Source,
                IsInternal = a.Statement.IsInternal,
                TrustWeight = a.Statement.TrustWeight,
                VerificationStatus = a.Statement.VerificationStatus,
                PeriodStart = a.Summary.PeriodStart,
                PeriodEnd = a.Summary.PeriodEnd,
                TotalCredits = a.Summary.TotalCredits,
                TotalDebits = a.Summary.TotalDebits,
                AverageBalance = a.Summary.AverageMonthlyBalance
            }).ToList()
        };
    }

    private static decimal CalculateOverallTrustScore(int internalCount, int externalCount, 
        List<(BankStatement Statement, CashflowSummary Summary)> analyses)
    {
        if (analyses.Count == 0) return 0;

        // Base score from statement mix
        decimal baseScore = 50;
        
        // Internal statements add significant trust
        if (internalCount > 0)
            baseScore += 30;
        
        // Having both internal and external is ideal (complete picture)
        if (internalCount > 0 && externalCount > 0)
            baseScore += 10;
        
        // Deductions for issues
        var hasUnverifiedExternal = analyses.Any(a => 
            a.Statement.IsExternal && a.Statement.VerificationStatus != StatementVerificationStatus.Verified);
        if (hasUnverifiedExternal)
            baseScore -= 10;

        // Deduction for gambling activity
        var hasGambling = analyses.Any(a => a.Summary.GamblingTransactionCount > 0);
        if (hasGambling)
            baseScore -= 5;

        // Deduction for bounced transactions
        var hasBounced = analyses.Any(a => a.Summary.BouncedTransactionCount > 0);
        if (hasBounced)
            baseScore -= 10;

        return Math.Clamp(baseScore, 0, 100);
    }
}

public class AggregatedCashflowResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    
    public int TotalStatementsAnalyzed { get; set; }
    public int InternalStatementsCount { get; set; }
    public int ExternalStatementsCount { get; set; }
    
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int TotalMonthsCovered { get; set; }
    
    // Weighted financial metrics
    public decimal WeightedTotalCredits { get; set; }
    public decimal WeightedTotalDebits { get; set; }
    public decimal WeightedAverageMonthlyBalance { get; set; }
    public decimal WeightedMonthlyObligations { get; set; }
    public decimal WeightedNetMonthlyCashflow { get; set; }
    
    // Salary detection
    public decimal? DetectedMonthlySalary { get; set; }
    public bool HasRegularSalary { get; set; }
    public string? SalarySource { get; set; }
    public bool SalaryFromInternalStatement { get; set; }
    
    // Risk indicators
    public decimal TotalGamblingAmount { get; set; }
    public int TotalGamblingTransactions { get; set; }
    public int TotalBouncedTransactions { get; set; }
    public int MaxDaysWithNegativeBalance { get; set; }
    
    // Volatility
    public decimal WeightedBalanceVolatility { get; set; }
    public decimal WeightedIncomeVolatility { get; set; }
    
    // Trust assessment
    public decimal OverallTrustScore { get; set; }
    public bool HasInternalStatement { get; set; }
    public bool AllExternalStatementsVerified { get; set; }
    
    // Individual summaries
    public List<StatementSummaryWithTrust> StatementSummaries { get; set; } = new();

    public static AggregatedCashflowResult Empty(string reason) => new()
    {
        IsSuccess = false,
        ErrorMessage = reason
    };

    // Computed properties
    public string CashflowHealthAssessment => (WeightedNetMonthlyCashflow, WeightedBalanceVolatility) switch
    {
        ( > 0, < 0.3m) => "Strong",
        ( > 0, < 0.5m) => "Good",
        ( > 0, _) => "Volatile",
        ( <= 0, _) => "Weak"
    };

    public bool MeetsMinimumRequirements => 
        HasInternalStatement && 
        TotalMonthsCovered >= 6 &&
        AllExternalStatementsVerified;

    public List<string> GetWarnings()
    {
        var warnings = new List<string>();
        
        if (!HasInternalStatement)
            warnings.Add("No internal bank statement provided - analysis based solely on external statements");
        
        if (!AllExternalStatementsVerified && ExternalStatementsCount > 0)
            warnings.Add("Some external statements are not yet verified");
        
        if (TotalGamblingTransactions > 0)
            warnings.Add($"Gambling activity detected: {TotalGamblingTransactions} transactions totaling {TotalGamblingAmount:N0}");
        
        if (TotalBouncedTransactions > 0)
            warnings.Add($"Bounced transactions detected: {TotalBouncedTransactions} occurrences");
        
        if (MaxDaysWithNegativeBalance > 5)
            warnings.Add($"Frequent negative balance: {MaxDaysWithNegativeBalance} days");
        
        if (WeightedIncomeVolatility > 0.5m)
            warnings.Add("High income volatility detected");
        
        if (TotalMonthsCovered < 6)
            warnings.Add($"Insufficient statement period: {TotalMonthsCovered} months (minimum 6 required)");

        return warnings;
    }
}

public class StatementSummaryWithTrust
{
    public Guid StatementId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public StatementSource Source { get; set; }
    public bool IsInternal { get; set; }
    public decimal TrustWeight { get; set; }
    public StatementVerificationStatus VerificationStatus { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal TotalCredits { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal AverageBalance { get; set; }
}
