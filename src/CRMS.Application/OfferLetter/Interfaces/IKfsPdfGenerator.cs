namespace CRMS.Application.OfferLetter.Interfaces;

public interface IKfsPdfGenerator
{
    Task<byte[]> GenerateAsync(KfsData data, CancellationToken ct = default);
}

public record KfsData(
    string ApplicationNumber,
    DateTime GeneratedDate,
    string CustomerName,
    string ProductName,
    decimal LoanAmount,
    string Currency,
    int TenorMonths,
    decimal NominalRatePerAnnum,
    decimal EffectiveAnnualRate,
    decimal MonthlyInstallment,
    decimal TotalInterest,
    decimal TotalRepayment,
    decimal ProcessingFeeAmount,
    decimal ManagementFeeAmount,
    decimal TotalCostOfCredit,
    string LatePaymentPenalty,
    string EarlyRepaymentTerms,
    string SecurityRequired,
    string BankName,
    string BranchName,
    string ComplaintChannel
);
