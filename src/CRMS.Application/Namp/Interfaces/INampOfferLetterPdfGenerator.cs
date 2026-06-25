using CRMS.Application.OfferLetter.Interfaces;

namespace CRMS.Application.Namp.Interfaces;

public interface INampOfferLetterPdfGenerator
{
    Task<byte[]> GenerateAsync(NampOfferLetterData data, CancellationToken ct = default);
}

public record NampOfferLetterData(
    string ApplicationNumber,
    string ApplicationReference,
    DateTime GeneratedDate,
    string ApplicantName,
    string BoaAccountNumber,
    string ApplicantCategory,
    string EquipmentDescription,
    decimal EquipmentValue,
    decimal? LoanAmount,
    decimal? EquityAmount,
    decimal? EquityPercent,
    int? TenorMonths,
    decimal InterestRatePerAnnum,
    string? LoanPurpose,
    string? CommitteeConditions,
    string BankName,
    string BranchName,
    // Repayment schedule (populated from Fineract at ratification)
    List<ScheduleInstallmentData> RepaymentSchedule,
    decimal TotalPrincipal,
    decimal TotalInterest,
    decimal TotalRepayment,
    decimal MonthlyInstallment,
    string ScheduleSource,
    // Template-rendered content (placeholders already substituted; null = use hardcoded defaults)
    string? RenderedIntro = null,
    string? RenderedConditions = null
);
