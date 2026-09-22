using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Infrastructure.Services;

/// <summary>
/// Scoped implementation of <see cref="IUserNameResolver"/>. Loads the full user list once on first
/// use and caches it for the lifetime of the scope (one HTTP request, or one Blazor circuit
/// operation), so resolving actors across many tabs costs a single query.
///
/// Deliberately does NOT cache across scopes — a renamed user should show their new name on the
/// next page load. That's the opposite trade-off to the committee module, which snapshots UserName
/// at write time and therefore keeps historical names frozen.
/// </summary>
public class UserNameResolver : IUserNameResolver
{
    private readonly IUserRepository _userRepo;
    private Dictionary<Guid, string>? _lookup;

    public UserNameResolver(IUserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    public async Task<string> ResolveAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return "—";

        var lookup = await GetLookupAsync(ct);
        return lookup.TryGetValue(userId, out var name) ? name : Truncate(userId);
    }

    public async Task<string?> ResolveAsync(Guid? userId, CancellationToken ct = default)
        => userId.HasValue ? await ResolveAsync(userId.Value, ct) : null;

    public async Task<IReadOnlyDictionary<Guid, string>> ResolveManyAsync(
        IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        var lookup = await GetLookupAsync(ct);
        return userIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToDictionary(id => id, id => lookup.TryGetValue(id, out var name) ? name : Truncate(id));
    }

    private async Task<Dictionary<Guid, string>> GetLookupAsync(CancellationToken ct)
    {
        if (_lookup is not null)
            return _lookup;

        var users = await _userRepo.GetAllAsync(ct);
        _lookup = users.ToDictionary(u => u.Id, u => u.FullName);
        return _lookup;
    }

    // Matches GenerateNampLoanPackCommand's long-standing fallback — a short, obviously-partial id
    // rather than a full GUID, so an unresolvable actor reads as a reference, not as noise.
    private static string Truncate(Guid id) => id.ToString()[..8] + "…";
}
