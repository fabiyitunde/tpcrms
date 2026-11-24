using CRMS.Domain.Common;
using CRMS.Domain.Constants;

namespace CRMS.Domain.Aggregates.Access;

/// <summary>
/// Which roles may use one feature, chosen by an administrator in Admin &gt; Feature Access and
/// changeable at any time without a deploy. SystemAdmin is always allowed and is never stored, so
/// an administrator cannot lock themselves out.
/// </summary>
public class FeatureAccessRule : AggregateRoot
{
    public string FeatureKey { get; private set; } = string.Empty;
    /// <summary>Comma-separated role names.</summary>
    public string AllowedRoles { get; private set; } = string.Empty;

    private FeatureAccessRule() { }

    public static FeatureAccessRule Create(string featureKey, IEnumerable<string>? roles = null)
    {
        var rule = new FeatureAccessRule { FeatureKey = featureKey.Trim() };
        rule.SetRoles(roles ?? []);
        return rule;
    }

    public IReadOnlyList<string> RoleList =>
        AllowedRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Replaces the roles. Unknown names, vendor roles, the customer role and SystemAdmin
    /// (always allowed) are dropped rather than stored.</summary>
    public Result SetRoles(IEnumerable<string> roles)
    {
        var kept = roles
            .Select(r => r?.Trim() ?? string.Empty)
            .Where(r => Roles.AllRoles.Contains(r, StringComparer.Ordinal)
                && r != Roles.SystemAdmin && r != Roles.Customer && !VendorRoles.Contains(r))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(r => Array.IndexOf(Roles.AllRoles, r))
            .ToList();
        AllowedRoles = string.Join(",", kept);
        return Result.Success();
    }

    /// <summary>True when any of the user's roles may use the feature.</summary>
    public bool Allows(IEnumerable<string> userRoles)
    {
        var list = userRoles.ToList();
        return list.Contains(Roles.SystemAdmin, StringComparer.Ordinal)
            || list.Intersect(RoleList, StringComparer.Ordinal).Any();
    }
}

/// <summary>The features whose access is configured in Admin &gt; Feature Access.</summary>
public static class FeatureKeys
{
    /// <summary>Run individual and company credit checks from the intranet.</summary>
    public const string CreditCheckRun = "credit-check.run";
    /// <summary>Open credit checks other staff ran.</summary>
    public const string CreditCheckViewAll = "credit-check.view-all";

    public static readonly IReadOnlyList<string> All = [CreditCheckRun, CreditCheckViewAll];

    public static string Label(string key) => key switch
    {
        CreditCheckRun => "Run credit checks",
        CreditCheckViewAll => "See every staff member's credit checks",
        _ => key,
    };

    public static string Description(string key) => key switch
    {
        CreditCheckRun => "Run an individual (BVN) or company (RC number) credit check from Credit Check in the menu, and see the checks they ran themselves. Each check calls billed third parties.",
        CreditCheckViewAll => "Open and download credit checks run by anyone, not only their own. Each look at someone else's check is written to the audit trail.",
        _ => string.Empty,
    };
}
