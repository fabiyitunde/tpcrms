namespace CRMS.Application.Namp.Interfaces;

public interface INampLeaseAgreementPdfGenerator
{
    Task<byte[]> GenerateAsync(NampAgreementDocumentData data, CancellationToken ct = default);
}

public interface INampGpsConsentFormPdfGenerator
{
    Task<byte[]> GenerateAsync(NampAgreementDocumentData data, CancellationToken ct = default);
}

public record NampAgreementDocumentData(
    string ApplicationNumber,
    string DocumentTitle,
    DateTime GeneratedDate,
    string ApplicantName,
    string BoaAccountNumber,
    string EquipmentDescription,
    decimal EquipmentValue,
    decimal? LoanAmount,
    decimal? EquityAmount,
    int? TenorMonths,
    decimal InterestRatePerAnnum,
    decimal MonthlyInstallment,
    string BankName,
    string BranchName,
    // Template-rendered content (placeholders already substituted)
    string RenderedBody
);
