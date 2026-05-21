using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Namp;

/// <summary>
/// A collateral asset submitted via the NAMP webhook payload and unpacked at Loan Officer recall time.
/// </summary>
public class NampCollateral : Entity
{
    public Guid NampApplicationId { get; private set; }

    // ── Asset Details ──────────────────────────────────────────────────────
    public string CollateralType { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? AssetIdentifier { get; private set; }
    public string? Location { get; private set; }
    public string? OwnerName { get; private set; }
    public string? OwnershipType { get; private set; }

    // ── Valuation ──────────────────────────────────────────────────────────
    public decimal? MarketValue { get; private set; }
    public decimal? ForcedSaleValue { get; private set; }

    // ── Lien ───────────────────────────────────────────────────────────────
    public string? LienType { get; private set; }
    public string? LienReference { get; private set; }
    public string? LienRegistrationAuthority { get; private set; }

    // ── Insurance ──────────────────────────────────────────────────────────
    public bool IsInsured { get; private set; }
    public string? InsurancePolicyNumber { get; private set; }
    public string? InsuranceCompany { get; private set; }
    public decimal? InsuredValue { get; private set; }
    public string? InsuranceExpiryDate { get; private set; }

    private NampCollateral() { }

    public static NampCollateral Create(
        Guid nampApplicationId,
        string collateralType,
        string description,
        string? assetIdentifier,
        string? location,
        string? ownerName,
        string? ownershipType,
        decimal? marketValue,
        decimal? forcedSaleValue,
        string? lienType,
        string? lienReference,
        string? lienRegistrationAuthority,
        bool isInsured,
        string? insurancePolicyNumber,
        string? insuranceCompany,
        decimal? insuredValue,
        string? insuranceExpiryDate)
    {
        return new NampCollateral
        {
            NampApplicationId = nampApplicationId,
            CollateralType = collateralType,
            Description = description,
            AssetIdentifier = assetIdentifier,
            Location = location,
            OwnerName = ownerName,
            OwnershipType = ownershipType,
            MarketValue = marketValue,
            ForcedSaleValue = forcedSaleValue,
            LienType = lienType,
            LienReference = lienReference,
            LienRegistrationAuthority = lienRegistrationAuthority,
            IsInsured = isInsured,
            InsurancePolicyNumber = insurancePolicyNumber,
            InsuranceCompany = insuranceCompany,
            InsuredValue = insuredValue,
            InsuranceExpiryDate = insuranceExpiryDate,
        };
    }
}
