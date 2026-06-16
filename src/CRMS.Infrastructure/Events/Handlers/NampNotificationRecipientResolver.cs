using CRMS.Application.Namp.Interfaces;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Entities.Identity;
using CRMS.Domain.Interfaces;

namespace CRMS.Infrastructure.Events.Handlers;

/// <summary>
/// Resolves notification recipients for a NAMP stage by role, scoped to the application's
/// own location. See <see cref="INampNotificationRecipientResolver"/> for the contract.
/// </summary>
public class NampNotificationRecipientResolver : INampNotificationRecipientResolver
{
    private readonly IUserRepository _userRepo;

    public NampNotificationRecipientResolver(IUserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    public Task<IReadOnlyList<ApplicationUser>> ResolveAsync(
        string role,
        NampApplication application,
        CancellationToken ct = default)
    {
        var locationIds = new List<Guid> { application.BranchId };
        if (application.OfficeId is Guid officeId) locationIds.Add(officeId);
        if (application.LocationId is Guid locationId) locationIds.Add(locationId);
        return ResolveAsync(role, locationIds, ct);
    }

    public async Task<IReadOnlyList<ApplicationUser>> ResolveAsync(
        string role,
        IEnumerable<Guid> locationIds,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(role))
            return Array.Empty<ApplicationUser>();

        var usersInRole = await _userRepo.GetByRoleAsync(role, ct);

        var active = usersInRole
            .Where(u => u.Status == UserStatus.Active && !string.IsNullOrWhiteSpace(u.Email))
            .ToList();

        if (active.Count == 0)
            return active;

        var scopeIds = locationIds.ToHashSet();

        var scoped = active
            .Where(u => u.LocationId is Guid ul && scopeIds.Contains(ul))
            .ToList();

        // Fall back to every active user in the role so a notification is never silently dropped.
        return scoped.Count > 0 ? scoped : active;
    }
}
