using System.Collections.Concurrent;
using System.Threading.Channels;
using PokeArena.Core.Twitch.EventSub;

namespace PokeArena.Core.Tests.Fakes;

/// <summary>Hands out <see cref="FakeEventSubSocket"/>s; tests push frames into them.</summary>
public sealed class FakeEventSubSockets : IEventSubSocketFactory
{
    private readonly Channel<FakeEventSubSocket> _opened = Channel.CreateUnbounded<FakeEventSubSocket>();

    public ConcurrentQueue<Uri> ConnectedUrls { get; } = new();

    /// <summary>When set, connecting to this URL fails.</summary>
    public Uri? FailingUrl { get; set; }

    public Task<IEventSubSocket> ConnectAsync(Uri url, CancellationToken ct)
    {
        if (url == FailingUrl)
        {
            throw new HttpRequestException("connect failed");
        }

        ConnectedUrls.Enqueue(url);
        var socket = new FakeEventSubSocket(url);
        _opened.Writer.TryWrite(socket);
        return Task.FromResult<IEventSubSocket>(socket);
    }

    /// <summary>The next socket the session opens.</summary>
    public async Task<FakeEventSubSocket> NextAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return await _opened.Reader.ReadAsync(timeout.Token);
    }
}

public sealed class FakeEventSubSocket(Uri url) : IEventSubSocket
{
    private readonly Channel<string?> _frames = Channel.CreateUnbounded<string?>();

    public Uri Url { get; } = url;

    public bool Closed { get; private set; }

    public bool Disposed { get; private set; }

    public string? CloseReason { get; private set; }

    public void Push(string json) => _frames.Writer.TryWrite(json);

    /// <summary>The server closes the connection.</summary>
    public void ServerClose(string reason)
    {
        CloseReason = reason;
        _frames.Writer.TryWrite(null);
    }

    public async Task<string?> ReceiveAsync(CancellationToken ct) => await _frames.Reader.ReadAsync(ct);

    public Task CloseAsync(CancellationToken ct)
    {
        Closed = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

/// <summary>EventSub frames as Twitch sends them.</summary>
public static class EventSubFrames
{
    private static int _next;

    public static string Welcome(string sessionId = "session-1", int keepaliveSeconds = 10) => $$$"""
        {"metadata":{"message_id":"{{{Id()}}}","message_type":"session_welcome","message_timestamp":"2026-10-12T10:00:00.000000000Z"},
         "payload":{"session":{"id":"{{{sessionId}}}","status":"connected","connected_at":"2026-10-12T10:00:00Z","keepalive_timeout_seconds":{{{keepaliveSeconds}}},"reconnect_url":null} } }
        """;

    public static string Keepalive() => $$$"""
        {"metadata":{"message_id":"{{{Id()}}}","message_type":"session_keepalive","message_timestamp":"2026-10-12T10:00:00Z"},"payload":{}}
        """;

    public static string Reconnect(string url) => $$$"""
        {"metadata":{"message_id":"{{{Id()}}}","message_type":"session_reconnect","message_timestamp":"2026-10-12T10:00:00Z"},
         "payload":{"session":{"id":"session-1","status":"reconnecting","keepalive_timeout_seconds":null,"reconnect_url":"{{{url}}}","connected_at":"2026-10-12T10:00:00Z"} } }
        """;

    public static string Revocation(string status) => $$$"""
        {"metadata":{"message_id":"{{{Id()}}}","message_type":"revocation","message_timestamp":"2026-10-12T10:00:00Z","subscription_type":"channel.chat.message","subscription_version":"1"},
         "payload":{"subscription":{"id":"sub-1","status":"{{{status}}}","type":"channel.chat.message","version":"1"} } }
        """;

    public static string ChatMessage(
        string text,
        string chatterId = "1001",
        string chatterLogin = "ash",
        string chatterName = "Ash",
        string broadcasterId = "42",
        string badgesJson = "[]",
        string? sourceBroadcasterId = null,
        string? messageId = null)
    {
        var source = sourceBroadcasterId is null ? "null" : $"\"{sourceBroadcasterId}\"";
        return $$$"""
            {"metadata":{"message_id":"{{{messageId ?? Id()}}}","message_type":"notification","message_timestamp":"2026-10-12T10:00:00Z","subscription_type":"channel.chat.message","subscription_version":"1"},
             "payload":{"subscription":{"id":"sub-1","type":"channel.chat.message","version":"1"},
              "event":{"broadcaster_user_id":"{{{broadcasterId}}}","broadcaster_user_login":"shiyofurball","broadcaster_user_name":"ShiyoFurball",
                "chatter_user_id":"{{{chatterId}}}","chatter_user_login":"{{{chatterLogin}}}","chatter_user_name":"{{{chatterName}}}",
                "message_id":"msg-{{{Id()}}}","message":{"text":"{{{text}}}","fragments":[]},"color":"#00FF7F","badges":{{{badgesJson}}},
                "message_type":"text","cheer":null,"reply":null,"channel_points_custom_reward_id":null,
                "source_broadcaster_user_id":{{{source}}},"source_message_id":null,"source_badges":null} } }
            """;
    }

    private static string Id() => $"m{Interlocked.Increment(ref _next)}";
}
