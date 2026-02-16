using CRMS.Domain.Common;

namespace CRMS.Domain.ValueObjects;

public class AccountNumber : ValueObject
{
    public string Value { get; }

    private AccountNumber(string value)
    {
        Value = value;
    }

    public static Result<AccountNumber> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<AccountNumber>("Account number is required");

        var sanitized = value.Trim().Replace(" ", "").Replace("-", "");

        if (sanitized.Length != 10)
            return Result.Failure<AccountNumber>("Account number must be exactly 10 digits");

        if (!sanitized.All(char.IsDigit))
            return Result.Failure<AccountNumber>("Account number must contain only digits");

        return Result.Success(new AccountNumber(sanitized));
    }

    public string Masked => $"****{Value[^4..]}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
