using PokeArena.Core.Chat;

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

    /// <summary>
    /// Switches on the command queue, between two commands — the way the UI must do it. A command checks the mode
    /// once and then opens the database several times; switching in between could write a simulated message into
    /// the live database (review 1.0.3, A13).
    /// </summary>
    public void SwitchOnQueue(ChatInbox inbox, bool enabled) =>
        inbox.Post(new QueuedWork("Probelauf", _ =>
        {
            Enabled = enabled;
            return Task.CompletedTask;
        }));
}
