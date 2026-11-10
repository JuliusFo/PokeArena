using PokeArena.Core.Chat;

namespace PokeArena.Core.Tests.Fakes;

/// <summary>Records every chat message instead of sending it; hints (throttled per viewer in the app) also land in <see cref="Hints"/>.</summary>
public sealed class RecordingSender : IChatSender
{
    /// <summary>Every message in order, hints included.</summary>
    public List<string> Sent { get; } = [];

    /// <summary>The messages sent as a hint, with the viewer and the command they answered.</summary>
    public List<(string ViewerId, string Command, string Text)> Hints { get; } = [];

    /// <summary>Runs on every message, e.g. to look at state while the code under test is in the middle of its work.</summary>
    public Action<string>? OnSend { get; set; }

    public void Send(string text)
    {
        Sent.Add(text);
        OnSend?.Invoke(text);
    }

    public void SendHint(string viewerId, string command, string text)
    {
        Hints.Add((viewerId, command, text));
        Sent.Add(text);
    }
}
