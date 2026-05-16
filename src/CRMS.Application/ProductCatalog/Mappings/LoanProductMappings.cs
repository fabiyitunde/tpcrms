using CRMS.Application.OfferAcceptance.DTOs;
using CRMS.Application.ProductCatalog.DTOs;
using CRMS.Domain.Aggregates.ProductCatalog;

namespace CRMS.Application.ProductCatalog.Mappings;

public static class LoanProductMappings
{
    public static LoanProductDto ToDto(this LoanProduct product)
    {
        return new LoanProductDto(
            product.Id,
            product.Code,
            product.Name,
            product.Description,
            product.Type.ToString(),
            product.MinAmount.Amount,
            product.MaxAmount.Amount,
            product.MinAmount.Currency,
            product.MinTenorMonths,
            product.MaxTenorMonths,
            product.Status.ToString(),
            product.CreatedAt,
            product.FineractProductId,
            product.PricingTiers.Select(t => t.ToDto()).ToList(),
            product.EligibilityRules.Select(r => r.ToDto()).ToList(),
            product.DocumentRequirements.Select(d => d.ToDto()).ToList(),
            product.DisbursementChecklist.OrderBy(c => c.SortOrder).Select(c => c.ToChecklistTemplateItemDto()).ToList()
        );
    }

    public static LoanProductSummaryDto ToSummaryDto(this LoanProduct product)
    {
        var baseRate = product.PricingTiers.FirstOrDefault()?.InterestRatePerAnnum ?? 0m;
        return new LoanProductSummaryDto(
            product.Id,
            product.Code,
            product.Name,
            product.Type.ToString(),
            product.MinAmount.Amount,
            product.MaxAmount.Amount,
            product.MinAmount.Currency,
            product.Status.ToString(),
            product.MinTenorMonths,
            product.MaxTenorMonths,
            baseRate,
            product.FineractProductId
        );
    }

    public static PricingTierDto ToDto(this PricingTier tier)
    {
        return new PricingTierDto(
            tier.Id,
            tier.Name,
            tier.InterestRatePerAnnum,
            tier.RateType.ToString(),
            tier.ProcessingFeePercent,
            tier.ProcessingFeeFixed?.Amount,
            tier.ProcessingFeeFixed?.Currency,
            tier.MinCreditScore,
            tier.MaxCreditScore
        );
    }

    public static EligibilityRuleDto ToDto(this EligibilityRule rule)
    {
        return new EligibilityRuleDto(
            rule.Id,
            rule.RuleType.ToString(),
            rule.FieldName,
            rule.Operator.ToString(),
            rule.Value,
            rule.IsHardRule,
            rule.FailureMessage
        );
    }

    public static DocumentRequirementDto ToDto(this DocumentRequirement doc)
    {
        return new DocumentRequirementDto(
            doc.Id,
            doc.DocumentType.ToString(),
            doc.Name,
            doc.IsMandatory,
            doc.MaxFileSizeMB,
            doc.AllowedExtensions
        );
    }

    public static ChecklistTemplateItemDto ToChecklistTemplateItemDto(this DisbursementChecklistTemplate item)
    {
        return new ChecklistTemplateItemDto(
            item.Id,
            item.LoanProductId,
            item.ItemName,
            item.Description,
            item.IsMandatory,
            item.ConditionType.ToString(),
            item.SubsequentDueDays,
            item.RequiresDocumentUpload,
            item.RequiresLegalRatification,
            item.CanBeWaived,
            item.SortOrder,
            item.IsActive
        );
    }
}
