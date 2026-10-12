using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Core.Tests.Fakes;
using PokeArena.Core.Twitch.EventSub;

namespace PokeArena.Core.Tests.Twitch;

public class EventSubSessionTests
{
    private readonly FakeEventSubSockets _sockets = new();
    private readonly FakeTimeProvider _time = new();
    private readonly RecordingHandler _handler = new();
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly EventSubSession _session;

    public EventSubSessionTests()
    {
        _session = new EventSubSession(_sockets, _time, NullLogger<EventSubSession>.Instance);
    }

    [Fact]
    public void Parses_every_message_type()
    {
        Assert.Equal(
            new EventSubMessage.Welcome("x", "session-9", TimeSpan.FromSeconds(30)) with { MessageId = "x" },
            EventSubMessage.Parse(EventSubFrames.Welcome("session-9", 30)) with { MessageId = "x" });
        Assert.IsType<EventSubMessage.Keepalive>(EventSubMessage.Parse(EventSubFrames.Keepalive()));
        Assert.Equal(
            new Uri("wss://eventsub.wss.twitch.tv/ws?reconnect=1"),
            Assert.IsType<EventSubMessage.Reconnect>(EventSubMessage.Parse(EventSubFrames.Reconnect("wss://eventsub.wss.twitch.tv/ws?reconnect=1"))).ReconnectUrl);
        Assert.Equal(
            "authorization_revoked",
            Assert.IsType<EventSubMessage.Revocation>(EventSubMessage.Parse(EventSubFrames.Revocation("authorization_revoked"))).Status);
        var notification = Assert.IsType<EventSubMessage.Notification>(EventSubMessage.Parse(EventSubFrames.ChatMessage("hi")));
        Assert.Equal("channel.chat.message", notification.SubscriptionType);
        Assert.Equal("hi", notification.Event.GetProperty("message").GetProperty("text").GetString());
    }

    [Fact]
    public async Task Welcome_starts_the_session_and_notifications_reach_the_handler_once()
    {
        var run = _session.RunAsync(_handler, _stop.Token);
        var socket = await _sockets.NextAsync();

        socket.Push(EventSubFrames.Welcome("session-1"));
        var duplicate = EventSubFrames.ChatMessage("!pokearena", messageId: "dup-1");
        socket.Push(duplicate);
        socket.Push(duplicate);
        socket.Push(EventSubFrames.ChatMessage("hallo"));

        await Eventually.TrueAsync(() => _handler.Texts.Count == 2);
        Assert.Equal(["session-1"], _handler.Sessions);
        Assert.Equal(["!pokearena", "hallo"], _handler.Texts);
        Assert.Equal(EventSubSession.DefaultUrl, socket.Url);
        await StopAsync(run);
    }

    [Fact]
    public async Task Missing_keepalive_ends_the_session_as_connection_lost()
    {
        var run = _session.RunAsync(_handler, _stop.Token);
        var socket = await _sockets.NextAsync();
        socket.Push(EventSubFrames.Welcome(keepaliveSeconds: 10));
        await Eventually.TrueAsync(() => _handler.Sessions.Count == 1);

        socket.Push(EventSubFrames.Keepalive());
        await Task.Delay(50, TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromSeconds(14));
        Assert.False(run.IsCompleted);

        _time.Advance(TimeSpan.FromSeconds(2));

        await Assert.ThrowsAsync<EventSubConnectionLostException>(() => run);
        Assert.True(socket.Disposed);
    }

    [Fact]
    public async Task Server_close_ends_the_session_as_connection_lost()
    {
        var run = _session.RunAsync(_handler, _stop.Token);
        var socket = await _sockets.NextAsync();
        socket.Push(EventSubFrames.Welcome());

        socket.ServerClose("4003 connection unused");

        var ex = await Assert.ThrowsAsync<EventSubConnectionLostException>(() => run);
        Assert.Contains("4003", ex.Message);
    }

    [Fact]
    public async Task Reconnect_keeps_the_old_connection_until_the_new_one_is_welcomed()
    {
        var run = _session.RunAsync(_handler, _stop.Token);
        var old = await _sockets.NextAsync();
        old.Push(EventSubFrames.Welcome("session-1"));

        old.Push(EventSubFrames.Reconnect("wss://eventsub.wss.twitch.tv/ws?id=new"));
        var replacement = await _sockets.NextAsync();
        old.Push(EventSubFrames.ChatMessage("noch alt"));
        await Eventually.TrueAsync(() => _handler.Texts.Count == 1);
        Assert.False(old.Closed);

        replacement.Push(EventSubFrames.Welcome("session-1"));
        await Eventually.TrueAsync(() => old.Disposed);
        replacement.Push(EventSubFrames.ChatMessage("schon neu"));

        await Eventually.TrueAsync(() => _handler.Texts.Count == 2);
        Assert.True(old.Closed);
        Assert.Equal(["session-1"], _handler.Sessions); // Subscriptions moved along; no new subscribe.
        Assert.Equal(new Uri("wss://eventsub.wss.twitch.tv/ws?id=new"), replacement.Url);
        await StopAsync(run);
    }

    [Fact]
    public async Task Failed_reconnect_target_keeps_the_old_connection()
    {
        _sockets.FailingUrl = new Uri("wss://eventsub.wss.twitch.tv/ws?id=broken");
        var run = _session.RunAsync(_handler, _stop.Token);
        var old = await _sockets.NextAsync();
        old.Push(EventSubFrames.Welcome());

        old.Push(EventSubFrames.Reconnect(_sockets.FailingUrl.ToString()));
        old.Push(EventSubFrames.ChatMessage("weiter da"));

        await Eventually.TrueAsync(() => _handler.Texts.Count == 1);
        Assert.False(run.IsCompleted);
        await StopAsync(run);
    }

    [Fact]
    public async Task Malformed_frames_are_skipped()
    {
        var run = _session.RunAsync(_handler, _stop.Token);
        var socket = await _sockets.NextAsync();
        socket.Push(EventSubFrames.Welcome());

        socket.Push("{ not json");
        socket.Push("""{"metadata":{}}""");
        socket.Push(EventSubFrames.ChatMessage("ok"));

        await Eventually.TrueAsync(() => _handler.Texts.Count == 1);
        await StopAsync(run);
    }

    [Fact]
    public async Task Handler_exception_on_revocation_ends_the_session()
    {
        var run = _session.RunAsync(_handler, _stop.Token);
        var socket = await _sockets.NextAsync();
        socket.Push(EventSubFrames.Welcome());

        socket.Push(EventSubFrames.Revocation("user_removed"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => run);
        Assert.Equal("user_removed", ex.Message);
        Assert.True(socket.Disposed);
    }

    private async Task StopAsync(Task run)
    {
        await _stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    private sealed class RecordingHandler : IEventSubHandler
    {
        public ConcurrentQueue<string> Sessions { get; } = new();

        public ConcurrentQueue<string> Texts { get; } = new();

        public Task OnSessionStartedAsync(string sessionId, CancellationToken ct)
        {
            Sessions.Enqueue(sessionId);
            return Task.CompletedTask;
        }

        public Task OnNotificationAsync(EventSubMessage.Notification notification, CancellationToken ct)
        {
            Texts.Enqueue(notification.Event.GetProperty("message").GetProperty("text").GetString()!);
            return Task.CompletedTask;
        }

        public Task OnRevocationAsync(EventSubMessage.Revocation revocation, CancellationToken ct) =>
            throw new InvalidOperationException(revocation.Status);
    }
}
