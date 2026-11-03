namespace PokeArena.Core.Twitch.EventSub;

/// <summary>
/// <c>stream.online</c> / <c>stream.offline</c>: the channel went live or ended. Public events, no scope needed. They
/// only report changes — the state at connect time comes from Helix <c>GET /streams</c>.
/// </summary>
public static class StreamEvents
{
    public const string Online = "stream.online";

    public const string Offline = "stream.offline";
}
