using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Entities.Identity;

namespace CRMS.Application.Namp.Interfaces;

/// <summary>
/// Resolves which active users should be notified when a NAMP application reaches a stage
/// assigned to a given role. Scopes branch-level roles to the application's own location
/// (branch / office / location), falling back to every active user in the role when no
/// location-scoped match exists, so a notification is never silently dropped.
/// </summary>
public interface INampNotificationRecipientResolver
{
    Task<IReadOnlyList<ApplicationUser>> ResolveAsync(
        string role,
        NampApplication application,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves active users in <paramref name="role"/> scoped to the supplied location ids
    /// (branch / office / location), falling back to every active user in the role when none match.
    /// Used where there is no NampApplication yet (e.g. a freshly staged record).
    /// </summary>
    Task<IReadOnlyList<ApplicationUser>> ResolveAsync(
        string role,
        IEnumerable<Guid> locationIds,
        CancellationToken ct = default);
}
