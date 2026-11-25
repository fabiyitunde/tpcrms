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
    /// <summary>Open an RH-SHF director's or manager's CV and ID.</summary>
    public const string RhshfPersonalDocumentsView = "rhshf.personal-documents.view";
    /// <summary>See an RH-SHF director's full BVN or NIN (masked otherwise).</summary>
    public const string RhshfIdentityReveal = "rhshf.identity.reveal";

    public static readonly IReadOnlyList<string> All =
        [CreditCheckRun, CreditCheckViewAll, RhshfPersonalDocumentsView, RhshfIdentityReveal];

    public static string Label(string key) => key switch
    {
        CreditCheckRun => "Run credit checks",
        CreditCheckViewAll => "See every staff member's credit checks",
        RhshfPersonalDocumentsView => "Open RH-SHF directors' and managers' CVs and IDs",
        RhshfIdentityReveal => "Reveal an RH-SHF director's full BVN or NIN",
        _ => key,
    };

    public static string Description(string key) => key switch
    {
        CreditCheckRun => "Run an individual (BVN) or company (RC number) credit check from Credit Check in the menu, and see the checks they ran themselves. Each check calls billed third parties.",
        CreditCheckViewAll => "Open and download credit checks run by anyone, not only their own. Each look at someone else's check is written to the audit trail.",
        RhshfPersonalDocumentsView => "View and download the CVs and means of identification a FAC uploaded for its directors and management staff. Each view and download is written to the audit trail.",
        RhshfIdentityReveal => "Show a director's BVN or NIN in full, where everyone else sees it masked. Each reveal is written to the audit trail.",
        _ => string.Empty,
    };

    /// <summary>The roles a feature's row is seeded with when it is first added. Credit checks start
    /// with administrators only; the RH-SHF reviewer features start with the roles that review the
    /// file, so a Credit Officer can verify a director's CV on the day this ships. Never re-applied:
    /// once the row exists, its roles are the administrator's.</summary>
    public static IReadOnlyList<string> DefaultRoles(string key) => key switch
    {
        RhshfPersonalDocumentsView =>
            [Roles.CreditOfficer, Roles.LegalOfficer, Roles.RiskManager, Roles.CommitteeMember, Roles.FinalApprover],
        RhshfIdentityReveal => [Roles.CreditOfficer, Roles.RiskManager],
        _ => [],
    };
}
