using CRMS.Domain.Common;

namespace CRMS.Domain.Entities.Identity;

public class ApplicationRolePermission : Entity
{
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }

    public ApplicationRole? Role { get; private set; }
    public Permission? Permission { get; private set; }

    private ApplicationRolePermission() { }

    public ApplicationRolePermission(Guid roleId, Guid permissionId)
    {
        RoleId = roleId;
        PermissionId = permissionId;
    }
}
