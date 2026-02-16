using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.ValueObjects;

namespace CRMS.Domain.Aggregates.ProductCatalog;

public class LoanProduct : AggregateRoot
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public LoanProductType Type { get; private set; }
    public Money MinAmount { get; private set; } = null!;
    public Money MaxAmount { get; private set; } = null!;
    public int MinTenorMonths { get; private set; }
    public int MaxTenorMonths { get; private set; }
    public ProductStatus Status { get; private set; }

    private readonly List<PricingTier> _pricingTiers = [];
    private readonly List<EligibilityRule> _eligibilityRules = [];
    private readonly List<DocumentRequirement> _documentRequirements = [];

    public IReadOnlyCollection<PricingTier> PricingTiers => _pricingTiers.AsReadOnly();
    public IReadOnlyCollection<EligibilityRule> EligibilityRules => _eligibilityRules.AsReadOnly();
    public IReadOnlyCollection<DocumentRequirement> DocumentRequirements => _documentRequirements.AsReadOnly();

    private LoanProduct() { }

    public static Result<LoanProduct> Create(
        string code,
        string name,
        string description,
        LoanProductType type,
        Money minAmount,
        Money maxAmount,
        int minTenorMonths,
        int maxTenorMonths)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result.Failure<LoanProduct>("Product code is required");

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<LoanProduct>("Product name is required");

        if (minAmount.IsGreaterThan(maxAmount))
            return Result.Failure<LoanProduct>("Minimum amount cannot be greater than maximum amount");

        if (minTenorMonths <= 0)
            return Result.Failure<LoanProduct>("Minimum tenor must be greater than 0");

        if (maxTenorMonths < minTenorMonths)
            return Result.Failure<LoanProduct>("Maximum tenor cannot be less than minimum tenor");

        var product = new LoanProduct
        {
            Code = code.ToUpperInvariant(),
            Name = name,
            Description = description,
            Type = type,
            MinAmount = minAmount,
            MaxAmount = maxAmount,
            MinTenorMonths = minTenorMonths,
            MaxTenorMonths = maxTenorMonths,
            Status = ProductStatus.Draft
        };

        product.AddDomainEvent(new LoanProductCreatedEvent(product.Id, product.Code, product.Name));

        return Result.Success(product);
    }

    public Result Update(string name, string description, Money minAmount, Money maxAmount, int minTenorMonths, int maxTenorMonths)
    {
        if (Status == ProductStatus.Discontinued)
            return Result.Failure("Cannot update a discontinued product");

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure("Product name is required");

        if (minAmount.IsGreaterThan(maxAmount))
            return Result.Failure("Minimum amount cannot be greater than maximum amount");

        if (minTenorMonths <= 0)
            return Result.Failure("Minimum tenor must be greater than 0");

        if (maxTenorMonths < minTenorMonths)
            return Result.Failure("Maximum tenor cannot be less than minimum tenor");

        Name = name;
        Description = description;
        MinAmount = minAmount;
        MaxAmount = maxAmount;
        MinTenorMonths = minTenorMonths;
        MaxTenorMonths = maxTenorMonths;

        return Result.Success();
    }

    public Result Activate()
    {
        if (Status == ProductStatus.Discontinued)
            return Result.Failure("Cannot activate a discontinued product");

        if (!_pricingTiers.Any())
            return Result.Failure("Product must have at least one pricing tier before activation");

        Status = ProductStatus.Active;
        AddDomainEvent(new LoanProductActivatedEvent(Id, Code));
        return Result.Success();
    }

    public Result Suspend()
    {
        if (Status != ProductStatus.Active)
            return Result.Failure("Only active products can be suspended");

        Status = ProductStatus.Suspended;
        return Result.Success();
    }

    public Result Discontinue()
    {
        Status = ProductStatus.Discontinued;
        return Result.Success();
    }

    public Result<PricingTier> AddPricingTier(
        string name,
        decimal interestRatePerAnnum,
        InterestRateType rateType,
        decimal? processingFeePercent,
        Money? processingFeeFixed,
        int? minCreditScore,
        int? maxCreditScore)
    {
        if (Status == ProductStatus.Discontinued)
            return Result.Failure<PricingTier>("Cannot modify a discontinued product");

        var tierResult = PricingTier.Create(
            Id, name, interestRatePerAnnum, rateType,
            processingFeePercent, processingFeeFixed,
            minCreditScore, maxCreditScore);

        if (tierResult.IsFailure)
            return tierResult;

        _pricingTiers.Add(tierResult.Value);
        return tierResult;
    }

    public Result RemovePricingTier(Guid tierId)
    {
        if (Status == ProductStatus.Discontinued)
            return Result.Failure("Cannot modify a discontinued product");

        var tier = _pricingTiers.FirstOrDefault(t => t.Id == tierId);
        if (tier == null)
            return Result.Failure("Pricing tier not found");

        if (Status == ProductStatus.Active && _pricingTiers.Count == 1)
            return Result.Failure("Active product must have at least one pricing tier");

        _pricingTiers.Remove(tier);
        return Result.Success();
    }

    public Result<EligibilityRule> AddEligibilityRule(
        EligibilityRuleType ruleType,
        string fieldName,
        ComparisonOperator comparisonOperator,
        string value,
        bool isHardRule,
        string failureMessage)
    {
        if (Status == ProductStatus.Discontinued)
            return Result.Failure<EligibilityRule>("Cannot modify a discontinued product");

        var ruleResult = EligibilityRule.Create(
            Id, ruleType, fieldName, comparisonOperator,
            value, isHardRule, failureMessage);

        if (ruleResult.IsFailure)
            return ruleResult;

        _eligibilityRules.Add(ruleResult.Value);
        return ruleResult;
    }

    public Result RemoveEligibilityRule(Guid ruleId)
    {
        if (Status == ProductStatus.Discontinued)
            return Result.Failure("Cannot modify a discontinued product");

        var rule = _eligibilityRules.FirstOrDefault(r => r.Id == ruleId);
        if (rule == null)
            return Result.Failure("Eligibility rule not found");

        _eligibilityRules.Remove(rule);
        return Result.Success();
    }

    public Result<DocumentRequirement> AddDocumentRequirement(
        DocumentType documentType,
        string name,
        bool isMandatory,
        int? maxFileSizeMB,
        string? allowedExtensions)
    {
        if (Status == ProductStatus.Discontinued)
            return Result.Failure<DocumentRequirement>("Cannot modify a discontinued product");

        if (_documentRequirements.Any(d => d.DocumentType == documentType))
            return Result.Failure<DocumentRequirement>($"Document requirement for {documentType} already exists");

        var reqResult = DocumentRequirement.Create(
            Id, documentType, name, isMandatory,
            maxFileSizeMB, allowedExtensions);

        if (reqResult.IsFailure)
            return reqResult;

        _documentRequirements.Add(reqResult.Value);
        return reqResult;
    }

    public Result RemoveDocumentRequirement(Guid requirementId)
    {
        if (Status == ProductStatus.Discontinued)
            return Result.Failure("Cannot modify a discontinued product");

        var req = _documentRequirements.FirstOrDefault(r => r.Id == requirementId);
        if (req == null)
            return Result.Failure("Document requirement not found");

        _documentRequirements.Remove(req);
        return Result.Success();
    }
}

public record LoanProductCreatedEvent(Guid ProductId, string Code, string Name) : DomainEvent;
public record LoanProductActivatedEvent(Guid ProductId, string Code) : DomainEvent;
