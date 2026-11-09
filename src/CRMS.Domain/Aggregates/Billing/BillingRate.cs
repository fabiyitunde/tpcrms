using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Billing;

/// <summary>
/// What the bank pays the vendor for one unit of an item from a given month on, and what that unit
/// costs the vendor. A rate is scoped to one flow, or to every flow when <see cref="Flow"/> is null.
/// Rates are never deleted: changing a price from next month adds a row, so earlier months keep
/// theirs. Setting a price for a month that already has a row replaces that row's figures, which is
/// how a mistake, or a late agreement, is corrected.
/// </summary>
public class BillingRate : AggregateRoot
{
    public const decimal MaxAmount = 100_000_000m;

    public string Item { get; private set; } = string.Empty;
    public BillingFlow? Flow { get; private set; }
    /// <summary>First day of the month the rate applies from, UTC.</summary>
    public DateTime EffectiveFrom { get; private set; }
    public decimal UnitPrice { get; private set; }
    /// <summary>The vendor's own cost per unit. Shown only to Billing Managers.</summary>
    public decimal UnitCost { get; private set; }
    public string Currency { get; private set; } = "NGN";
    public string? Note { get; private set; }

    private BillingRate() { }

    public static Result<BillingRate> Create(string item, BillingFlow? flow, DateTime effectiveFromMonth, decimal unitPrice, decimal unitCost, string? note, Guid setBy)
    {
        if (BillingItems.Find(item) is null)
            return Result.Failure<BillingRate>($"'{item}' is not a billable item.");
        var amounts = CheckAmounts(unitPrice, unitCost);
        if (amounts.IsFailure)
            return Result.Failure<BillingRate>(amounts.Error);
        var rate = new BillingRate
        {
            Item = item,
            Flow = flow,
            EffectiveFrom = BillingUsage.MonthOf(effectiveFromMonth.Kind == DateTimeKind.Utc ? effectiveFromMonth : DateTime.SpecifyKind(effectiveFromMonth, DateTimeKind.Utc)),
            UnitPrice = unitPrice,
            UnitCost = unitCost,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        };
        rate.SetAuditInfo(setBy.ToString(), isNew: true);
        return Result.Success(rate);
    }

    public Result Revise(decimal unitPrice, decimal unitCost, string? note, Guid setBy)
    {
        var amounts = CheckAmounts(unitPrice, unitCost);
        if (amounts.IsFailure)
            return amounts;
        UnitPrice = unitPrice;
        UnitCost = unitCost;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        SetAuditInfo(setBy.ToString());
        return Result.Success();
    }

    private static Result CheckAmounts(decimal unitPrice, decimal unitCost)
    {
        if (unitPrice < 0 || unitCost < 0)
            return Result.Failure("A price or cost cannot be negative.");
        if (unitPrice > MaxAmount || unitCost > MaxAmount)
            return Result.Failure("That amount is beyond the allowed maximum.");
        if (decimal.Round(unitPrice, 2) != unitPrice || decimal.Round(unitCost, 2) != unitCost)
            return Result.Failure("Amounts are kept to two decimal places.");
        return Result.Success();
    }
}
