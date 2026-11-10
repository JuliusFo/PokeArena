using PokeArena.Core.Twitch;

namespace PokeArena.Core;

/// <summary>When a downloaded update may be installed — the restart costs a few seconds without the bot.</summary>
public interface IUpdateGate
{
    /// <summary>A boss lobby is open or its fight is being resolved: a restart would drop it, so not even „Jetzt aktualisieren“ may.</summary>
    bool LobbyOpen { get; }

    /// <summary>The automatic install may run now: the bot has been idle for a while, no dry run, no lobby.</summary>
    bool MayInstallAutomatically { get; }
}

/// <summary>
/// WP-11 rule (ADR-0010): install automatically only once the bot has taken no commands (stream offline and „Auch ohne
/// Stream“ off, WP-07) for <see cref="IdleBeforeInstall"/>, no dry run is active and no boss lobby is open. The wait
/// keeps a short stream drop from turning into a restart while the streamer comes back (review WP-11). Open
/// <c>!fight</c> challenges do not block — nobody can accept them while the bot is idle, and they expire anyway.
/// </summary>
public sealed class UpdateGate : IUpdateGate
{
    public static readonly TimeSpan IdleBeforeInstall = TimeSpan.FromMinutes(10);

    private readonly StreamStatus _stream;
    private readonly DryRunMode _dryRun;
    private readonly Func<bool> _lobbyOpen;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();

    /// <summary>Since when the bot takes no commands; null while it does. An app start counts as the start of idling.</summary>
    private DateTimeOffset? _idleSince;

    public UpdateGate(StreamStatus stream, DryRunMode dryRun, Func<bool> lobbyOpen, TimeProvider time)
    {
        _stream = stream;
        _dryRun = dryRun;
        _lobbyOpen = lobbyOpen;
        _time = time;
        _idleSince = stream.AcceptsCommands ? null : time.GetUtcNow();
        stream.Changed += OnStreamChanged;
    }

    public bool LobbyOpen => _lobbyOpen();

    public bool MayInstallAutomatically
    {
        get
        {
            DateTimeOffset? idleSince;
            lock (_lock)
            {
                idleSince = _idleSince;
            }

            return !_stream.AcceptsCommands
                && idleSince is { } since
                && _time.GetUtcNow() - since >= IdleBeforeInstall
                && !_dryRun.Enabled
                && !LobbyOpen;
        }
    }

    private void OnStreamChanged()
    {
        lock (_lock)
        {
            if (_stream.AcceptsCommands)
            {
                _idleSince = null;
            }
            else
            {
                _idleSince ??= _time.GetUtcNow();
            }
        }
    }
}
