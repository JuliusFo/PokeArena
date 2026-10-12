using PokeArena.Core.Twitch.Auth;

namespace PokeArena.Core.Twitch;

public enum TwitchStatus
{
    /// <summary>Client ID or channel missing in the configuration.</summary>
    NotConfigured,

    /// <summary>No login stored; the bot account has to sign in.</summary>
    SignedOut,

    /// <summary>Device login running; the user enters the code on twitch.tv/activate.</summary>
    WaitingForLogin,

    /// <summary>Signed in, but not connected (stopped by the user).</summary>
    Disconnected,

    Connecting,
    Connected,

    /// <summary>Connection lost; a new attempt follows automatically.</summary>
    Reconnecting,

    /// <summary>Stopped because of a problem that retrying cannot fix, see <see cref="TwitchProblem"/>.</summary>
    Failed,
}

/// <summary>Why the last attempt ended; the UI turns it into a German hint for the streamer.</summary>
public enum TwitchProblem
{
    None,

    /// <summary>The device code expired before the user confirmed it.</summary>
    LoginExpired,

    /// <summary>The device login could not be started or ended with an error.</summary>
    LoginFailed,

    /// <summary>Twitch no longer accepts the stored login (revoked, or unused for 30 days).</summary>
    SessionExpired,

    /// <summary>The configured channel does not exist.</summary>
    ChannelNotFound,

    /// <summary>Twitch refused the chat subscription (bot banned, or permission missing).</summary>
    MissingPermission,

    /// <summary>Twitch ended the chat subscription (e.g. the bot account was suspended).</summary>
    SubscriptionRevoked,

    /// <summary>Connection lost; see <see cref="TwitchConnectionState.RetryAt"/>.</summary>
    ConnectionLost,
}

/// <param name="BotLogin">The signed-in bot account, once known.</param>
/// <param name="Login">The open device login while <see cref="TwitchStatus.WaitingForLogin"/>.</param>
/// <param name="RetryAt">When the next attempt starts while <see cref="TwitchStatus.Reconnecting"/>.</param>
public sealed record TwitchConnectionState(
    TwitchStatus Status,
    TwitchProblem Problem = TwitchProblem.None,
    string? BotLogin = null,
    DeviceAuthorization? Login = null,
    DateTimeOffset? RetryAt = null);

/// <summary>The bot's link to Twitch as the UI sees and controls it.</summary>
public interface ITwitchConnection
{
    TwitchConnectionState State { get; }

    /// <summary>The configured channel (login name), empty if not configured.</summary>
    string Channel { get; }

    /// <summary>Raised after every state change, on an arbitrary thread.</summary>
    event Action? StateChanged;

    /// <summary>Starts a device login; the state then carries the code to enter.</summary>
    Task BeginLoginAsync();

    void CancelLogin();

    /// <summary>Connects with the stored login; does nothing if already running.</summary>
    void Connect();

    Task DisconnectAsync();

    /// <summary>Disconnects and forgets the stored login.</summary>
    Task SignOutAsync();
}
