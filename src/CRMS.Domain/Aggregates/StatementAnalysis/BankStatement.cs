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

    private readonly List<StatementTransaction> _transactions = [];
    public IReadOnlyCollection<StatementTransaction> Transactions => _transactions.AsReadOnly();

    public CashflowSummary? CashflowSummary { get; private set; }

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
            LoanApplicationId = loanApplicationId
        };

        statement.AddDomainEvent(new BankStatementUploadedEvent(statement.Id, accountNumber, periodStart, periodEnd));

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

    public int GetStatementMonths()
    {
        return (int)Math.Ceiling((PeriodEnd - PeriodStart).TotalDays / 30.0);
    }
}

// Domain Events
public record BankStatementUploadedEvent(Guid StatementId, string AccountNumber, DateTime PeriodStart, DateTime PeriodEnd) : DomainEvent;
public record StatementAnalysisCompletedEvent(Guid StatementId, Guid? LoanApplicationId) : DomainEvent;
