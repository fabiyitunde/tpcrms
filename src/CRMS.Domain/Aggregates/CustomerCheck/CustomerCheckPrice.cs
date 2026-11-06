using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.CustomerCheck;

/// <summary>
/// What a consumer is charged for one billable item of a customer credit check: the check itself, a
/// section answered, or a third-party call made. Every item exists with a price of zero until
/// finance sets it, so the dashboard shows money the moment a price is entered and nothing waits
/// on a pricing decision.
/// </summary>
public class CustomerCheckPrice : AggregateRoot
{
    public string Item { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public string Currency { get; private set; } = "NGN";

    private CustomerCheckPrice() { }

    public static Result<CustomerCheckPrice> Create(string item, string displayName, decimal unitPrice, string currency, Guid createdBy)
    {
        if (string.IsNullOrWhiteSpace(item) || item.Length > 50)
            return Result.Failure<CustomerCheckPrice>("Item is required.");
        if (unitPrice < 0)
            return Result.Failure<CustomerCheckPrice>("A price cannot be negative.");
        var price = new CustomerCheckPrice
        {
            Item = item.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? item.Trim() : displayName.Trim(),
            UnitPrice = unitPrice,
            Currency = string.IsNullOrWhiteSpace(currency) ? "NGN" : currency.Trim().ToUpperInvariant(),
        };
        price.SetAuditInfo(createdBy.ToString(), isNew: true);
        return Result.Success(price);
    }

    public Result SetPrice(decimal unitPrice, Guid modifiedBy)
    {
        if (unitPrice < 0)
            return Result.Failure("A price cannot be negative.");
        if (unitPrice > 10_000_000m)
            return Result.Failure("That price is beyond the allowed maximum.");
        UnitPrice = unitPrice;
        SetAuditInfo(modifiedBy.ToString());
        return Result.Success();
    }
}

/// <summary>The billable items, in the order the pricing table shows them.</summary>
public static class CustomerCheckPriceItems
{
    public const string Check = "check";
    public const string SectionPrefix = "section:";
    public const string CallIdentity = "call:identity";
    public const string CallBureau = "call:bureau";
    public const string CallScore = "call:score";
    public const string CallFraud = "call:fraud";
    public const string CallAdvisory = "call:advisory";

    public static string Section(string section) => SectionPrefix + section;

    /// <summary>Item key and display name, in display order.</summary>
    public static readonly IReadOnlyList<(string Item, string DisplayName)> All =
    [
        (Check, "Per completed check"),
        (Section("identity"), "Section: identity"),
        (Section("creditBureau"), "Section: credit bureau"),
        (Section("fraudCheck"), "Section: fraud check"),
        (Section("bankAccounts"), "Section: bank accounts"),
        (Section("bankLoans"), "Section: bank loans"),
        (Section("crmsFacilities"), "Section: CRMS facilities"),
        (Section("aiAdvisory"), "Section: AI advisory"),
        (CallIdentity, "Third-party call: identity (BVN)"),
        (CallBureau, "Third-party call: bureau report"),
        (CallScore, "Third-party call: credit score"),
        (CallFraud, "Third-party call: fraud check"),
        (CallAdvisory, "Third-party call: advisory narrative"),
    ];
}
