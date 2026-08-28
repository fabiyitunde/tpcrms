namespace CRMS.Application.Rhshf.Interfaces;

/// <summary>
/// Generates the RH-SHF offer document (design doc §3.6, Phase 6). Own, independent generator —
/// mirrors the shape of concern in NampOfferLetterPdfGenerator/OfferLetterPdfGenerator, but RH-SHF's
/// "offer" is a credit-profiling approval for input financing, not a term loan with an amortization
/// schedule, so it doesn't fit their OfferLetterData shape (tenor/interest-rate/repayment schedule
/// fields that don't exist for this product) — a separate, much simpler data shape instead.
/// </summary>
public interface IRhshfOfferLetterPdfGenerator
{
    Task<byte[]> GenerateAsync(RhshfOfferLetterData data, CancellationToken ct = default);
}

public record RhshfOfferLetterData(
    string Reference,
    string CompanyName,
    string RcNumber,
    string ProgrammeName,
    string SessionName,
    decimal ApprovedAmount,
    string Currency,
    DateTime GeneratedDate,
    string BankName);
