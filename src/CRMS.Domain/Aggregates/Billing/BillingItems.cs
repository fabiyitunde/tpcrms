namespace CRMS.Domain.Aggregates.Billing;

/// <summary>One kind of billable event.</summary>
/// <param name="Item">Stable key stored on usage rows and rates.</param>
/// <param name="Category">The group the price list and the charts show it under.</param>
/// <param name="DisplayName">What people see.</param>
/// <param name="PerFlow">True when the rate is kept per loan flow (the platform fee grid); false when one rate covers every flow.</param>
public sealed record BillingItem(string Item, BillingCategory Category, string DisplayName, bool PerFlow = false);

/// <summary>
/// The catalogue of billable items. Every third-party operation the system can make has an entry,
/// so a call is never dropped for want of a price: an item without a rate is simply worth zero
/// until the Billing Manager sets one. Keys are stored on usage rows, so they are never renamed.
/// </summary>
public static class BillingItems
{
    // Identity
    public const string Bvn = "identity:bvn";
    public const string BvnAdvanced = "identity:bvn-advanced";
    public const string Nin = "identity:nin";
    public const string Tin = "identity:tin";
    public const string Cac = "identity:cac";
    public const string CacAdvanced = "identity:cac-advanced";

    // Individual credit bureau
    public const string CrcScore = "bureau:crc-score";
    public const string CrcHistory = "bureau:crc-history";
    public const string CrcFull = "bureau:crc-full";
    public const string FirstCentralSummary = "bureau:firstcentral-summary";
    public const string FirstCentralFull = "bureau:firstcentral-full";
    public const string FirstCentralScore = "bureau:firstcentral-score";
    public const string CreditRegistrySummary = "bureau:creditregistry-summary";
    public const string CreditRegistryFull = "bureau:creditregistry-full";
    public const string CreditRegistryAdvanced = "bureau:creditregistry-advanced";
    public const string Premium = "bureau:premium";

    // Business credit bureau
    public const string BusinessCrc = "business:crc";
    public const string BusinessFirstCentral = "business:firstcentral";
    public const string BusinessPremium = "business:premium";

    // Fraud
    public const string FraudIndividual = "fraud:individual";
    public const string FraudBusiness = "fraud:business";

    // Advisory
    public const string AdvisoryNarrative = "advisory:narrative";

    // Partner API
    public const string PartnerCheck = "partner:check";

    // Platform fees, one per loan event, priced per flow
    public const string LoanReceived = "loan:received";
    public const string LoanBooked = "loan:booked";
    public const string LoanDeclined = "loan:declined";
    public const string LoanCancelled = "loan:cancelled";

    public static readonly IReadOnlyList<BillingItem> All =
    [
        new(Bvn, BillingCategory.Identity, "BVN verification"),
        new(BvnAdvanced, BillingCategory.Identity, "BVN verification (advanced)"),
        new(Nin, BillingCategory.Identity, "NIN verification"),
        new(Tin, BillingCategory.Identity, "TIN verification"),
        new(Cac, BillingCategory.Identity, "CAC lookup"),
        new(CacAdvanced, BillingCategory.Identity, "CAC lookup (advanced, with directors)"),

        new(CrcScore, BillingCategory.CreditBureau, "CRC credit score"),
        new(CrcHistory, BillingCategory.CreditBureau, "CRC credit history"),
        new(CrcFull, BillingCategory.CreditBureau, "CRC full report"),
        new(FirstCentralSummary, BillingCategory.CreditBureau, "First Central summary"),
        new(FirstCentralFull, BillingCategory.CreditBureau, "First Central full report"),
        new(FirstCentralScore, BillingCategory.CreditBureau, "First Central credit score"),
        new(CreditRegistrySummary, BillingCategory.CreditBureau, "Credit Registry summary"),
        new(CreditRegistryFull, BillingCategory.CreditBureau, "Credit Registry full report"),
        new(CreditRegistryAdvanced, BillingCategory.CreditBureau, "Credit Registry advanced report"),
        new(Premium, BillingCategory.CreditBureau, "Premium (all bureaus) report"),

        new(BusinessCrc, BillingCategory.BusinessBureau, "CRC business report"),
        new(BusinessFirstCentral, BillingCategory.BusinessBureau, "First Central business report"),
        new(BusinessPremium, BillingCategory.BusinessBureau, "Premium business report"),

        new(FraudIndividual, BillingCategory.FraudCheck, "Loan fraud check (individual)"),
        new(FraudBusiness, BillingCategory.FraudCheck, "Loan fraud check (business)"),

        new(AdvisoryNarrative, BillingCategory.Advisory, "AI advisory narrative"),

        new(PartnerCheck, BillingCategory.PartnerApi, "Partner customer credit check"),

        new(LoanReceived, BillingCategory.Platform, "Application received", PerFlow: true),
        new(LoanBooked, BillingCategory.Platform, "Loan booked", PerFlow: true),
        new(LoanDeclined, BillingCategory.Platform, "Application declined", PerFlow: true),
        new(LoanCancelled, BillingCategory.Platform, "Application cancelled or withdrawn", PerFlow: true),
    ];

    private static readonly Dictionary<string, BillingItem> ByKey = All.ToDictionary(i => i.Item, StringComparer.Ordinal);

    public static BillingItem? Find(string item) => ByKey.GetValueOrDefault(item);

    public static BillingCategory CategoryOf(string item) =>
        ByKey.TryGetValue(item, out var i) ? i.Category : BillingCategory.Identity;

    public static string DisplayNameOf(string item) =>
        ByKey.TryGetValue(item, out var i) ? i.DisplayName : item;

    /// <summary>The loan flows a platform fee is kept for.</summary>
    public static readonly IReadOnlyList<BillingFlow> LoanFlows = [BillingFlow.Corporate, BillingFlow.Namp, BillingFlow.Rhshf];

    public static readonly IReadOnlyList<string> LoanEventItems = [LoanReceived, LoanBooked, LoanDeclined, LoanCancelled];

    public static string FlowDisplayName(BillingFlow flow) => flow switch
    {
        BillingFlow.Corporate => "Corporate",
        BillingFlow.Namp => "NAMP",
        BillingFlow.Rhshf => "RH-SHF",
        BillingFlow.PartnerApi => "Partner API",
        BillingFlow.Diagnostics => "Diagnostics",
        BillingFlow.Other => "Other lookups",
        _ => "Unattributed",
    };

    public static string CategoryDisplayName(BillingCategory category) => category switch
    {
        BillingCategory.Identity => "Identity",
        BillingCategory.CreditBureau => "Credit bureau",
        BillingCategory.BusinessBureau => "Business bureau",
        BillingCategory.FraudCheck => "Fraud check",
        BillingCategory.Advisory => "AI advisory",
        BillingCategory.PartnerApi => "Partner API",
        _ => "Platform fees",
    };
}
