using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.ProductCatalog;

public class EligibilityRule : Entity
{
    public Guid LoanProductId { get; private set; }
    public EligibilityRuleType RuleType { get; private set; }
    public string FieldName { get; private set; } = string.Empty;
    public ComparisonOperator Operator { get; private set; }
    public string Value { get; private set; } = string.Empty;
    public bool IsHardRule { get; private set; }
    public string FailureMessage { get; private set; } = string.Empty;

    private EligibilityRule() { }

    internal static Result<EligibilityRule> Create(
        Guid loanProductId,
        EligibilityRuleType ruleType,
        string fieldName,
        ComparisonOperator comparisonOperator,
        string value,
        bool isHardRule,
        string failureMessage)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            return Result.Failure<EligibilityRule>("Field name is required");

        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<EligibilityRule>("Rule value is required");

        return Result.Success(new EligibilityRule
        {
            LoanProductId = loanProductId,
            RuleType = ruleType,
            FieldName = fieldName,
            Operator = comparisonOperator,
            Value = value,
            IsHardRule = isHardRule,
            FailureMessage = failureMessage ?? $"Failed {ruleType} check"
        });
    }

    public Result Update(
        EligibilityRuleType ruleType,
        string fieldName,
        ComparisonOperator comparisonOperator,
        string value,
        bool isHardRule,
        string failureMessage)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            return Result.Failure("Field name is required");

        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure("Rule value is required");

        RuleType = ruleType;
        FieldName = fieldName;
        Operator = comparisonOperator;
        Value = value;
        IsHardRule = isHardRule;
        FailureMessage = failureMessage ?? $"Failed {ruleType} check";

        return Result.Success();
    }

    public bool Evaluate(object fieldValue)
    {
        if (fieldValue == null)
            return false;

        return Operator switch
        {
            ComparisonOperator.Equals => fieldValue.ToString()?.Equals(Value, StringComparison.OrdinalIgnoreCase) ?? false,
            ComparisonOperator.NotEquals => !fieldValue.ToString()?.Equals(Value, StringComparison.OrdinalIgnoreCase) ?? false,
            ComparisonOperator.GreaterThan => CompareNumeric(fieldValue, v => v > 0),
            ComparisonOperator.LessThan => CompareNumeric(fieldValue, v => v < 0),
            ComparisonOperator.GreaterOrEqual => CompareNumeric(fieldValue, v => v >= 0),
            ComparisonOperator.LessOrEqual => CompareNumeric(fieldValue, v => v <= 0),
            ComparisonOperator.In => Value.Split(',').Select(v => v.Trim()).Contains(fieldValue.ToString(), StringComparer.OrdinalIgnoreCase),
            ComparisonOperator.NotIn => !Value.Split(',').Select(v => v.Trim()).Contains(fieldValue.ToString(), StringComparer.OrdinalIgnoreCase),
            ComparisonOperator.Between => EvaluateBetween(fieldValue),
            _ => false
        };
    }

    private bool CompareNumeric(object fieldValue, Func<int, bool> comparison)
    {
        if (!decimal.TryParse(fieldValue.ToString(), out var fieldNum))
            return false;

        if (!decimal.TryParse(Value, out var ruleNum))
            return false;

        return comparison(fieldNum.CompareTo(ruleNum));
    }

    private bool EvaluateBetween(object fieldValue)
    {
        var parts = Value.Split(',');
        if (parts.Length != 2)
            return false;

        if (!decimal.TryParse(fieldValue.ToString(), out var fieldNum))
            return false;

        if (!decimal.TryParse(parts[0].Trim(), out var min))
            return false;

        if (!decimal.TryParse(parts[1].Trim(), out var max))
            return false;

        return fieldNum >= min && fieldNum <= max;
    }
}
