namespace CRMS.Application.ProductCatalog.DTOs;

public record LoanProductDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string Type,
    decimal MinAmount,
    decimal MaxAmount,
    string Currency,
    int MinTenorMonths,
    int MaxTenorMonths,
    string Status,
    DateTime CreatedAt,
    List<PricingTierDto> PricingTiers,
    List<EligibilityRuleDto> EligibilityRules,
    List<DocumentRequirementDto> DocumentRequirements
);

public record LoanProductSummaryDto(
    Guid Id,
    string Code,
    string Name,
    string Type,
    decimal MinAmount,
    decimal MaxAmount,
    string Currency,
    string Status
);

public record PricingTierDto(
    Guid Id,
    string Name,
    decimal InterestRatePerAnnum,
    string RateType,
    decimal? ProcessingFeePercent,
    decimal? ProcessingFeeFixed,
    string? ProcessingFeeCurrency,
    int? MinCreditScore,
    int? MaxCreditScore
);

public record EligibilityRuleDto(
    Guid Id,
    string RuleType,
    string FieldName,
    string Operator,
    string Value,
    bool IsHardRule,
    string FailureMessage
);

public record DocumentRequirementDto(
    Guid Id,
    string DocumentType,
    string Name,
    bool IsMandatory,
    int? MaxFileSizeMB,
    string? AllowedExtensions
);
