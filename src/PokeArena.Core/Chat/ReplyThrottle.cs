namespace PokeArena.Core.Chat;

/// <summary>
/// "At most one reply per key and interval" — for status answers, cooldown hints and error replies, so a chat full
/// of the same command does not make the bot spam. Not thread-safe: use it from one thread (the command queue) or
/// under a lock.
/// </summary>
public sealed class ReplyThrottle(TimeSpan interval, TimeProvider time)
{
    /// <summary>Entries kept before old ones are dropped; a few hundred viewers stay far below.</summary>
    private const int PruneAbove = 1000;

    private readonly Dictionary<string, DateTimeOffset> _last = [];

    /// <summary>True (and remembers now) if the last reply for <paramref name="key"/> is older than the interval.</summary>
    public bool Allows(string key = "")
    {
        var now = time.GetUtcNow();
        if (_last.TryGetValue(key, out var last) && now - last < interval)
        {
            return false;
        }

        if (_last.Count >= PruneAbove)
        {
            foreach (var old in _last.Where(e => now - e.Value >= interval).Select(e => e.Key).ToList())
            {
                _last.Remove(old);
            }
        }

        _last[key] = now;
        return true;
    }

    /// <summary>Forgets every key, e.g. after <c>!bossreset</c>.</summary>
    public void Reset() => _last.Clear();
}
