using CRMS.Domain.Entities.Identity;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly CRMSDbContext _context;

    public UserRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.ToUpperInvariant();
        return await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);
    }

    public async Task<ApplicationUser?> GetByUserNameAsync(string userName, CancellationToken ct = default)
    {
        var normalizedUserName = userName.ToUpperInvariant();
        return await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName, ct);
    }

    public async Task<IReadOnlyList<ApplicationUser>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ApplicationUser>> GetByRoleAsync(string roleName, CancellationToken ct = default)
    {
        var normalizedRoleName = roleName.ToUpperInvariant();
        return await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(u => u.UserRoles.Any(ur => ur.Role!.NormalizedName == normalizedRoleName))
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.ToUpperInvariant();
        return await _context.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, ct);
    }

    public async Task AddAsync(ApplicationUser user, CancellationToken ct = default)
    {
        await _context.Users.AddAsync(user, ct);
    }

    public Task UpdateAsync(ApplicationUser user, CancellationToken ct = default)
    {
        _context.Users.Update(user);
        return Task.CompletedTask;
    }
}

public class RoleRepository : IRoleRepository
{
    private readonly CRMSDbContext _context;

    public RoleRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<ApplicationRole?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<ApplicationRole?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var normalizedName = name.ToUpperInvariant();
        return await _context.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.NormalizedName == normalizedName, ct);
    }

    public async Task<IReadOnlyList<ApplicationRole>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ApplicationRole>> GetUserRolesAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Include(ur => ur.Role)
                .ThenInclude(r => r!.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .Select(ur => ur.Role!)
            .ToListAsync(ct);
    }

    public async Task AddAsync(ApplicationRole role, CancellationToken ct = default)
    {
        await _context.Roles.AddAsync(role, ct);
    }

    public Task UpdateAsync(ApplicationRole role, CancellationToken ct = default)
    {
        _context.Roles.Update(role);
        return Task.CompletedTask;
    }
}

public class PermissionRepository : IPermissionRepository
{
    private readonly CRMSDbContext _context;

    public PermissionRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Permissions.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<Permission?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        return await _context.Permissions.FirstOrDefaultAsync(p => p.Code == code, ct);
    }

    public async Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Permissions.OrderBy(p => p.Module).ThenBy(p => p.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Permission>> GetByModuleAsync(string module, CancellationToken ct = default)
    {
        return await _context.Permissions
            .Where(p => p.Module == module)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Permission>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role!.RolePermissions)
            .Select(rp => rp.Permission!)
            .Distinct()
            .OrderBy(p => p.Module)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Permission permission, CancellationToken ct = default)
    {
        await _context.Permissions.AddAsync(permission, ct);
    }
}
