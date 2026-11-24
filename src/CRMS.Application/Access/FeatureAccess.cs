using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Access;
using CRMS.Domain.Constants;
using CRMS.Domain.Entities.Identity;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Services;

namespace CRMS.Application.Access;

/// <summary>
/// Whether a user may use a feature, answered from the database every time: the user's current
/// roles and the rule an administrator saved. Not from the roles cached at sign-in, so ticking or
/// unticking a role in Admin &gt; Feature Access applies on the user's next page load.
/// </summary>
public interface IFeatureAccessService
{
    Task<bool> IsAllowedAsync(Guid userId, string featureKey, CancellationToken ct = default);
}

public class FeatureAccessService : IFeatureAccessService
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IFeatureAccessRuleRepository _rules;

    public FeatureAccessService(IUserRepository users, IRoleRepository roles, IFeatureAccessRuleRepository rules)
    {
        _users = users;
        _roles = roles;
        _rules = rules;
    }

    public async Task<bool> IsAllowedAsync(Guid userId, string featureKey, CancellationToken ct = default)
    {
        if (userId == Guid.Empty) return false;
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null || user.Status != UserStatus.Active) return false;

        var roles = (await _roles.GetUserRolesAsync(userId, ct)).Select(r => r.Name).ToList();
        if (roles.Contains(Roles.SystemAdmin, StringComparer.Ordinal)) return true;

        var rule = await _rules.GetByKeyAsync(featureKey, ct);
        return rule is not null && rule.Allows(roles);
    }
}

// ── Admin > Feature Access ───────────────────────────────────────────────────────────────────────

public record FeatureAccessRuleDto(string FeatureKey, string Label, string Description, List<string> AllowedRoles, DateTime? ModifiedAt, string? ModifiedBy);

public record GetFeatureAccessRulesQuery : IRequest<ApplicationResult<List<FeatureAccessRuleDto>>>;

public class GetFeatureAccessRulesHandler : IRequestHandler<GetFeatureAccessRulesQuery, ApplicationResult<List<FeatureAccessRuleDto>>>
{
    private readonly IFeatureAccessRuleRepository _rules;

    public GetFeatureAccessRulesHandler(IFeatureAccessRuleRepository rules) => _rules = rules;

    public async Task<ApplicationResult<List<FeatureAccessRuleDto>>> Handle(GetFeatureAccessRulesQuery request, CancellationToken ct = default)
    {
        var saved = (await _rules.GetAllAsync(ct)).ToDictionary(r => r.FeatureKey);
        // Every known feature is listed, even one whose row the seeder has not added yet: it then
        // reads as "administrators only", which is what it means.
        var list = FeatureKeys.All.Select(k => saved.TryGetValue(k, out var r)
                ? new FeatureAccessRuleDto(k, FeatureKeys.Label(k), FeatureKeys.Description(k), r.RoleList.ToList(), r.ModifiedAt, r.ModifiedBy)
                : new FeatureAccessRuleDto(k, FeatureKeys.Label(k), FeatureKeys.Description(k), [], null, null))
            .ToList();
        return ApplicationResult<List<FeatureAccessRuleDto>>.Success(list);
    }
}

public record UpdateFeatureAccessRuleCommand(string FeatureKey, List<string> Roles, Guid UserId, string UserName, string? IpAddress = null)
    : IRequest<ApplicationResult>;

public class UpdateFeatureAccessRuleHandler : IRequestHandler<UpdateFeatureAccessRuleCommand, ApplicationResult>
{
    private readonly IFeatureAccessRuleRepository _rules;
    private readonly IRoleRepository _roles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditService _audit;

    public UpdateFeatureAccessRuleHandler(IFeatureAccessRuleRepository rules, IRoleRepository roles, IUnitOfWork unitOfWork, AuditService audit)
    {
        _rules = rules;
        _roles = roles;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<ApplicationResult> Handle(UpdateFeatureAccessRuleCommand request, CancellationToken ct = default)
    {
        if (!FeatureKeys.All.Contains(request.FeatureKey))
            return ApplicationResult.Failure("Unknown feature.");
        var actorRoles = (await _roles.GetUserRolesAsync(request.UserId, ct)).Select(r => r.Name);
        if (!actorRoles.Contains(Roles.SystemAdmin))
            return ApplicationResult.Failure("Only a system administrator can change who may use a feature.");

        var rule = await _rules.GetByKeyAsync(request.FeatureKey, ct);
        var isNew = rule is null;
        rule ??= FeatureAccessRule.Create(request.FeatureKey);
        var before = rule.AllowedRoles;
        rule.SetRoles(request.Roles);
        if (!isNew && before == rule.AllowedRoles)
            return ApplicationResult.Success();

        rule.SetAuditInfo(request.UserName, isNew);
        if (isNew) await _rules.AddAsync(rule, ct);
        else _rules.Update(rule);
        await _unitOfWork.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditAction.ConfigChange, AuditCategory.Configuration,
            $"Changed who may use \"{FeatureKeys.Label(rule.FeatureKey)}\".",
            "FeatureAccessRule", rule.Id, rule.FeatureKey,
            request.UserId, request.UserName, Roles.SystemAdmin, request.IpAddress,
            oldValues: new { AllowedRoles = before }, newValues: new { rule.AllowedRoles }, ct: ct);
        return ApplicationResult.Success();
    }
}
