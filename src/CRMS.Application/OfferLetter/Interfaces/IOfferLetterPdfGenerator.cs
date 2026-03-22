namespace CRMS.Application.OfferLetter.Interfaces;

/// <summary>
/// Interface for generating offer letter PDFs.
/// </summary>
public interface IOfferLetterPdfGenerator
{
    /// <summary>
    /// Generates an offer letter PDF from the provided data.
    /// </summary>
    /// <param name="data">All data required to generate the offer letter</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>PDF document as byte array</returns>
    Task<byte[]> GenerateAsync(OfferLetterData data, CancellationToken ct = default);
}

public record OfferLetterData(
    string ApplicationNumber,
    DateTime GeneratedDate,
    string CustomerName,
    string CustomerAddress,
    string ProductName,
    decimal ApprovedAmount,
    string Currency,
    int TenorMonths,
    decimal InterestRatePerAnnum,
    string RepaymentFrequency,
    string AmortizationMethod,
    List<ScheduleInstallmentData> RepaymentSchedule,
    decimal TotalPrincipal,
    decimal TotalInterest,
    decimal TotalRepayment,
    decimal MonthlyInstallment,
    List<string> Conditions,
    string BankName,
    string BranchName,
    string ScheduleSource,
    int Version
);

public record ScheduleInstallmentData(
    int InstallmentNumber,
    DateTime DueDate,
    decimal Principal,
    decimal Interest,
    decimal TotalPayment,
    decimal OutstandingBalance
);
