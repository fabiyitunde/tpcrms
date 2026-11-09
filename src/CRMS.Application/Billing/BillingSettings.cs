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
}
