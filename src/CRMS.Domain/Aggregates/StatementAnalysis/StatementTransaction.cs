using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.StatementAnalysis;

public class StatementTransaction : Entity
{
    public Guid BankStatementId { get; private set; }
    public DateTime Date { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string NormalizedDescription { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public StatementTransactionType Type { get; private set; }
    public decimal RunningBalance { get; private set; }
    public string? Reference { get; private set; }
    public TransactionCategory Category { get; private set; }
    public decimal CategoryConfidence { get; private set; }
    public bool IsRecurring { get; private set; }
    public string? RecurringPattern { get; private set; }

    private StatementTransaction() { }

    public static Result<StatementTransaction> Create(
        Guid bankStatementId,
        DateTime date,
        string description,
        decimal amount,
        StatementTransactionType type,
        decimal runningBalance,
        string? reference = null)
    {
        if (string.IsNullOrWhiteSpace(description))
            return Result.Failure<StatementTransaction>("Description is required");

        if (amount <= 0)
            return Result.Failure<StatementTransaction>("Amount must be greater than zero");

        return Result.Success(new StatementTransaction
        {
            BankStatementId = bankStatementId,
            Date = date,
            Description = description,
            NormalizedDescription = NormalizeDescription(description),
            Amount = amount,
            Type = type,
            RunningBalance = runningBalance,
            Reference = reference,
            Category = TransactionCategory.Unknown,
            CategoryConfidence = 0
        });
    }

    public void SetCategory(TransactionCategory category, decimal confidence)
    {
        Category = category;
        CategoryConfidence = Math.Clamp(confidence, 0, 1);
    }

    public void MarkAsRecurring(string pattern)
    {
        IsRecurring = true;
        RecurringPattern = pattern;
    }

    private static string NormalizeDescription(string description)
    {
        return description
            .ToUpperInvariant()
            .Replace("  ", " ")
            .Trim();
    }

    public bool IsCredit => Type == StatementTransactionType.Credit;
    public bool IsDebit => Type == StatementTransactionType.Debit;
}
