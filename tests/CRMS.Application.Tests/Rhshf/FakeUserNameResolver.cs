using CRMS.Application.Common;

namespace CRMS.Application.Tests.Rhshf;

/// <summary>Shared test double for <see cref="IUserNameResolver"/>. Returns a stable, predictable
/// name per id so assertions can check that the resolved name actually reaches the DTO, rather than
/// just that *something* non-null did.</summary>
internal class FakeUserNameResolver : IUserNameResolver
{
    private readonly Dictionary<Guid, string> _known;

    public FakeUserNameResolver(Dictionary<Guid, string>? known = null) => _known = known ?? [];

    public string NameFor(Guid id) => _known.TryGetValue(id, out var n) ? n : $"User {id.ToString()[..4]}";

    public Task<string> ResolveAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(NameFor(userId));

    public Task<string?> ResolveAsync(Guid? userId, CancellationToken ct = default)
        => Task.FromResult<string?>(userId.HasValue ? NameFor(userId.Value) : null);

    public Task<IReadOnlyDictionary<Guid, string>> ResolveManyAsync(IEnumerable<Guid> userIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, string>>(
            userIds.Where(id => id != Guid.Empty).Distinct().ToDictionary(id => id, NameFor));
}
