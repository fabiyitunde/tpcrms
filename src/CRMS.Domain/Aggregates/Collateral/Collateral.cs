using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.ValueObjects;

namespace CRMS.Domain.Aggregates.Collateral;

public class Collateral : AggregateRoot
{
    public Guid LoanApplicationId { get; private set; }
    public string CollateralReference { get; private set; } = string.Empty;
    public CollateralType Type { get; private set; }
    public CollateralStatus Status { get; private set; }
    public PerfectionStatus PerfectionStatus { get; private set; }
    
    // Asset Details
    public string Description { get; private set; } = string.Empty;
    public string? AssetIdentifier { get; private set; } // Reg number, title deed, certificate number
    public string? Location { get; private set; }
    public string? OwnerName { get; private set; }
    public string? OwnershipType { get; private set; } // Sole, Joint, Corporate
    
    // Valuation
    public Money? MarketValue { get; private set; }
    public Money? ForcedSaleValue { get; private set; }
    public decimal HaircutPercentage { get; private set; }
    public Money? AcceptableValue { get; private set; } // After haircut
    public DateTime? LastValuationDate { get; private set; }
    public DateTime? NextRevaluationDue { get; private set; }
    
    // Lien Details
    public LienType? LienType { get; private set; }
    public string? LienReference { get; private set; }
    public DateTime? LienRegistrationDate { get; private set; }
    public string? LienRegistrationAuthority { get; private set; }
    
    // Insurance
    public bool IsInsured { get; private set; }
    public string? InsurancePolicyNumber { get; private set; }
    public string? InsuranceCompany { get; private set; }
    public Money? InsuredValue { get; private set; }
    public DateTime? InsuranceExpiryDate { get; private set; }
    
