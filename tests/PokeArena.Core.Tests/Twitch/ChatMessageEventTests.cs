using System.Text.Json;
using PokeArena.Core.Chat;
using PokeArena.Core.Tests.Fakes;
using PokeArena.Core.Twitch.EventSub;

namespace PokeArena.Core.Tests.Twitch;

/// <summary>Reading a chat event from EventSub: id, names, colour, role and which messages count.</summary>
public class ChatMessageEventTests
{
    private static readonly ChatTarget Target = new("42", "shiyofurball", "2001", "pokearenabot");

    [Fact]
    public void Chat_event_maps_to_a_message_keyed_by_user_id()
    {
        var message = Read(EventSubFrames.ChatMessage("!pokearena", chatterId: "1001", chatterLogin: "ash", chatterName: "Ash"));

        Assert.NotNull(message);
        Assert.Equal("1001", message.UserId);
        Assert.Equal("ash", message.Login);
        Assert.Equal("Ash", message.DisplayName);
        Assert.Equal("!pokearena", message.Text);
        Assert.Equal(ChatRole.Viewer, message.Role);
        Assert.False(message.IsSimulated);
    }

    [Theory]
    [InlineData("#00ff7f", "#00FF7F")]
    [InlineData("", null)]
    [InlineData("#00FF7", null)]
    [InlineData("red;x:y", null)]
    [InlineData("#00FF7G", null)]
    public void Chat_colour_is_kept_only_as_a_plain_hex_colour(string color, string? expected) =>
        Assert.Equal(expected, Read(EventSubFrames.ChatMessage("hi", color: color))?.Color);

    [Theory]
    [InlineData("1001", """[{"set_id":"moderator","id":"1","info":""}]""", ChatRole.Moderator)]
    [InlineData("1001", """[{"set_id":"lead_moderator","id":"1","info":""}]""", ChatRole.Moderator)]
    [InlineData("42", """[{"set_id":"broadcaster","id":"1","info":""}]""", ChatRole.Broadcaster)]
    [InlineData("1001", """[{"set_id":"subscriber","id":"12","info":"16"}]""", ChatRole.Viewer)]
    [InlineData("1001", """[{"set_id":"vip","id":"1","info":""}]""", ChatRole.Viewer)]
    [InlineData("1001", """[{"set_id":"vip","id":"1","info":""},{"set_id":"moderator","id":"1","info":""}]""", ChatRole.Moderator)]
    public void Chat_event_role_comes_from_badges_and_broadcaster_id(string chatterId, string badges, ChatRole expected)
    {
        var message = Read(EventSubFrames.ChatMessage("hi", chatterId: chatterId, badgesJson: badges));

        Assert.Equal(expected, message?.Role);
    }


    [Fact]
    public void Own_messages_and_shared_chat_messages_from_other_channels_are_ignored()
    {
        Assert.Null(Read(EventSubFrames.ChatMessage("PokeArena-Bot ist online", chatterId: "2001")));
        Assert.Null(Read(EventSubFrames.ChatMessage("!pokearena", sourceBroadcasterId: "77")));
        Assert.NotNull(Read(EventSubFrames.ChatMessage("!pokearena", sourceBroadcasterId: "42")));
    }


    private static ChatMessage? Read(string frame)
    {
        using var document = JsonDocument.Parse(frame);
        return ChatMessageEvent.Read(document.RootElement.GetProperty("payload").GetProperty("event"), Target);
    }
}
