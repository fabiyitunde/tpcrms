using System.Collections.Concurrent;

namespace CRMS.API.PartyExposure;

/// <summary>
/// Per-portal-user and overall limits for the single lookup, in one-minute windows. The caller is
/// named in the request body (requestedBy.portalUserId), which the host's IP-based limiter cannot
/// see — and every portal user arrives from the portal's one address. Kept in memory: the API runs
/// as a single instance, and a restart forgiving a minute's count does no harm.
/// </summary>
public class PartyExposureRateLimiter
{
    private readonly ConcurrentDictionary<string, Window> _windows = new();
    private readonly TimeProvider _clock;

    public PartyExposureRateLimiter(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    private sealed class Window
    {
        public long Minute;
        public int Count;
    }

    /// <returns>Null when allowed; otherwise the seconds until the caller may try again.</returns>
    public int? TryAcquire(string userKey, int perUser, int overall)
    {
        var now = _clock.GetUtcNow();
        var minute = now.ToUnixTimeSeconds() / 60;
        var retryAfter = (int)(60 - now.ToUnixTimeSeconds() % 60);

        var all = _windows.GetOrAdd("*", _ => new Window());
        var user = _windows.GetOrAdd($"u:{userKey}", _ => new Window());

        lock (all)
        lock (user)
        {
            Roll(all, minute);
            Roll(user, minute);
            if (all.Count >= overall || user.Count >= perUser)
                return retryAfter;
            all.Count++;
            user.Count++;
        }

        // Old user windows are dropped now and then so the dictionary does not grow without bound.
        if (_windows.Count > 10_000)
            foreach (var key in _windows.Where(kv => kv.Value.Minute < minute).Select(kv => kv.Key).ToList())
                _windows.TryRemove(key, out _);

        return null;
    }

    private static void Roll(Window w, long minute)
    {
        if (w.Minute == minute) return;
        w.Minute = minute;
        w.Count = 0;
    }
}