    // Audit
    public Guid CreatedByUserId { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public Guid? RejectedByUserId { get; private set; }
    public DateTime? RejectedAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? Notes { get; private set; }

    private readonly List<CollateralValuation> _valuations = [];
    private readonly List<CollateralDocument> _documents = [];

    public IReadOnlyCollection<CollateralValuation> Valuations => _valuations.AsReadOnly();
    public IReadOnlyCollection<CollateralDocument> Documents => _documents.AsReadOnly();

    private Collateral() { }

    public static Result<Collateral> Create(
        Guid loanApplicationId,
        CollateralType type,
        string description,
        Guid createdByUserId,
        string? assetIdentifier = null,
        string? location = null,
        string? ownerName = null,
        string? ownershipType = null)
    {
        if (loanApplicationId == Guid.Empty)
            return Result.Failure<Collateral>("Loan application ID is required");

        if (string.IsNullOrWhiteSpace(description))
            return Result.Failure<Collateral>("Collateral description is required");

        var collateral = new Collateral
        {
            LoanApplicationId = loanApplicationId,
            CollateralReference = GenerateReference(type),
            Type = type,
            Status = CollateralStatus.Proposed,
            PerfectionStatus = PerfectionStatus.NotStarted,
            Description = description,
            AssetIdentifier = assetIdentifier,
            Location = location,
            OwnerName = ownerName,
            OwnershipType = ownershipType,
            HaircutPercentage = GetDefaultHaircut(type),
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };

        collateral.AddDomainEvent(new CollateralProposedEvent(collateral.Id, loanApplicationId, type, description));

        return Result.Success(collateral);
    }

    private static string GenerateReference(CollateralType type)
    {
        var prefix = type switch
        {
            CollateralType.RealEstate => "RE",
            CollateralType.Vehicle => "VH",
            CollateralType.Equipment => "EQ",
            CollateralType.CashDeposit or CollateralType.FixedDeposit => "CD",
            CollateralType.Stocks or CollateralType.Bonds => "SC",
            _ => "CL"
        };
        return $"{prefix}{DateTime.UtcNow:yyyyMMddHHmmss}{Guid.NewGuid().ToString()[..4].ToUpper()}";
    }

    private static decimal GetDefaultHaircut(CollateralType type)
    {
        return type switch
        {
            CollateralType.CashDeposit => 0,
            CollateralType.FixedDeposit => 5,
            CollateralType.TreasuryBills => 5,
            CollateralType.Bonds => 10,
            CollateralType.Stocks => 30,
            CollateralType.RealEstate => 20,
            CollateralType.Vehicle => 30,
            CollateralType.Equipment => 40,
            CollateralType.Inventory => 50,
            _ => 40
        };
    }

    public Result SetValuation(Money marketValue, Money? forcedSaleValue, decimal? haircutPercentage = null)
    {
        if (marketValue.Amount <= 0)
            return Result.Failure("Market value must be greater than zero");

        MarketValue = marketValue;
        ForcedSaleValue = forcedSaleValue ?? Money.Create(marketValue.Amount * 0.7m, marketValue.Currency);
        
        if (haircutPercentage.HasValue)
            HaircutPercentage = haircutPercentage.Value;

        AcceptableValue = Money.Create(marketValue.Amount * (1 - HaircutPercentage / 100), marketValue.Currency);
        LastValuationDate = DateTime.UtcNow;
        NextRevaluationDue = DateTime.UtcNow.AddYears(1);
        Status = CollateralStatus.Valued;

        return Result.Success();
    }

    public Result Approve(Guid approvedByUserId)
    {
        if (Status != CollateralStatus.Valued)
            return Result.Failure("Collateral must be valued before approval");

        if (MarketValue == null || MarketValue.Amount <= 0)
            return Result.Failure("Collateral must have a valid market value");

        Status = CollateralStatus.Approved;
        ApprovedByUserId = approvedByUserId;
        ApprovedAt = DateTime.UtcNow;

        AddDomainEvent(new CollateralApprovedEvent(Id, LoanApplicationId, AcceptableValue!));

        return Result.Success();
    }

    public Result Reject(Guid rejectedByUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Rejection reason is required");

        Status = CollateralStatus.Rejected;
        RejectedByUserId = rejectedByUserId;
        RejectedAt = DateTime.UtcNow;
        RejectionReason = reason;

        return Result.Success();
    }

    public Result RecordPerfection(LienType lienType, string lienReference, string registrationAuthority, DateTime? registrationDate = null)
    {
        if (Status != CollateralStatus.Approved)
            return Result.Failure("Collateral must be approved before perfection");

        LienType = lienType;
        LienReference = lienReference;
        LienRegistrationAuthority = registrationAuthority;
        LienRegistrationDate = registrationDate ?? DateTime.UtcNow;
        PerfectionStatus = PerfectionStatus.Perfected;
        Status = CollateralStatus.Perfected;

        AddDomainEvent(new CollateralPerfectedEvent(Id, LoanApplicationId, lienType, lienReference));

        return Result.Success();
    }

    public Result RecordInsurance(string policyNumber, string company, Money insuredValue, DateTime expiryDate)
    {
        if (string.IsNullOrWhiteSpace(policyNumber))
            return Result.Failure("Insurance policy number is required");

        IsInsured = true;
        InsurancePolicyNumber = policyNumber;
        InsuranceCompany = company;
        InsuredValue = insuredValue;
        InsuranceExpiryDate = expiryDate;

        return Result.Success();
    }

    public Result Release(string reason)
    {
        if (Status != CollateralStatus.Perfected && Status != CollateralStatus.Approved)
            return Result.Failure("Only perfected or approved collateral can be released");

        Status = CollateralStatus.Released;
        Notes = reason;
        PerfectionStatus = PerfectionStatus.NotStarted;

        AddDomainEvent(new CollateralReleasedEvent(Id, LoanApplicationId, reason));

        return Result.Success();
    }

    public void AddValuation(CollateralValuation valuation)
    {
        _valuations.Add(valuation);
    }

    public void AddDocument(CollateralDocument document)
    {
        _documents.Add(document);
    }

    public void RemoveDocument(CollateralDocument document)
    {
        _documents.Remove(document);
    }

    public Result UpdateBasicInfo(CollateralType type, string description, string? assetIdentifier, 
        string? location, string? ownerName, string? ownershipType)
    {
        if (Status != CollateralStatus.Proposed && Status != CollateralStatus.UnderValuation)
            return Result.Failure("Can only update collateral in Proposed or UnderValuation status");

        if (string.IsNullOrWhiteSpace(description))
            return Result.Failure("Description is required");

        Type = type;
        Description = description;
        AssetIdentifier = assetIdentifier;
        Location = location;
        OwnerName = ownerName;
        OwnershipType = ownershipType;
        HaircutPercentage = GetDefaultHaircut(type);
        ModifiedAt = DateTime.UtcNow;

        return Result.Success();
    }

    public decimal CalculateLTV(Money loanAmount)
    {
        if (AcceptableValue == null || AcceptableValue.Amount == 0)
            return 100;

        return (loanAmount.Amount / AcceptableValue.Amount) * 100;
    }
}

// Domain Events
public record CollateralProposedEvent(Guid CollateralId, Guid LoanApplicationId, CollateralType Type, string Description) : DomainEvent;
public record CollateralApprovedEvent(Guid CollateralId, Guid LoanApplicationId, Money AcceptableValue) : DomainEvent;
public record CollateralPerfectedEvent(Guid CollateralId, Guid LoanApplicationId, LienType LienType, string LienReference) : DomainEvent;
public record CollateralReleasedEvent(Guid CollateralId, Guid LoanApplicationId, string Reason) : DomainEvent;
