using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.CreditBureau;

public class BureauAccount : Entity
{
    public Guid BureauReportId { get; private set; }
    public string AccountNumber { get; private set; } = string.Empty;
    public string? CreditorName { get; private set; }
    public string? AccountType { get; private set; }
    public AccountStatus Status { get; private set; }
    public DelinquencyLevel DelinquencyLevel { get; private set; }
    public decimal CreditLimit { get; private set; }
    public decimal Balance { get; private set; }
    public decimal? MinimumPayment { get; private set; }
    public DateTime? DateOpened { get; private set; }
    public DateTime? DateClosed { get; private set; }
    public DateTime? LastPaymentDate { get; private set; }
    public decimal? LastPaymentAmount { get; private set; }
    public string? PaymentProfile { get; private set; }
    public LegalStatus LegalStatus { get; private set; }
    public DateTime? LegalStatusDate { get; private set; }
    public string? Currency { get; private set; }
    public DateTime LastUpdated { get; private set; }

    private BureauAccount() { }

    public static BureauAccount Create(
        Guid bureauReportId,
        string accountNumber,
        string? creditorName,
        string? accountType,
        AccountStatus status,
        DelinquencyLevel delinquencyLevel,
        decimal creditLimit,
        decimal balance,
        decimal? minimumPayment,
        DateTime? dateOpened,
        DateTime? dateClosed,
        DateTime? lastPaymentDate,
        decimal? lastPaymentAmount,
        string? paymentProfile,
        LegalStatus legalStatus,
        DateTime? legalStatusDate,
        string? currency,
        DateTime lastUpdated)
    {
        return new BureauAccount
        {
            BureauReportId = bureauReportId,
            AccountNumber = accountNumber,
            CreditorName = creditorName,
            AccountType = accountType,
            Status = status,
            DelinquencyLevel = delinquencyLevel,
            CreditLimit = creditLimit,
            Balance = balance,
            MinimumPayment = minimumPayment,
            DateOpened = dateOpened,
            DateClosed = dateClosed,
            LastPaymentDate = lastPaymentDate,
            LastPaymentAmount = lastPaymentAmount,
            PaymentProfile = paymentProfile,
            LegalStatus = legalStatus,
            LegalStatusDate = legalStatusDate,
            Currency = currency,
            LastUpdated = lastUpdated
        };
    }

    public int GetDelinquencyDays()
    {
        return DelinquencyLevel switch
        {
            DelinquencyLevel.Current => 0,
            DelinquencyLevel.Days1To30 => 30,
            DelinquencyLevel.Days31To60 => 60,
            DelinquencyLevel.Days61To90 => 90,
            DelinquencyLevel.Days91To120 => 120,
            DelinquencyLevel.Days121To150 => 150,
            DelinquencyLevel.Days151To180 => 180,
            DelinquencyLevel.Days181To360 => 360,
            DelinquencyLevel.Over360Days => 999,
            _ => 0
        };
    }
}
