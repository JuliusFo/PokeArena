using PokeArena.Core.Twitch;

namespace PokeArena.Core;

/// <summary>When a downloaded update may be installed — the restart costs about half a minute without the bot.</summary>
public interface IUpdateGate
{
    /// <summary>A boss lobby is open: a restart would drop its participants, so not even „Jetzt aktualisieren“ may.</summary>
    bool LobbyOpen { get; }

    /// <summary>The automatic install may run now: the bot takes no commands, no dry run, no lobby.</summary>
    bool MayInstallAutomatically { get; }
}

/// <summary>
/// WP-11 rule, Proposal (not confirmed): install automatically only while the bot takes no commands (stream offline
/// and „Auch ohne Stream“ off, WP-07), no dry run is active and no boss lobby is open. Open <c>!fight</c> challenges
/// do not block — nobody can accept them while the bot is idle, and they expire anyway.
/// </summary>
public sealed class UpdateGate(StreamStatus stream, DryRunMode dryRun, Func<bool> lobbyOpen) : IUpdateGate
{
    public bool LobbyOpen => lobbyOpen();

    public bool MayInstallAutomatically => !stream.AcceptsCommands && !dryRun.Enabled && !LobbyOpen;
}
