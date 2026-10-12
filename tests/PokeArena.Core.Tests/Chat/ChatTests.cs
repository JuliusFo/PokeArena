using System.Text.Json;
using PokeArena.Core.Chat;
using PokeArena.Core.Tests.Fakes;
using PokeArena.Core.Twitch.EventSub;

namespace PokeArena.Core.Tests.Chat;

public class ChatTests
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
    [InlineData("1001", """[{"set_id":"moderator","id":"1","info":""}]""", ChatRole.Moderator)]
    [InlineData("42", """[{"set_id":"broadcaster","id":"1","info":""}]""", ChatRole.Broadcaster)]
    [InlineData("1001", """[{"set_id":"subscriber","id":"12","info":"16"}]""", ChatRole.Viewer)]
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

    [Fact]
    public void Short_text_stays_one_message()
    {
        Assert.Equal(["Hallo Welt"], ChatText.Split("  Hallo Welt "));
        Assert.Empty(ChatText.Split("   "));
    }

    [Fact]
    public void Long_text_splits_at_spaces_within_the_limit()
    {
        var parts = ChatText.Split("aaaa bbbb cccc dddd", maxLength: 10);

        Assert.Equal(["aaaa bbbb", "cccc dddd"], parts);
    }

    [Fact]
    public void Text_without_spaces_is_cut_hard_but_never_inside_an_emoji()
    {
        Assert.Equal(["abcde", "fghij", "k"], ChatText.Split("abcdefghijk", maxLength: 5));
        Assert.Equal(["abcd", "😀e"], ChatText.Split("abcd😀e", maxLength: 5));
    }

    [Fact]
    public void Default_split_respects_twitch_limit()
    {
        var parts = ChatText.Split(string.Join(' ', Enumerable.Repeat("Glumanda", 200)));

        Assert.All(parts, p => Assert.InRange(p.Length, 1, ChatText.MaxLength));
        Assert.Equal(200, parts.Sum(p => p.Split(' ').Length));
    }

    [Fact]
    public void Rate_limiter_spaces_messages_and_caps_the_window()
    {
        var limiter = new SendRateLimiter();
        var start = new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

        Assert.Equal(TimeSpan.Zero, limiter.GetDelay(start));
        limiter.Record(start);
        Assert.Equal(TimeSpan.FromSeconds(1), limiter.GetDelay(start));

        for (var i = 1; i < SendRateLimiter.MaxPerWindow; i++)
        {
            limiter.Record(start.AddSeconds(i));
        }

        var afterBurst = start.AddSeconds(SendRateLimiter.MaxPerWindow);
        Assert.Equal(TimeSpan.FromSeconds(10), limiter.GetDelay(afterBurst)); // Oldest leaves the window at +30 s.
        Assert.Equal(TimeSpan.Zero, limiter.GetDelay(start.AddSeconds(30)));
    }

    private static ChatMessage? Read(string frame)
    {
        using var document = JsonDocument.Parse(frame);
        return ChatMessageEvent.Read(document.RootElement.GetProperty("payload").GetProperty("event"), Target);
    }
}
