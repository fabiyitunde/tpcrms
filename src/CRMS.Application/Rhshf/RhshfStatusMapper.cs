using System.Text;
using CRMS.Domain.Enums;

namespace CRMS.Application.Rhshf;

/// <summary>Maps our PascalCase enums to the brief's SCREAMING_SNAKE_CASE wire vocabulary (§5).</summary>
public static class RhshfStatusMapper
{
    public static string ToWireFormat(this RhshfCaseStatus status) => ToScreamingSnakeCase(status.ToString());

    public static string ToWireFormat(this RhshfDecisionOutcome outcome) => ToScreamingSnakeCase(outcome.ToString());

    private static string ToScreamingSnakeCase(string pascalCase)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < pascalCase.Length; i++)
        {
            if (i > 0 && char.IsUpper(pascalCase[i]))
                sb.Append('_');
            sb.Append(char.ToUpperInvariant(pascalCase[i]));
        }
        return sb.ToString();
    }
}
