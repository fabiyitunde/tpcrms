using CRMS.Domain.Common;
using CRMS.Domain.ValueObjects;

namespace CRMS.Domain.Aggregates.ProductCatalog;

public class PricingTier : Entity
{
    public Guid LoanProductId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal InterestRatePerAnnum { get; private set; }
    public InterestRateType RateType { get; private set; }
    public decimal? ProcessingFeePercent { get; private set; }
    public Money? ProcessingFeeFixed { get; private set; }
    public int? MinCreditScore { get; private set; }
    public int? MaxCreditScore { get; private set; }

    private PricingTier() { }

    internal static Result<PricingTier> Create(
        Guid loanProductId,
        string name,
        decimal interestRatePerAnnum,
        InterestRateType rateType,
        decimal? processingFeePercent,
        Money? processingFeeFixed,
        int? minCreditScore,
        int? maxCreditScore)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<PricingTier>("Pricing tier name is required");

        if (interestRatePerAnnum < 0)
            return Result.Failure<PricingTier>("Interest rate cannot be negative");

        if (interestRatePerAnnum > 100)
            return Result.Failure<PricingTier>("Interest rate cannot exceed 100%");

        if (processingFeePercent.HasValue && (processingFeePercent < 0 || processingFeePercent > 100))
            return Result.Failure<PricingTier>("Processing fee percentage must be between 0 and 100");

        if (minCreditScore.HasValue && maxCreditScore.HasValue && minCreditScore > maxCreditScore)
            return Result.Failure<PricingTier>("Minimum credit score cannot be greater than maximum");

        return Result.Success(new PricingTier
        {
            LoanProductId = loanProductId,
            Name = name,
            InterestRatePerAnnum = interestRatePerAnnum,
            RateType = rateType,
            ProcessingFeePercent = processingFeePercent,
            ProcessingFeeFixed = processingFeeFixed,
            MinCreditScore = minCreditScore,
            MaxCreditScore = maxCreditScore
        });
    }

    public Result Update(
        string name,
        decimal interestRatePerAnnum,
        InterestRateType rateType,
        decimal? processingFeePercent,
        Money? processingFeeFixed,
        int? minCreditScore,
        int? maxCreditScore)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure("Pricing tier name is required");

        if (interestRatePerAnnum < 0)
            return Result.Failure("Interest rate cannot be negative");

        if (interestRatePerAnnum > 100)
            return Result.Failure("Interest rate cannot exceed 100%");

        if (processingFeePercent.HasValue && (processingFeePercent < 0 || processingFeePercent > 100))
            return Result.Failure("Processing fee percentage must be between 0 and 100");

        if (minCreditScore.HasValue && maxCreditScore.HasValue && minCreditScore > maxCreditScore)
            return Result.Failure("Minimum credit score cannot be greater than maximum");

        Name = name;
        InterestRatePerAnnum = interestRatePerAnnum;
        RateType = rateType;
        ProcessingFeePercent = processingFeePercent;
        ProcessingFeeFixed = processingFeeFixed;
        MinCreditScore = minCreditScore;
        MaxCreditScore = maxCreditScore;

        return Result.Success();
    }

    public bool IsApplicableForCreditScore(int creditScore)
    {
        if (!MinCreditScore.HasValue && !MaxCreditScore.HasValue)
            return true;

        if (MinCreditScore.HasValue && creditScore < MinCreditScore.Value)
            return false;

        if (MaxCreditScore.HasValue && creditScore > MaxCreditScore.Value)
            return false;

        return true;
    }
}
