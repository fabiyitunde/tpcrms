using CRMS.Domain.Constants;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Domain.Services;

/// <summary>
/// Service for determining visibility scope and filtering based on user role and location.
/// </summary>
public class VisibilityService
{
    private readonly ILocationRepository _locationRepository;

    public VisibilityService(ILocationRepository locationRepository)
    {
        _locationRepository = locationRepository;
    }

    /// <summary>
    /// Gets the list of branch IDs that a user can see based on their role and location.
    /// </summary>
    /// <param name="userLocationId">The user's assigned location ID (can be null for HO users)</param>
    /// <param name="userRole">The user's primary role</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of branch IDs the user can access. Empty list means NO access, null means GLOBAL access.</returns>
    public async Task<IReadOnlyList<Guid>?> GetVisibleBranchIdsAsync(
        Guid? userLocationId,
        string userRole,
        CancellationToken ct = default)
    {
        var scope = Roles.GetVisibilityScope(userRole);

        // Global visibility - return null to indicate no filtering needed
        if (scope == VisibilityScope.Global)
            return null;

        // Own visibility - this should be handled at the query level by filtering by InitiatedByUserId
        if (scope == VisibilityScope.Own)
            return []; // Empty list means filter by user, not branch

        // Branch-level visibility
        if (!userLocationId.HasValue)
            return []; // No location assigned - can't see anything

        // Get all descendant branches from the user's location
        return await _locationRepository.GetDescendantBranchIdsAsync(userLocationId.Value, ct);
    }

    /// <summary>
    /// Checks if a user can access a specific loan application based on their role and location.
    /// </summary>
    /// <param name="userId">The user's ID</param>
    /// <param name="userLocationId">The user's assigned location ID</param>
    /// <param name="userRole">The user's primary role</param>
    /// <param name="applicationBranchId">The loan application's branch ID</param>
    /// <param name="applicationInitiatorId">The loan application's initiator user ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if user can access the application</returns>
    public async Task<bool> CanAccessApplicationAsync(
        Guid userId,
        Guid? userLocationId,
        string userRole,
        Guid? applicationBranchId,
        Guid applicationInitiatorId,
        CancellationToken ct = default)
    {
        var scope = Roles.GetVisibilityScope(userRole);

        // Global visibility - can access everything
        if (scope == VisibilityScope.Global)
            return true;

        // Own visibility - can only access applications they created
        if (scope == VisibilityScope.Own)
            return userId == applicationInitiatorId;

        // Branch/Zone/Region visibility - check if application's branch is in user's scope
        if (!userLocationId.HasValue || !applicationBranchId.HasValue)
            return false;

        var visibleBranches = await _locationRepository.GetDescendantBranchIdsAsync(userLocationId.Value, ct);
        return visibleBranches.Contains(applicationBranchId.Value);
    }

    /// <summary>
    /// Gets the visibility scope for a given role.
    /// </summary>
    public static VisibilityScope GetVisibilityScopeForRole(string role)
    {
        return Roles.GetVisibilityScope(role);
    }

    /// <summary>
    /// Checks if a role has global visibility (can see all applications).
    /// </summary>
    public static bool HasGlobalVisibility(string role)
    {
        return Roles.HasGlobalVisibility(role);
    }
}
