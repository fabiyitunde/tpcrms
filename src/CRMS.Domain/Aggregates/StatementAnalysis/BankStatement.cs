using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.ValueObjects;

namespace CRMS.Domain.Aggregates.StatementAnalysis;

public class BankStatement : AggregateRoot
{
    public Guid? LoanApplicationId { get; private set; }
    public string AccountNumber { get; private set; } = string.Empty;
    public string AccountName { get; private set; } = string.Empty;
    public string BankName { get; private set; } = string.Empty;
    public string Currency { get; private set; } = "NGN";
    public DateTime PeriodStart { get; private set; }
    public DateTime PeriodEnd { get; private set; }
    public decimal OpeningBalance { get; private set; }
    public decimal ClosingBalance { get; private set; }
    public StatementFormat Format { get; private set; }
    public StatementSource Source { get; private set; }
    public AnalysisStatus AnalysisStatus { get; private set; }
    public string? OriginalFileName { get; private set; }
    public string? FilePath { get; private set; }
    public Guid UploadedByUserId { get; private set; }

    // Verification status for external statements
    public StatementVerificationStatus VerificationStatus { get; private set; }
    public DateTime? VerifiedAt { get; private set; }
    public Guid? VerifiedByUserId { get; private set; }
    public string? VerificationNotes { get; private set; }
    
    // Data integrity validation results
    public bool? BalanceReconciled { get; private set; }
    public decimal? CalculatedClosingBalance { get; private set; }
    public decimal? BalanceDiscrepancy { get; private set; }

    private readonly List<StatementTransaction> _transactions = [];
    public IReadOnlyCollection<StatementTransaction> Transactions => _transactions.AsReadOnly();

    public CashflowSummary? CashflowSummary { get; private set; }

    /// <summary>
    /// Trust weight for cashflow analysis (0.0 to 1.0).
    /// Internal statements: 1.0 (100%)
    /// External statements: 0.85 (85%) - slight discount for external source
    /// </summary>
    public decimal TrustWeight => Source switch
    {
        StatementSource.CoreBanking => 1.0m,
        StatementSource.OpenBanking => 0.95m,
        StatementSource.MonoConnect => 0.90m,
        StatementSource.ManualUpload => VerificationStatus == StatementVerificationStatus.Verified ? 0.85m : 0.70m,
        _ => 0.70m
    };

    /// <summary>
    /// Whether this is an internal (own bank) statement
    /// </summary>
    public bool IsInternal => Source == StatementSource.CoreBanking;

    /// <summary>
    /// Whether this is an external (other bank) statement
    /// </summary>
    public bool IsExternal => Source != StatementSource.CoreBanking;

    /// <summary>
    /// Whether the statement can be used for analysis
    /// Internal: Always ready
    /// External: Ready after basic data integrity check passes
    /// </summary>
    public bool IsReadyForAnalysis => IsInternal || 
        (VerificationStatus != StatementVerificationStatus.Rejected && BalanceReconciled == true);

    private BankStatement() { }

    public static Result<BankStatement> Create(
        string accountNumber,
        string accountName,
        string bankName,
        DateTime periodStart,
        DateTime periodEnd,
        decimal openingBalance,
        decimal closingBalance,
        StatementFormat format,
        StatementSource source,
        Guid uploadedByUserId,
        string? originalFileName = null,
        string? filePath = null,
        Guid? loanApplicationId = null,
        string currency = "NGN")
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return Result.Failure<BankStatement>("Account number is required");

        if (periodStart >= periodEnd)
            return Result.Failure<BankStatement>("Period start must be before period end");

        var statement = new BankStatement
        {
            AccountNumber = accountNumber,
            AccountName = accountName,
            BankName = bankName,
            Currency = currency,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            OpeningBalance = openingBalance,
            ClosingBalance = closingBalance,
            Format = format,
            Source = source,
            AnalysisStatus = AnalysisStatus.Pending,
            OriginalFileName = originalFileName,
            FilePath = filePath,
            UploadedByUserId = uploadedByUserId,
            LoanApplicationId = loanApplicationId,
            // Internal statements are auto-verified; external need verification
            VerificationStatus = source == StatementSource.CoreBanking 
                ? StatementVerificationStatus.Verified 
                : StatementVerificationStatus.Pending
        };

        statement.AddDomainEvent(new BankStatementUploadedEvent(statement.Id, accountNumber, periodStart, periodEnd, source));

