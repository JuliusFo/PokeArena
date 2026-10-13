namespace PokeArena.Core;

/// <summary>
/// Dry run ("Probelauf"): replies only go to the chat log, game data goes to a sandbox database that starts empty
/// each time, and real chat messages are not processed (WP-02 decision).
/// </summary>
public sealed class DryRunMode
{
    private volatile bool _enabled;

    /// <summary>Raised after every switch, on the switching thread.</summary>
    public event Action? Changed;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled != value)
            {
                _enabled = value;
                Changed?.Invoke();
            }
        }
    }
}
