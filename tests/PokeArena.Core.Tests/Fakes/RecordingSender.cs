using PokeArena.Core.Chat;

namespace PokeArena.Core.Tests.Fakes;

/// <summary>Records every chat message instead of sending it; hints (throttled per viewer in the app) also land in <see cref="Hints"/>.</summary>
public sealed class RecordingSender : IChatSender
{
    /// <summary>Every message in order, hints included.</summary>
    public List<string> Sent { get; } = [];

    /// <summary>The messages sent as a hint, with the viewer they were for.</summary>
    public List<(string ViewerId, string Text)> Hints { get; } = [];

    public void Send(string text) => Sent.Add(text);

    public void SendHint(string viewerId, string text)
    {
        Hints.Add((viewerId, text));
        Sent.Add(text);
    }
}
