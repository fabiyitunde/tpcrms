using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Collateral;

/// <summary>
/// Configurable collateral type with bank-defined haircut rates.
/// Replaces the hardcoded CollateralType enum for admin-managed setup.
/// </summary>
public class CollateralTypeConfig : Entity
{
    /// <summary>Display name shown in UI dropdowns e.g. "Residential Property"</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Short machine-readable code matching the legacy CollateralType enum value for migration mapping.</summary>
    public string Code { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>
    /// Haircut rate as a decimal fraction e.g. 0.30 = 30%.
    /// AcceptableValue = ValuationBase × (1 − HaircutRate).
    /// </summary>
    public decimal HaircutRate { get; private set; }

    /// <summary>"MarketValue" or "FSV" — which figure the haircut is applied to.</summary>
    public string ValuationBasis { get; private set; } = "MarketValue";

    public bool IsActive { get; private set; } = true;

    public int SortOrder { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public Guid? ModifiedByUserId { get; private set; }

    private CollateralTypeConfig() { }

    public static Result<CollateralTypeConfig> Create(
        string name,
        string code,
        decimal haircutRate,
        string valuationBasis,
        Guid createdByUserId,
        string? description = null,
        int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<CollateralTypeConfig>("Collateral type name is required");

        if (string.IsNullOrWhiteSpace(code))
            return Result.Failure<CollateralTypeConfig>("Collateral type code is required");

        if (haircutRate < 0 || haircutRate > 1)
            return Result.Failure<CollateralTypeConfig>("Haircut rate must be between 0 and 1 (e.g. 0.30 for 30%)");

        if (valuationBasis != "MarketValue" && valuationBasis != "FSV")
            return Result.Failure<CollateralTypeConfig>("Valuation basis must be 'MarketValue' or 'FSV'");

        return Result.Success(new CollateralTypeConfig
        {
            Name = name.Trim(),
            Code = code.Trim(),
            Description = description?.Trim(),
            HaircutRate = haircutRate,
            ValuationBasis = valuationBasis,
            IsActive = true,
            SortOrder = sortOrder,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        });
    }

    public Result Update(string name, decimal haircutRate, string valuationBasis, Guid modifiedByUserId, string? description = null, int? sortOrder = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure("Collateral type name is required");

        if (haircutRate < 0 || haircutRate > 1)
            return Result.Failure("Haircut rate must be between 0 and 1");

        if (valuationBasis != "MarketValue" && valuationBasis != "FSV")
            return Result.Failure("Valuation basis must be 'MarketValue' or 'FSV'");

        Name = name.Trim();
        Description = description?.Trim();
        HaircutRate = haircutRate;
        ValuationBasis = valuationBasis;
        if (sortOrder.HasValue) SortOrder = sortOrder.Value;
        ModifiedByUserId = modifiedByUserId;
        ModifiedAt = DateTime.UtcNow;

        return Result.Success();
    }

    public void Deactivate(Guid modifiedByUserId)
    {
        IsActive = false;
        ModifiedByUserId = modifiedByUserId;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Activate(Guid modifiedByUserId)
    {
        IsActive = true;
        ModifiedByUserId = modifiedByUserId;
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>Computes acceptable value from the given market value and FSV based on this type's configuration.</summary>
    public decimal ComputeAcceptableValue(decimal marketValue, decimal? forcedSaleValue)
    {
        var basis = ValuationBasis == "FSV" && forcedSaleValue.HasValue
            ? forcedSaleValue.Value
            : marketValue;
        return basis * (1 - HaircutRate);
    }
}
