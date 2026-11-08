using PokeArena.Core.Twitch;

namespace PokeArena.UI.Tests;

/// <summary>The Twitch connection as the UI sees it, without a network; <see cref="Set"/> changes the state like a real event.</summary>
public sealed class FakeTwitchConnection : ITwitchConnection
{
    public TwitchConnectionState State { get; set; } = new(TwitchStatus.Disconnected);

    public string Channel => "shiyofurball";

    public int LoginStarts { get; private set; }

    public int Connects { get; private set; }

    public event Action? StateChanged;

    public void Set(TwitchConnectionState state)
    {
        State = state;
        StateChanged?.Invoke();
    }

    public Task BeginLoginAsync()
    {
        LoginStarts++;
        return Task.CompletedTask;
    }

    public void CancelLogin()
    {
    }

    public void Connect() => Connects++;

    public Task DisconnectAsync() => Task.CompletedTask;

    public Task SignOutAsync() => Task.CompletedTask;
}
