using CRMS.Domain.Common;

namespace CRMS.Domain.ValueObjects;

public class BVN : ValueObject
{
    public string Value { get; }

    private BVN(string value)
    {
        Value = value;
    }

    public static Result<BVN> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<BVN>("BVN is required");

        var sanitized = value.Trim().Replace(" ", "");

        if (sanitized.Length != 11)
            return Result.Failure<BVN>("BVN must be exactly 11 digits");

        if (!sanitized.All(char.IsDigit))
            return Result.Failure<BVN>("BVN must contain only digits");

        return Result.Success(new BVN(sanitized));
    }

    public string Masked => $"***{Value[^4..]}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Masked;
}
