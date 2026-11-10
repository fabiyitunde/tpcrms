using CRMS.Domain.Entities.Identity;

namespace CRMS.Application.Notification.Interfaces;

/// <summary>
/// Who to tell when a case reaches a stage a role acts at: the active users in that role whose
/// location is one of the case's, falling back to every active user in the role so a notice is
/// never silently dropped. The same rule for every loan track; NAMP's resolver implements it.
/// </summary>
public interface IStaffRecipientResolver
{
    Task<IReadOnlyList<ApplicationUser>> ResolveAsync(string role, IEnumerable<Guid> locationIds, CancellationToken ct = default);
}
