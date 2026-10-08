namespace CRMS.Application.Rhshf.Interfaces;

/// <summary>
/// Generates the Key Facts Statement (KFS) that accompanies the RH-SHF offer letter — a one-page
/// plain-language summary of the facility the FAC signs and returns alongside the signed offer.
/// RH-SHF repays as a single bullet at harvest (the crop cycle is the tenor), so there is no
/// amortisation schedule — the repayment section states the one amount due at harvest.
/// </summary>
public interface IRhshfKfsPdfGenerator
{
    Task<byte[]> GenerateAsync(RhshfKfsData data, CancellationToken ct = default);
}

public record RhshfKfsData(
    string Reference,
    string CompanyName,
    string RcNumber,
    string ProgrammeName,
    string SessionName,
    decimal ApprovedAmount,
    string Currency,
    DateTime GeneratedDate,
    string BankName,
    /// <summary>Facility terms from the credit appraisal — null when no appraisal was recorded, in
    /// which case the KFS states the figure is per the facility terms rather than inventing one.</summary>
    decimal? InterestRatePercent,
    int? CycleMonths,
    decimal? AmountDueAtHarvest);