        return Result.Success(statement);
    }

    public Result<StatementTransaction> AddTransaction(
        DateTime date,
        string description,
        decimal amount,
        StatementTransactionType type,
        decimal runningBalance,
        string? reference = null)
    {
        if (date < PeriodStart || date > PeriodEnd)
            return Result.Failure<StatementTransaction>("Transaction date must be within statement period");

        var transaction = StatementTransaction.Create(
            Id, date, description, amount, type, runningBalance, reference);

        if (transaction.IsFailure)
            return transaction;

        _transactions.Add(transaction.Value);
        return transaction;
    }

    public void CategorizeTransaction(Guid transactionId, TransactionCategory category, decimal confidence)
    {
        var transaction = _transactions.FirstOrDefault(t => t.Id == transactionId);
        transaction?.SetCategory(category, confidence);
    }

    public void MarkProcessing()
    {
        AnalysisStatus = AnalysisStatus.Processing;
    }

    public void CompleteAnalysis(CashflowSummary summary)
    {
        CashflowSummary = summary;
        AnalysisStatus = AnalysisStatus.Completed;
        AddDomainEvent(new StatementAnalysisCompletedEvent(Id, LoanApplicationId));
    }

    public void MarkFailed(string reason)
    {
        AnalysisStatus = AnalysisStatus.Failed;
    }

    /// <summary>
    /// Perform data integrity validation on the statement.
    /// Checks that opening balance + sum of transactions = closing balance.
    /// </summary>
    public Result ValidateDataIntegrity()
    {
        var totalCredits = _transactions.Where(t => t.Type == StatementTransactionType.Credit).Sum(t => t.Amount);
        var totalDebits = _transactions.Where(t => t.Type == StatementTransactionType.Debit).Sum(t => t.Amount);
        
        CalculatedClosingBalance = OpeningBalance + totalCredits - totalDebits;
        BalanceDiscrepancy = Math.Abs(ClosingBalance - CalculatedClosingBalance.Value);
        
        // Allow tolerance of 1 NGN for rounding
        BalanceReconciled = BalanceDiscrepancy <= 1.0m;

        if (!BalanceReconciled.Value)
        {
            return Result.Failure($"Balance discrepancy of {BalanceDiscrepancy:N2} detected. " +
                $"Expected closing: {CalculatedClosingBalance:N2}, Actual: {ClosingBalance:N2}");
        }

        return Result.Success();
    }

    /// <summary>
    /// Verify an external statement after data integrity check passes.
    /// </summary>
    public Result Verify(Guid verifiedByUserId, string? notes = null)
    {
        if (IsInternal)
            return Result.Failure("Internal statements do not require manual verification");

        if (VerificationStatus == StatementVerificationStatus.Verified)
            return Result.Failure("Statement is already verified");

        if (BalanceReconciled != true)
            return Result.Failure("Statement must pass data integrity validation before verification");

        VerificationStatus = StatementVerificationStatus.Verified;
        VerifiedAt = DateTime.UtcNow;
        VerifiedByUserId = verifiedByUserId;
        VerificationNotes = notes;

        AddDomainEvent(new BankStatementVerifiedEvent(Id, LoanApplicationId, Source));

        return Result.Success();
    }

    /// <summary>
    /// Reject an external statement that fails verification.
    /// </summary>
    public Result Reject(Guid rejectedByUserId, string reason)
    {
        if (IsInternal)
            return Result.Failure("Internal statements cannot be rejected");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Rejection reason is required");

        VerificationStatus = StatementVerificationStatus.Rejected;
        VerifiedAt = DateTime.UtcNow;
        VerifiedByUserId = rejectedByUserId;
        VerificationNotes = reason;

        return Result.Success();
    }

    public int GetStatementMonths()
    {
        return (int)Math.Ceiling((PeriodEnd - PeriodStart).TotalDays / 30.0);
    }

    /// <summary>
    /// Check if the statement covers the required period (typically 6+ months).
    /// </summary>
    public bool MeetsMinimumPeriod(int requiredMonths = 6)
    {
        return GetStatementMonths() >= requiredMonths;
    }
}

// Domain Events
public record BankStatementUploadedEvent(Guid StatementId, string AccountNumber, DateTime PeriodStart, DateTime PeriodEnd, StatementSource Source) : DomainEvent;
public record StatementAnalysisCompletedEvent(Guid StatementId, Guid? LoanApplicationId) : DomainEvent;
public record BankStatementVerifiedEvent(Guid StatementId, Guid? LoanApplicationId, StatementSource Source) : DomainEvent;
