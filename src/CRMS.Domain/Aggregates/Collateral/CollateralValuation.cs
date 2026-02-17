using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.ValueObjects;

namespace CRMS.Domain.Aggregates.Collateral;

public class CollateralValuation : Entity
{
    public Guid CollateralId { get; private set; }
    public ValuationType Type { get; private set; }
    public ValuationStatus Status { get; private set; }
    public DateTime ValuationDate { get; private set; }
    public Money MarketValue { get; private set; } = null!;
    public Money? ForcedSaleValue { get; private set; }
    public string? ValuerName { get; private set; }
    public string? ValuerCompany { get; private set; }
    public string? ValuerLicense { get; private set; }
    public string? ValuationReportReference { get; private set; }
    public string? Remarks { get; private set; }
    public DateTime? ExpiryDate { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public DateTime RecordedAt { get; private set; }

    private CollateralValuation() { }

    public static CollateralValuation Create(
        Guid collateralId,
        ValuationType type,
        DateTime valuationDate,
        Money marketValue,
        Money? forcedSaleValue,
        string? valuerName,
        string? valuerCompany,
        string? valuerLicense,
        string? reportReference,
        Guid recordedByUserId,
        string? remarks = null)
    {
        return new CollateralValuation
        {
            CollateralId = collateralId,
            Type = type,
            Status = ValuationStatus.Completed,
            ValuationDate = valuationDate,
            MarketValue = marketValue,
            ForcedSaleValue = forcedSaleValue,
            ValuerName = valuerName,
            ValuerCompany = valuerCompany,
            ValuerLicense = valuerLicense,
            ValuationReportReference = reportReference,
            Remarks = remarks,
            ExpiryDate = valuationDate.AddYears(1),
            RecordedByUserId = recordedByUserId,
            RecordedAt = DateTime.UtcNow
        };
    }

    public bool IsExpired() => ExpiryDate.HasValue && DateTime.UtcNow > ExpiryDate.Value;
}
