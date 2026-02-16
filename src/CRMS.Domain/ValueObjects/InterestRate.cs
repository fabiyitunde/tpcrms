using CRMS.Domain.Common;

namespace CRMS.Domain.ValueObjects;

public enum InterestRateType
{
    Flat,
    Reducing
}

public enum InterestRatePeriod
{
    PerAnnum,
    PerMonth
}

public class InterestRate : ValueObject
{
    public decimal Rate { get; }
    public InterestRateType Type { get; }
    public InterestRatePeriod Period { get; }

    private InterestRate(decimal rate, InterestRateType type, InterestRatePeriod period)
    {
        Rate = rate;
        Type = type;
        Period = period;
    }

    public static Result<InterestRate> Create(decimal rate, InterestRateType type, InterestRatePeriod period)
    {
        if (rate < 0)
            return Result.Failure<InterestRate>("Interest rate cannot be negative");

        if (rate > 100)
            return Result.Failure<InterestRate>("Interest rate cannot exceed 100%");

        return Result.Success(new InterestRate(rate, type, period));
    }

    public decimal ToAnnualRate()
    {
        return Period == InterestRatePeriod.PerAnnum ? Rate : Rate * 12;
    }

    public decimal ToMonthlyRate()
    {
        return Period == InterestRatePeriod.PerMonth ? Rate : Rate / 12;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Rate;
        yield return Type;
        yield return Period;
    }

    public override string ToString() => $"{Rate}% {Period} ({Type})";
}
