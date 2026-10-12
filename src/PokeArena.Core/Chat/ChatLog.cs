namespace PokeArena.Core.Chat;

public enum ChatLogKind
{
    Incoming,
    Outgoing,
    Info,
    Warning,
}

/// <param name="Name">Who wrote it (incoming/outgoing), or null for notes of the bot itself.</param>
/// <param name="DryRun">Part of the dry run: simulated input, or a reply that did not go to Twitch.</param>
public sealed record ChatLogEntry(DateTimeOffset At, ChatLogKind Kind, string? Name, string Text, bool DryRun = false);

/// <summary>
/// The live chat log of the desktop app: the last messages in and out plus notes for the streamer (German).
/// In memory only; the Serilog file log is the durable record.
/// </summary>
public sealed class ChatLog(TimeProvider time)
{
    public const int Capacity = 500;

    private readonly Lock _lock = new();
    private readonly LinkedList<ChatLogEntry> _entries = [];

    /// <summary>Raised after every change, on the thread that made it.</summary>
    public event Action? Changed;

    public void Add(ChatLogKind kind, string? name, string text, bool dryRun = false)
    {
        lock (_lock)
        {
            _entries.AddLast(new ChatLogEntry(time.GetUtcNow(), kind, name, text, dryRun));
            if (_entries.Count > Capacity)
            {
                _entries.RemoveFirst();
            }
        }

        Changed?.Invoke();
    }

    public IReadOnlyList<ChatLogEntry> Snapshot()
    {
        lock (_lock)
        {
            return [.. _entries];
        }
    }
}
