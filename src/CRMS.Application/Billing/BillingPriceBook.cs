using CRMS.Domain.Aggregates.Billing;

namespace CRMS.Application.Billing;

public sealed record BillingUnitRate(decimal UnitPrice, decimal UnitCost, string Currency, DateTime? EffectiveFrom)
{
    public static readonly BillingUnitRate Zero = new(0m, 0m, "NGN", null);
    public bool IsSet => EffectiveFrom is not null;
}

/// <summary>
/// Answers "what does one unit of this item cost the bank, for this flow, in this month?" from the
/// rate history. A flow-specific rate beats an all-flows rate; among those, the latest month that is
/// not after the asked month wins. No rate means zero, so an unpriced item shows up with a count and
/// no money rather than vanishing.
/// </summary>
public sealed class BillingPriceBook
{
    private readonly ILookup<string, BillingRate> _byItem;

    public BillingPriceBook(IEnumerable<BillingRate> rates)
    {
        _byItem = rates.ToLookup(r => r.Item, StringComparer.Ordinal);
    }

    public BillingUnitRate Resolve(string item, BillingFlow flow, DateTime month)
    {
        month = BillingUsage.MonthOf(month);
        var candidates = _byItem[item].Where(r => r.EffectiveFrom <= month).ToList();
        var pick = candidates.Where(r => r.Flow == flow).OrderByDescending(r => r.EffectiveFrom).FirstOrDefault()
                   ?? candidates.Where(r => r.Flow is null).OrderByDescending(r => r.EffectiveFrom).FirstOrDefault();
        return pick is null ? BillingUnitRate.Zero : new BillingUnitRate(pick.UnitPrice, pick.UnitCost, pick.Currency, pick.EffectiveFrom);
    }

    /// <summary>The rate that applies right now for the price list page: this month's, or the latest earlier one.</summary>
    public BillingUnitRate Current(string item, BillingFlow? flow) =>
        Resolve(item, flow ?? BillingFlow.Unattributed, BillingUsage.MonthOf(DateTime.UtcNow));

    /// <summary>Every rate row for an item, newest first, for the history drawer.</summary>
    public IReadOnlyList<BillingRate> History(string item, BillingFlow? flow) =>
        _byItem[item].Where(r => r.Flow == flow).OrderByDescending(r => r.EffectiveFrom).ToList();
}
