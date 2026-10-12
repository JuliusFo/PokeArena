namespace PokeArena.Core.Chat;

/// <summary>
/// Keeps outgoing messages within Twitch's chat limits. Uses the limits of a normal user (20 per 30 s, 1 per
/// second) even if the bot is a moderator: the bot cannot cheaply tell, and aggregated messages stay far below.
/// Not thread-safe; used by the single send loop.
/// </summary>
public sealed class SendRateLimiter
{
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(30);
    public const int MaxPerWindow = 20;
    public static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1);

    private readonly Queue<DateTimeOffset> _sent = new();

    /// <summary>How long to wait before the next message may go out.</summary>
    public TimeSpan GetDelay(DateTimeOffset now)
    {
        while (_sent.Count > 0 && _sent.Peek() <= now - Window)
        {
            _sent.Dequeue();
        }

        var delay = TimeSpan.Zero;
        if (_sent.Count >= MaxPerWindow)
        {
            delay = _sent.Peek() + Window - now;
        }

        if (_sent.Count > 0)
        {
            var sinceLast = _sent.Last() + MinInterval - now;
            delay = sinceLast > delay ? sinceLast : delay;
        }

        return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
    }

    public void Record(DateTimeOffset sentAt) => _sent.Enqueue(sentAt);
}
