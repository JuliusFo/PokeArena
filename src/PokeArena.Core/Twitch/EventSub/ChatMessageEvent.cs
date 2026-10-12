using System.Text.Json;
using PokeArena.Core.Chat;

namespace PokeArena.Core.Twitch.EventSub;

/// <summary>Maps a <c>channel.chat.message</c> event to a <see cref="ChatMessage"/>.</summary>
public static class ChatMessageEvent
{
    public const string SubscriptionType = "channel.chat.message";

    /// <returns>
    /// The message, or null if the bot must ignore it: its own messages (no reply loops) and, during a shared
    /// chat session, messages from the other channels (their viewers do not play here).
    /// </returns>
    public static ChatMessage? Read(JsonElement ev, ChatTarget target)
    {
        var source = OptionalString(ev, "source_broadcaster_user_id");
        if (source is not null && source != target.BroadcasterId)
        {
            return null;
        }

        var userId = RequiredString(ev, "chatter_user_id");
        if (userId == target.BotUserId)
        {
            return null;
        }

        return new ChatMessage(
            RequiredString(ev, "message_id"),
            userId,
            RequiredString(ev, "chatter_user_login"),
            RequiredString(ev, "chatter_user_name"),
            RequiredString(ev.GetProperty("message"), "text"),
            ReadRole(ev, userId, target));
    }

    private static ChatRole ReadRole(JsonElement ev, string userId, ChatTarget target)
    {
        if (userId == target.BroadcasterId)
        {
            return ChatRole.Broadcaster;
        }

        if (ev.TryGetProperty("badges", out var badges) && badges.ValueKind == JsonValueKind.Array)
        {
            foreach (var badge in badges.EnumerateArray())
            {
                if (OptionalString(badge, "set_id") == "moderator")
                {
                    return ChatRole.Moderator;
                }
            }
        }

        return ChatRole.Viewer;
    }

    private static string RequiredString(JsonElement element, string property) =>
        element.GetProperty(property).GetString() ?? throw new JsonException($"'{property}' is null");

    private static string? OptionalString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
