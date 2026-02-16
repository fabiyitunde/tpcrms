using CRMS.Domain.Entities.Identity;

namespace CRMS.Domain.Interfaces;

public interface IUserRepository
{
    Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<ApplicationUser?> GetByUserNameAsync(string userName, CancellationToken ct = default);
    Task<IReadOnlyList<ApplicationUser>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ApplicationUser>> GetByRoleAsync(string roleName, CancellationToken ct = default);
    Task<bool> ExistsAsync(string email, CancellationToken ct = default);
    Task AddAsync(ApplicationUser user, CancellationToken ct = default);
    Task UpdateAsync(ApplicationUser user, CancellationToken ct = default);
}

public interface IRoleRepository
{
    Task<ApplicationRole?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApplicationRole?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<ApplicationRole>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ApplicationRole>> GetUserRolesAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(ApplicationRole role, CancellationToken ct = default);
    Task UpdateAsync(ApplicationRole role, CancellationToken ct = default);
}

public interface IPermissionRepository
{
    Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Permission?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Permission>> GetByModuleAsync(string module, CancellationToken ct = default);
    Task<IReadOnlyList<Permission>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Permission permission, CancellationToken ct = default);
}
