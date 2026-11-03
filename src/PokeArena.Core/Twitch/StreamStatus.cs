namespace PokeArena.Core.Twitch;

/// <summary>
/// Whether the channel is live, and whether the bot takes commands (spec §8, WP-07). <see cref="TwitchConnection"/>
/// reports the live state; the streamer's "always active" switch lets them test offline. Neither is stored: after
/// an app start the channel counts as offline until Twitch says otherwise, and the switch is off.
/// </summary>
public sealed class StreamStatus
{
    private volatile bool _isLive;
    private volatile bool _alwaysActive;

    /// <summary>Raised after every change, on the changing thread.</summary>
    public event Action? Changed;

    public bool IsLive => _isLive;

    /// <summary>Take commands even while the channel is offline.</summary>
    public bool AlwaysActive
    {
        get => _alwaysActive;
        set
        {
            if (_alwaysActive != value)
            {
                _alwaysActive = value;
                Changed?.Invoke();
            }
        }
    }

    public bool AcceptsCommands => _isLive || _alwaysActive;

    /// <returns>True if the live state changed.</returns>
    public bool Report(bool isLive)
    {
        if (_isLive == isLive)
        {
            return false;
        }

        _isLive = isLive;
        Changed?.Invoke();
        return true;
    }
}
