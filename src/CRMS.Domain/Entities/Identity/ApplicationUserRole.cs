using CRMS.Domain.Common;

namespace CRMS.Domain.Entities.Identity;

public class ApplicationUserRole : Entity
{
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public DateTime AssignedAt { get; private set; }

    public ApplicationUser? User { get; private set; }
    public ApplicationRole? Role { get; private set; }

    private ApplicationUserRole() { }

    public ApplicationUserRole(Guid userId, Guid roleId)
    {
        UserId = userId;
        RoleId = roleId;
        AssignedAt = DateTime.UtcNow;
    }
}
