using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Billing;

public enum BillingStatementStatus
{
    Issued,
    Paid,
    Voided,
}

/// <summary>
/// A closed billing period: what the bank owed for a month, frozen at the moment it was issued.
/// The ledger rows it covers are stamped with its id, so they cannot be billed twice; voiding a
/// statement releases them. A statement is never edited, only paid or voided, and it keeps its
/// number either way, so the bank's copy and ours always agree.
/// </summary>
public class BillingStatement : AggregateRoot
{
    private readonly List<BillingStatementLine> _lines = [];

    public string Number { get; private set; } = string.Empty;
    /// <summary>First day of the period's month, UTC. Late rows from earlier months appear as their own lines.</summary>
    public DateTime Month { get; private set; }
    public BillingStatementStatus Status { get; private set; }
    public string Currency { get; private set; } = "NGN";
    public decimal Subtotal { get; private set; }
    public decimal VatRatePercent { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal Total { get; private set; }
    /// <summary>The vendor's own cost for the rows covered. Never printed; for the margin figure only.</summary>
    public decimal CostTotal { get; private set; }
    public int UsageCount { get; private set; }
    public DateTime IssuedAt { get; private set; }
    public Guid IssuedBy { get; private set; }
    public string IssuedByName { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public Guid? PaidBy { get; private set; }
    public string? PaymentReference { get; private set; }
    public string? PaymentNote { get; private set; }
    public DateTime? VoidedAt { get; private set; }
    public Guid? VoidedBy { get; private set; }
    public string? VoidReason { get; private set; }

    public IReadOnlyCollection<BillingStatementLine> Lines => _lines.AsReadOnly();

    private BillingStatement() { }

    public static Result<BillingStatement> Create(string number, DateTime month, string currency, decimal vatRatePercent, Guid issuedBy, string issuedByName, string? notes)
    {
        if (string.IsNullOrWhiteSpace(number))
            return Result.Failure<BillingStatement>("A statement needs a number.");
        if (vatRatePercent < 0 || vatRatePercent > 100)
            return Result.Failure<BillingStatement>("The VAT rate must be between 0 and 100 percent.");
        var statement = new BillingStatement
        {
            Number = number.Trim(),
            Month = BillingUsage.MonthOf(month),
            Status = BillingStatementStatus.Issued,
            Currency = string.IsNullOrWhiteSpace(currency) ? "NGN" : currency.Trim().ToUpperInvariant(),
            VatRatePercent = vatRatePercent,
            IssuedAt = DateTime.UtcNow,
            IssuedBy = issuedBy,
            IssuedByName = string.IsNullOrWhiteSpace(issuedByName) ? "Billing Manager" : issuedByName.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        };
        statement.SetAuditInfo(issuedBy.ToString(), isNew: true);
        return Result.Success(statement);
    }

    /// <summary>Adds a line and brings the totals up to date. Lines are added once, at issue.</summary>
    public void AddLine(DateTime month, string item, string itemName, BillingCategory category, BillingFlow flow, int quantity, decimal unitPrice, decimal unitCost)
    {
        if (quantity <= 0) return;
        _lines.Add(new BillingStatementLine(Id, BillingUsage.MonthOf(month), item, itemName, category, flow, quantity, unitPrice, unitCost, _lines.Count + 1));
        Subtotal = decimal.Round(_lines.Sum(l => l.Amount), 2);
        CostTotal = decimal.Round(_lines.Sum(l => l.Quantity * l.UnitCost), 2);
        VatAmount = decimal.Round(Subtotal * VatRatePercent / 100m, 2);
        Total = Subtotal + VatAmount;
        UsageCount = _lines.Sum(l => l.Quantity);
    }

    public Result MarkPaid(DateTime paidAtUtc, Guid paidBy, string? reference, string? note)
    {
        if (Status == BillingStatementStatus.Voided)
            return Result.Failure("A voided statement cannot be paid.");
        if (Status == BillingStatementStatus.Paid)
            return Result.Failure("This statement is already marked as paid.");
        if (paidAtUtc > DateTime.UtcNow.AddDays(1))
            return Result.Failure("The payment date cannot be in the future.");
        Status = BillingStatementStatus.Paid;
        PaidAt = paidAtUtc;
        PaidBy = paidBy;
        PaymentReference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        PaymentNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        SetAuditInfo(paidBy.ToString());
        return Result.Success();
    }

    public Result Void(Guid voidedBy, string reason)
    {
        if (Status == BillingStatementStatus.Paid)
            return Result.Failure("A paid statement cannot be voided. Record a credit on the next statement instead.");
        if (Status == BillingStatementStatus.Voided)
            return Result.Failure("This statement is already voided.");
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Say why the statement is being voided.");
        Status = BillingStatementStatus.Voided;
        VoidedAt = DateTime.UtcNow;
        VoidedBy = voidedBy;
        VoidReason = reason.Trim();
        SetAuditInfo(voidedBy.ToString());
        return Result.Success();
    }
}

/// <summary>One priced line of a statement: so many of one item, for one flow, in one month.</summary>
public class BillingStatementLine : Entity
{
    public Guid StatementId { get; private set; }
    public DateTime Month { get; private set; }
    public string Item { get; private set; } = string.Empty;
    public string ItemName { get; private set; } = string.Empty;
    public BillingCategory Category { get; private set; }
    public BillingFlow Flow { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal Amount { get; private set; }
    public int SortOrder { get; private set; }

    private BillingStatementLine() { }

    public BillingStatementLine(Guid statementId, DateTime month, string item, string itemName, BillingCategory category, BillingFlow flow, int quantity, decimal unitPrice, decimal unitCost, int sortOrder)
    {
        StatementId = statementId;
        Month = month;
        Item = item;
        ItemName = itemName;
        Category = category;
        Flow = flow;
        Quantity = quantity;
        UnitPrice = unitPrice;
        UnitCost = unitCost;
        Amount = decimal.Round(quantity * unitPrice, 2);
        SortOrder = sortOrder;
    }
}
