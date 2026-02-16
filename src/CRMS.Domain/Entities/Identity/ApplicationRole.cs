using CRMS.Domain.Common;

namespace CRMS.Domain.Entities.Identity;

public class ApplicationRole : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public RoleType Type { get; private set; }

    private readonly List<ApplicationRolePermission> _rolePermissions = [];
    public IReadOnlyCollection<ApplicationRolePermission> RolePermissions => _rolePermissions.AsReadOnly();

    private ApplicationRole() { }

    public static ApplicationRole Create(string name, string? description, RoleType type)
    {
        return new ApplicationRole
        {
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            Description = description,
            Type = type
        };
    }

    public void AddPermission(Permission permission)
    {
        if (_rolePermissions.Any(rp => rp.PermissionId == permission.Id))
            return;

        _rolePermissions.Add(new ApplicationRolePermission(Id, permission.Id));
    }

    public void RemovePermission(Guid permissionId)
    {
        var rolePermission = _rolePermissions.FirstOrDefault(rp => rp.PermissionId == permissionId);
        if (rolePermission != null)
            _rolePermissions.Remove(rolePermission);
    }

    public void Update(string description)
    {
        Description = description;
    }
}

public enum RoleType
{
    System,
    Custom
}
