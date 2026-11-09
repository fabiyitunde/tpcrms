namespace CRMS.Application.Billing;

/// <summary>
/// The <c>Billing</c> configuration section. Everything has a default so a host with no section
/// still bills; the vendor fills in its own details for the statement header in production.
/// </summary>
public sealed class BillingSettings
{
    public const string SectionName = "Billing";

    /// <summary>VAT applied on statements, as a percentage. Nigerian standard rate by default.</summary>
    public decimal VatRatePercent { get; set; } = 7.5m;
    public string Currency { get; set; } = "NGN";
    /// <summary>Who is owed: printed as the issuer of every statement.</summary>
    public string VendorName { get; set; } = "Freewave Consult Ltd";
    public string? VendorAddress { get; set; }
    public string? VendorEmail { get; set; }
    public string? VendorPhone { get; set; }
    public string? VendorTaxId { get; set; }
    /// <summary>Who pays: printed as the addressee.</summary>
    public string BankName { get; set; } = "Bank of Agriculture";
    public string? BankAddress { get; set; }
    /// <summary>Bank account details for settlement, printed at the foot of the statement.</summary>
    public string? PaymentInstructions { get; set; }
    /// <summary>Days after issue by which payment is due; printed as the due date.</summary>
    public int PaymentTermsDays { get; set; } = 30;
    /// <summary>Prefix of statement numbers: PREFIX-yyyyMM-NN.</summary>
    public string StatementPrefix { get; set; } = "BILL";

    public BillingAlertSettings Alerts { get; set; } = new();
}

/// <summary>Thresholds for the billing checks. Each has a default; tune them in the Billing:Alerts section.</summary>
public sealed class BillingAlertSettings
{
    public bool Enabled { get; set; } = true;
    /// <summary>Hours between runs of the checks in each host.</summary>
    public int CheckEveryHours { get; set; } = 6;
    /// <summary>A service is a spike when this month's calls, scaled to a full month, exceed its norm by this factor.</summary>
    public decimal SpikeFactor { get; set; } = 2.0m;
    /// <summary>Spikes are ignored below this many calls this month, so a quiet service cannot spike on two calls.</summary>
    public int SpikeMinCalls { get; set; } = 10;
    /// <summary>How many earlier months make the norm.</summary>
    public int HistoryMonths { get; set; } = 3;
    /// <summary>Failed share of a service's recent calls that raises an alert.</summary>
    public int FailureRatePercent { get; set; } = 50;
    public int FailureMinCalls { get; set; } = 5;
    public int FailureWindowDays { get; set; } = 2;
    /// <summary>Unattributed calls this month at or above this count raise an alert.</summary>
    public int UnattributedMinCalls { get; set; } = 1;
    /// <summary>Day of the month from which an unclosed previous month becomes a warning rather than a note.</summary>
    public int MonthCloseGraceDays { get; set; } = 10;
    /// <summary>Email Billing Managers when a new alert appears.</summary>
    public bool EmailManagers { get; set; } = true;
    /// <summary>Default snooze length from the dashboard, in days.</summary>
    public int SnoozeDays { get; set; } = 7;
}
