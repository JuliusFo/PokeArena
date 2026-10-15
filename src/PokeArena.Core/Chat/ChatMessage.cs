namespace PokeArena.Core.Chat;

public enum ChatRole
{
    Viewer,
    Moderator,
    Broadcaster,
}

/// <summary>An incoming chat message, from Twitch or typed into the dry run.</summary>
/// <param name="UserId">Twitch user id — the only identity of a trainer (spec §1).</param>
/// <param name="IsSimulated">Typed into the dry run by the streamer, not sent by a real viewer.</param>
public sealed record ChatMessage(
    string MessageId,
    string UserId,
    string Login,
    string DisplayName,
    string Text,
    ChatRole Role,
    bool IsSimulated = false) : InboxItem
{
    public bool IsModerator => Role is ChatRole.Moderator or ChatRole.Broadcaster;
}
