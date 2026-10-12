using System.Text.Json;

namespace PokeArena.Core.Twitch.EventSub;

/// <summary>One frame of the EventSub WebSocket, see dev.twitch.tv/docs/eventsub/handling-websocket-events.</summary>
public abstract record EventSubMessage(string MessageId)
{
    public sealed record Welcome(string MessageId, string SessionId, TimeSpan KeepaliveTimeout) : EventSubMessage(MessageId);

    public sealed record Keepalive(string MessageId) : EventSubMessage(MessageId);

    /// <param name="Event">The event object; owned by this record (cloned from the parsed document).</param>
    public sealed record Notification(string MessageId, string SubscriptionType, JsonElement Event) : EventSubMessage(MessageId);

    /// <summary>Twitch moves the session: connect to <see cref="ReconnectUrl"/> before the old socket closes.</summary>
    public sealed record Reconnect(string MessageId, Uri ReconnectUrl) : EventSubMessage(MessageId);

    /// <param name="Status">Reason, e.g. "authorization_revoked", "user_removed", "version_removed".</param>
    public sealed record Revocation(string MessageId, string SubscriptionType, string Status) : EventSubMessage(MessageId);

    public sealed record Unknown(string MessageId, string MessageType) : EventSubMessage(MessageId);

    /// <exception cref="JsonException">The frame is not a well-formed EventSub message.</exception>
    public static EventSubMessage Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var metadata = root.GetProperty("metadata");
        var payload = root.GetProperty("payload");
        var id = GetString(metadata, "message_id");
        var type = GetString(metadata, "message_type");

        return type switch
        {
            "session_welcome" => new Welcome(
                id,
                GetString(payload.GetProperty("session"), "id"),
                TimeSpan.FromSeconds(payload.GetProperty("session").GetProperty("keepalive_timeout_seconds").GetInt32())),
            "session_keepalive" => new Keepalive(id),
            "notification" => new Notification(
                id,
                GetString(metadata, "subscription_type"),
                payload.GetProperty("event").Clone()),
            "session_reconnect" => new Reconnect(
                id,
                new Uri(GetString(payload.GetProperty("session"), "reconnect_url"))),
            "revocation" => new Revocation(
                id,
                GetString(metadata, "subscription_type"),
                GetString(payload.GetProperty("subscription"), "status")),
            _ => new Unknown(id, type),
        };
    }

    private static string GetString(JsonElement element, string property) =>
        element.GetProperty(property).GetString() ?? throw new JsonException($"'{property}' is null");
}
